using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class BootstrapPlatformAdminTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Bootstrap_inicial_asigna_unico_superadmin_audita_y_no_se_puede_repetir()
    {
        var primerAuth = Guid.NewGuid();
        var primerUsuario = await CrearUsuarioAsync(primerAuth);

        await EjecutarBootstrapAsync(primerAuth);

        Assert.Equal(1, await ScalarLongAsync("""
            select count(*)
            from public.usuarios_roles ur
            join public.roles r on r.id=ur.rol_id
            join public.usuarios u on u.id=ur.usuario_id
            where ur.activo and ur.institucion_id is null
              and r.activo and r.tipo='plataforma' and r.codigo='platform_admin'
              and u.activo
            """));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*)
            from public.usuarios_roles ur
            join public.roles r on r.id=ur.rol_id
            where ur.usuario_id=$1 and ur.activo and ur.institucion_id is null
              and r.codigo='platform_admin' and r.tipo='plataforma'
            """, primerUsuario));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.seguridad_auditoria
            where accion='platform.superadmin.bootstrap_inicial'
              and detalle->>'usuario_id'=$1
            """, primerUsuario.ToString()));

        var segundoAuth = Guid.NewGuid();
        var segundoUsuario = await CrearUsuarioAsync(segundoAuth);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => EjecutarBootstrapAsync(segundoAuth));
        Assert.Equal("23514", ex.SqlState);
        Assert.Equal(0, await ScalarLongAsync("""
            select count(*)
            from public.usuarios_roles ur
            join public.roles r on r.id=ur.rol_id
            where ur.usuario_id=$1 and ur.activo and r.codigo='platform_admin'
            """, segundoUsuario));
    }

    [Fact]
    public async Task Bootstrap_sin_auth_user_id_falla_antes_de_modificar_asignaciones()
    {
        var antes = await ScalarLongAsync("""
            select count(*) from public.usuarios_roles ur
            join public.roles r on r.id=ur.rol_id
            where ur.activo and r.codigo='platform_admin' and r.tipo='plataforma'
            """);

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        var script = await File.ReadAllTextAsync(RutaBootstrap());
        await using var command = new NpgsqlCommand(script, connection);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("22023", ex.SqlState);

        var despues = await ScalarLongAsync("""
            select count(*) from public.usuarios_roles ur
            join public.roles r on r.id=ur.rol_id
            where ur.activo and r.codigo='platform_admin' and r.tipo='plataforma'
            """);
        Assert.Equal(antes, despues);
    }

    private async Task EjecutarBootstrapAsync(Guid authUserId)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using (var setting = new NpgsqlCommand(
            "select set_config('schoolmanager.bootstrap_auth_user_id', $1, false)", connection))
        {
            setting.Parameters.AddWithValue(authUserId.ToString());
            await setting.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(
            await File.ReadAllTextAsync(RutaBootstrap()), connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> CrearUsuarioAsync(Guid authUserId)
    {
        await using var command = fixture.DataSource.CreateCommand("""
            with persona as (
              insert into public.personas(nombres,apellidos)
              values('Bootstrap','042') returning id
            )
            insert into public.usuarios(persona_id,auth_user_id,activo)
            select id,$1,true from persona
            returning id
            """);
        command.Parameters.AddWithValue(authUserId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> ScalarLongAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static string RutaBootstrap()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "database",
                "operations",
                "bootstrap_first_platform_admin.sql");
            if (File.Exists(path)) return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No se encontro database/operations/bootstrap_first_platform_admin.sql.");
    }
}
