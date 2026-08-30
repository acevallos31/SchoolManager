using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class InstitutionConfigurationTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Admin_crea_primera_institucion_y_single_rechaza_segunda()
    {
        await ResetAsync();
        var admin = await InsertUsuarioAsync("admin");

        Assert.Equal("No hay un centro educativo configurado.", await AuthScalarAsync<string>(
            admin,
            "select case when public.rpc_obtener_configuracion_institucion()->'institucion' = 'null'::jsonb then 'No hay un centro educativo configurado.' end"));

        var institucion = await AuthScalarAsync<Guid>(admin, """
            select (public.rpc_crear_institucion(
              ' Centro Uno ', 'CU', 'Direccion', '2222-2222', 'INFO@EJEMPLO.COM', null,
              true, true, false, array['identidad', 'pasaporte']
            )->'institucion'->>'id')::uuid
            """);
        Assert.NotEqual(Guid.Empty, institucion);

        var conflicto = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin, "select public.rpc_crear_institucion('Centro Dos')"));
        Assert.Equal("SM004", conflicto.SqlState);

        Assert.Equal(1L, await ScalarAsync<long>(
            "select count(*) from public.configuracion_identificadores where institucion_id = $1",
            institucion));
    }

    [Fact]
    public async Task Admin_edita_datos_e_identificadores_y_contexto_single_sigue_funcionando()
    {
        await ResetAsync();
        var admin = await InsertUsuarioAsync("admin");
        var institucion = await InsertInstitucionAsync();

        var nombre = await AuthScalarAsync<string>(admin, """
            select public.rpc_actualizar_institucion(
              $1, 'Centro Actualizado', 'CA', 'Nueva direccion', '9999-9999',
              'centro@ejemplo.com', 'https://ejemplo.com/logo.png', true, false, true,
              array['identidad', 'otro']
            )->'institucion'->>'nombre'
            """, institucion);
        Assert.Equal("Centro Actualizado", nombre);
        Assert.Equal("Centro Actualizado", await AuthScalarAsync<string>(admin,
            "select public.rpc_obtener_configuracion_institucion()->'institucion'->>'nombre'"));
        Assert.True(await ScalarAsync<bool>("""
            select rne_requerido and codigo_interno_requerido
            from public.configuracion_identificadores where institucion_id = $1
            """, institucion));
    }

    [Fact]
    public async Task Validaciones_permisos_y_escritura_directa_se_aplican()
    {
        await ResetAsync();
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioAsync("admin");
        var usuario = await InsertUsuarioAsync("usuario", institucion);

        var nombreVacio = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin, "select public.rpc_actualizar_institucion($1, '   ')", institucion));
        Assert.Equal("22023", nombreVacio.SqlState);

        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            usuario, "select public.rpc_actualizar_institucion($1, 'Sin permiso')", institucion));
        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin, "update public.instituciones set nombre = 'Directo' where id = $1", institucion));
        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin, "delete from public.instituciones where id = $1", institucion));
        Assert.False(await ScalarAsync<bool>(
            "select to_regprocedure('public.rpc_eliminar_institucion(uuid)') is not null"));
    }

    [Fact]
    public async Task Multi_exige_institucion_explicita_y_no_inventa_contexto()
    {
        await ResetAsync();
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioAsync("admin");
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");

        var sinContexto = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin, "select public.rpc_obtener_configuracion_institucion()"));
        Assert.Equal("SM003", sinContexto.SqlState);
        Assert.Equal(institucion, await AuthScalarAsync<Guid>(admin,
            "select (public.rpc_obtener_configuracion_institucion($1)->'institucion'->>'id')::uuid",
            institucion));
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarAsync<Guid>(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private async Task ResetAsync()
    {
        await ExecuteAsync("delete from public.usuarios_roles");
        await ExecuteAsync("delete from public.usuarios");
        await ExecuteAsync("delete from public.personas");
        await ExecuteAsync("delete from public.configuracion_identificadores");
        await ExecuteAsync("delete from public.instituciones");
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = false where id = 1");
    }

    private async Task<Guid> InsertUsuarioAsync(string rol, Guid? institucionId = null)
    {
        var persona = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUser = Guid.NewGuid();
        var usuario = await ScalarAsync<Guid>(
            "insert into public.usuarios (persona_id, auth_user_id) values ($1, $2) returning id",
            persona, authUser);
        var rolId = await ScalarAsync<Guid>("select id from public.roles where codigo = $1", rol);
        if (institucionId.HasValue)
        {
            await ExecuteAsync(
                "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)",
                usuario, rolId, institucionId.Value);
        }
        else
        {
            await ExecuteAsync(
                "insert into public.usuarios_roles (usuario_id, rol_id) values ($1, $2)", usuario, rolId);
        }
        return authUser;
    }

    private async Task<T> AuthScalarAsync<T>(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetAuthenticatedAsync(connection, transaction, authUserId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, values);
        var result = (T)(await command.ExecuteScalarAsync())!;
        await transaction.CommitAsync();
        return result;
    }

    private async Task AuthExecuteAsync(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await SetAuthenticatedAsync(connection, transaction, authUserId);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task SetAuthenticatedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid authUserId)
    {
        await new NpgsqlCommand("set local role authenticated", connection, transaction)
            .ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand(
            "select set_config('request.jwt.claim.sub', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(authUserId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value);
    }
}
