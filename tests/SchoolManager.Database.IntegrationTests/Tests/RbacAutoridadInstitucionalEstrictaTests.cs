using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RbacAutoridadInstitucionalEstrictaTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Admin_global_legacy_no_puede_crear_rol_institucional()
    {
        var institucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioConRolAsync("admin", null);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthScalarGuidAsync(
                actor.AuthUserId,
                "select public.rpc_crear_rol_institucional($1, $2, $3, null)",
                institucion,
                Codigo("secretaria"),
                "Secretaria"));

        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task Admin_con_asignacion_explicita_en_institucion_si_puede_crear_rol()
    {
        var institucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioConRolAsync("admin", institucion);
        var codigo = Codigo("secretaria");

        var rolId = await AuthScalarGuidAsync(
            actor.AuthUserId,
            "select public.rpc_crear_rol_institucional($1, $2, $3, null)",
            institucion,
            codigo,
            "Secretaria");

        Assert.Equal(1, await ScalarLongAsync("""
            select count(*)
            from public.roles
            where id = $1
              and codigo = $2
              and institucion_id = $3
              and tipo = 'institucional'
              and activo
            """, rolId, codigo, institucion));
    }

    [Fact]
    public async Task Superadmin_global_es_excepcion_controlada_y_puede_crear_rol_institucional()
    {
        var institucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioConRolAsync("platform_admin", null);
        var codigo = Codigo("soporte");

        try
        {
            var rolId = await AuthScalarGuidAsync(
                actor.AuthUserId,
                "select public.rpc_crear_rol_institucional($1, $2, $3, null)",
                institucion,
                codigo,
                "Soporte" );

            Assert.Equal(1, await ScalarLongAsync(
                "select count(*) from public.roles where id = $1 and institucion_id = $2",
                rolId, institucion));
        }
        finally
        {
            await DesactivarAsignacionDirectaAsync(actor.AsignacionId);
        }
    }

    [Fact]
    public async Task Rpc_general_no_permite_a_admin_global_asignar_rol_institucional()
    {
        var institucion = await InsertInstitucionAsync();
        var rolId = await InsertRolInstitucionalAsync(Codigo("operacion"), institucion);
        var actor = await InsertUsuarioConRolAsync("admin", null);
        var destino = await InsertUsuarioAsync();
        var codigoRol = await ScalarStringAsync(
            "select codigo from public.roles where id = $1", rolId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthScalarGuidAsync(
                actor.AuthUserId,
                "select public.rpc_asignar_rol_usuario($1, $2, $3)",
                destino.UsuarioId, codigoRol, institucion));

        Assert.Equal("42501", exception.SqlState);
        Assert.Equal(0, await ScalarLongAsync("""
            select count(*) from public.usuarios_roles
            where usuario_id = $1 and rol_id = $2 and activo
            """, destino.UsuarioId, rolId));
    }

    [Fact]
    public async Task Cota_de_delegacion_ignora_permiso_heredado_solo_por_admin_global()
    {
        var institucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioAsync();
        var adminGlobalId = await GetRolIdAsync("admin");
        var gestorId = await InsertRolInstitucionalAsync(Codigo("gestor_rbac"), institucion);
        var destinoId = await InsertRolInstitucionalAsync(Codigo("destino"), institucion);
        var permisoAsignarId = await GetPermisoIdAsync("identidad.roles.asignar_permisos");

        await ExecuteAsync(
            "insert into public.roles_permisos(rol_id, permiso_id) values ($1, $2)",
            gestorId, permisoAsignarId);
        await ExecuteAsync(
            "insert into public.usuarios_roles(usuario_id, rol_id) values ($1, $2)",
            actor.UsuarioId, adminGlobalId);
        await ExecuteAsync("""
            insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
            values ($1, $2, $3)
            """, actor.UsuarioId, gestorId, institucion);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthExecuteAsync(
                actor.AuthUserId,
                "select public.rpc_reemplazar_permisos_rol_institucional($1, $2::text[])",
                destinoId,
                new[] { "academico.alumnos.ver" }));

        Assert.Equal("42501", exception.SqlState);
        Assert.Equal(0, await ScalarLongAsync(
            "select count(*) from public.roles_permisos where rol_id = $1",
            destinoId));
    }

    private async Task<IdentidadConAsignacion> InsertUsuarioConRolAsync(
        string rolCodigo,
        Guid? institucionId)
    {
        var usuario = await InsertUsuarioAsync();
        var rolId = await GetRolIdAsync(rolCodigo);
        var asignacionId = institucionId.HasValue
            ? await ScalarGuidAsync("""
                insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
                values ($1, $2, $3)
                returning id
                """, usuario.UsuarioId, rolId, institucionId.Value)
            : await ScalarGuidAsync("""
                insert into public.usuarios_roles(usuario_id, rol_id)
                values ($1, $2)
                returning id
                """, usuario.UsuarioId, rolId);

        return new IdentidadConAsignacion(
            usuario.UsuarioId, usuario.AuthUserId, asignacionId);
    }

    private async Task<Identidad> InsertUsuarioAsync()
    {
        var personaId = await ScalarGuidAsync(
            "insert into public.personas(nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUserId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync("""
            insert into public.usuarios(persona_id, auth_user_id, activo)
            values ($1, $2, true)
            returning id
            """, personaId, authUserId);
        return new Identidad(usuarioId, authUserId);
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones(nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private Task<Guid> InsertRolInstitucionalAsync(string codigo, Guid institucionId) =>
        ScalarGuidAsync("""
            insert into public.roles(codigo, nombre, activo, tipo, institucion_id)
            values ($1, $2, true, 'institucional', $3)
            returning id
            """, codigo, $"Rol {codigo}", institucionId);

    private Task<Guid> GetRolIdAsync(string codigo) => ScalarGuidAsync(
        "select id from public.roles where codigo = $1 and institucion_id is null",
        codigo);

    private Task<Guid> GetPermisoIdAsync(string codigo) => ScalarGuidAsync(
        "select id from public.permisos where codigo = $1", codigo);

    private async Task DesactivarAsignacionDirectaAsync(Guid asignacionId)
    {
        await ExecuteAsync("""
            update public.usuarios_roles
            set activo = false,
                fecha_desactivacion = now(),
                motivo_desactivacion = 'cleanup de prueba',
                updated_at = now()
            where id = $1 and activo
            """, asignacionId);
    }

    private Task<Guid> AuthScalarGuidAsync(Guid authUserId, string sql, params object[] values) =>
        AuthScalarAsync<Guid>(authUserId, sql, values);

    private async Task<T> AuthScalarAsync<T>(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await SetAuthenticatedAsync(connection, transaction, authUserId);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            var result = (T)(await command.ExecuteScalarAsync())!;
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid authUserId)
    {
        await using var roleCommand = new NpgsqlCommand(
            "set local role authenticated", connection, transaction);
        await roleCommand.ExecuteNonQueryAsync();
        await using var authCommand = new NpgsqlCommand(
            "select set_config('request.jwt.claim.sub', $1, true)", connection, transaction);
        authCommand.Parameters.AddWithValue(authUserId.ToString());
        await authCommand.ExecuteNonQueryAsync();
    }

    private async Task ExecuteAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> ScalarLongAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> ScalarStringAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    private static string Codigo(string prefijo) => $"{prefijo}_{Guid.NewGuid():N}";

    private sealed record Identidad(Guid UsuarioId, Guid AuthUserId);
    private sealed record IdentidadConAsignacion(
        Guid UsuarioId,
        Guid AuthUserId,
        Guid AsignacionId);
}
