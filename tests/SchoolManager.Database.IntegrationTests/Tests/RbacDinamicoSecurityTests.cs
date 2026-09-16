using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RbacDinamicoSecurityTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Admin_legacy_no_puede_asignar_platform_admin()
    {
        var admin = await InsertUsuarioConRolGlobalAsync("admin");
        var destino = await InsertUsuarioSinRolAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthScalarGuidAsync(
                admin.AuthUserId,
                "select public.rpc_asignar_rol_usuario($1, 'platform_admin', null)",
                destino.UsuarioId));

        Assert.Equal("42501", exception.SqlState);
        Assert.Equal(0, await ScalarLongAsync("""
            select count(*)
            from public.usuarios_roles ur
            join public.roles r on r.id = ur.rol_id
            where ur.usuario_id = $1
              and ur.activo
              and r.codigo = 'platform_admin'
            """, destino.UsuarioId));
    }

    [Fact]
    public async Task Superadmin_puede_asignar_otro_superadmin()
    {
        var bootstrap = await InsertUsuarioConRolGlobalAsync("platform_admin");
        var destino = await InsertUsuarioSinRolAsync();
        Guid? nuevaAsignacion = null;

        try
        {
            nuevaAsignacion = await AuthScalarGuidAsync(
                bootstrap.AuthUserId,
                "select public.rpc_asignar_rol_usuario($1, 'platform_admin', null)",
                destino.UsuarioId);

            Assert.Equal(1, await ScalarLongAsync("""
                select count(*)
                from public.usuarios_roles ur
                join public.roles r on r.id = ur.rol_id
                where ur.id = $1
                  and ur.usuario_id = $2
                  and ur.institucion_id is null
                  and ur.activo
                  and r.codigo = 'platform_admin'
                  and r.tipo = 'plataforma'
                """, nuevaAsignacion.Value, destino.UsuarioId));
        }
        finally
        {
            if (nuevaAsignacion.HasValue)
            {
                await DesactivarAsignacionDirectaAsync(nuevaAsignacion.Value);
            }
            await DesactivarAsignacionDirectaAsync(bootstrap.AsignacionId);
        }
    }

    [Fact]
    public async Task Ultimo_superadmin_activo_no_puede_retirarse()
    {
        var bootstrap = await InsertUsuarioConRolGlobalAsync("platform_admin");

        try
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                AuthExecuteAsync(
                    bootstrap.AuthUserId,
                    "select public.rpc_desactivar_rol_usuario($1, $2)",
                    bootstrap.AsignacionId, "intento de prueba"));

            Assert.Equal("23514", exception.SqlState);
            Assert.Equal(1, await ScalarLongAsync(
                "select count(*) from public.usuarios_roles where id = $1 and activo",
                bootstrap.AsignacionId));
        }
        finally
        {
            await DesactivarAsignacionDirectaAsync(bootstrap.AsignacionId);
        }
    }

    [Fact]
    public async Task Admin_legacy_no_ve_roles_de_plataforma_por_RLS()
    {
        var admin = await InsertUsuarioConRolGlobalAsync("admin");

        Assert.Equal(0, await AuthScalarLongAsync(
            admin.AuthUserId,
            "select count(*) from public.roles where codigo = 'platform_admin'"));
    }

    [Fact]
    public async Task Superadmin_si_ve_roles_de_plataforma_por_RLS()
    {
        var bootstrap = await InsertUsuarioConRolGlobalAsync("platform_admin");

        try
        {
            Assert.Equal(1, await AuthScalarLongAsync(
                bootstrap.AuthUserId,
                "select count(*) from public.roles where codigo = 'platform_admin'"));
        }
        finally
        {
            await DesactivarAsignacionDirectaAsync(bootstrap.AsignacionId);
        }
    }

    private async Task<IdentidadConAsignacion> InsertUsuarioConRolGlobalAsync(string rolCodigo)
    {
        var usuario = await InsertUsuarioSinRolAsync();
        var rolId = await ScalarGuidAsync(
            "select id from public.roles where codigo = $1 and institucion_id is null",
            rolCodigo);
        var asignacionId = await ScalarGuidAsync("""
            insert into public.usuarios_roles (usuario_id, rol_id)
            values ($1, $2)
            returning id
            """, usuario.UsuarioId, rolId);
        return new IdentidadConAsignacion(
            usuario.UsuarioId, usuario.PersonaId, usuario.AuthUserId, asignacionId);
    }

    private async Task<Identidad> InsertUsuarioSinRolAsync()
    {
        var personaId = await ScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUserId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync("""
            insert into public.usuarios (persona_id, auth_user_id, activo)
            values ($1, $2, true)
            returning id
            """, personaId, authUserId);
        return new Identidad(usuarioId, personaId, authUserId);
    }

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

    private Task<long> AuthScalarLongAsync(Guid authUserId, string sql, params object[] values) =>
        AuthScalarAsync<long>(authUserId, sql, values);

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

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    private sealed record Identidad(Guid UsuarioId, Guid PersonaId, Guid AuthUserId);
    private sealed record IdentidadConAsignacion(
        Guid UsuarioId,
        Guid PersonaId,
        Guid AuthUserId,
        Guid AsignacionId);
}
