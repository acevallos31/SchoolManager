using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class ConfigurationImplementationTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Singleton_y_resolucion_single_multi_cumplen_reglas()
    {
        await ResetAsync();
        Assert.False(await ScalarAsync<bool>(
            "select multiples_instituciones from public.configuracion_implementacion where id = 1"));

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "insert into public.configuracion_implementacion (id) values (2)"));

        var sinInstitucion = await Assert.ThrowsAsync<PostgresException>(() => ScalarAsync<Guid>(
            "select institucion_id from public.resolver_contexto_institucional()"));
        Assert.Equal("SM001", sinInstitucion.SqlState);

        var institucion = await InsertInstitucionAsync();
        Assert.Equal(institucion, await ScalarAsync<Guid>(
            "select institucion_id from public.resolver_contexto_institucional()"));

        await InsertInstitucionAsync();
        var multiplesEnSingle = await Assert.ThrowsAsync<PostgresException>(() => ScalarAsync<Guid>(
            "select institucion_id from public.resolver_contexto_institucional()"));
        Assert.Equal("SM002", multiplesEnSingle.SqlState);

        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");
        Assert.True(await ScalarAsync<bool>(
            "select multiples_instituciones from public.resolver_contexto_institucional()"));
        Assert.True(await ScalarAsync<bool>(
            "select institucion_id is null from public.resolver_contexto_institucional()"));
    }

    [Fact]
    public async Task Rpc_actualizacion_exige_admin_y_bloquea_downgrade_ambiguo()
    {
        await ResetAsync();
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioAsync("admin", null);
        var usuario = await InsertUsuarioAsync("usuario", institucion);

        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            usuario, "select public.rpc_actualizar_multiples_instituciones(true)"));

        await AuthExecuteAsync(admin,
            "select public.rpc_actualizar_multiples_instituciones(true)");
        Assert.True(await ScalarAsync<bool>(
            "select multiples_instituciones from public.configuracion_implementacion where id = 1"));

        await InsertInstitucionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin, "select public.rpc_actualizar_multiples_instituciones(false)"));
        Assert.Equal("SM002", error.SqlState);
        Assert.True(await ScalarAsync<bool>(
            "select multiples_instituciones from public.configuracion_implementacion where id = 1"));

        Assert.True(await AuthScalarAsync<bool>(admin,
            "select (public.rpc_obtener_contexto_implementacion()->>'multiplesInstituciones')::boolean"));
    }

    [Fact]
    public async Task Rls_impide_lectura_y_escritura_directa_authenticated()
    {
        await ResetAsync();
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioAsync("admin", null);

        await Assert.ThrowsAsync<PostgresException>(() => AuthScalarAsync<long>(admin,
            "select count(*) from public.configuracion_implementacion"));
        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(admin,
            "update public.configuracion_implementacion set multiples_instituciones = true"));

        var nombre = await AuthScalarAsync<string>(admin,
            "select public.rpc_obtener_contexto_implementacion()->'institucion'->>'nombre'");
        Assert.False(string.IsNullOrWhiteSpace(nombre));
        Assert.NotEqual(Guid.Empty, institucion);
    }

    private async Task ResetAsync()
    {
        await ExecuteAsync("delete from public.usuarios_roles");
        await ExecuteAsync("delete from public.usuarios");
        await ExecuteAsync("delete from public.personas");
        await ExecuteAsync("delete from public.instituciones");
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = false where id = 1");
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarAsync<Guid>(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private async Task<Guid> InsertUsuarioAsync(string rol, Guid? institucionId)
    {
        var persona = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUser = Guid.NewGuid();
        var usuario = await ScalarAsync<Guid>(
            "insert into public.usuarios (persona_id, auth_user_id) values ($1, $2) returning id",
            persona, authUser);
        var rolId = await ScalarAsync<Guid>("select id from public.roles where codigo = $1", rol);
        await ExecuteAsync(institucionId.HasValue
            ? "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)"
            : "insert into public.usuarios_roles (usuario_id, rol_id) values ($1, $2)",
            institucionId.HasValue
                ? new object[] { usuario, rolId, institucionId.Value }
                : new object[] { usuario, rolId });
        return authUser;
    }

    private async Task<T> AuthScalarAsync<T>(Guid authUserId, string sql)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetAuthenticatedAsync(connection, transaction, authUserId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var result = (T)(await command.ExecuteScalarAsync())!;
        await transaction.CommitAsync();
        return result;
    }

    private async Task AuthExecuteAsync(Guid authUserId, string sql)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await SetAuthenticatedAsync(connection, transaction, authUserId);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
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
