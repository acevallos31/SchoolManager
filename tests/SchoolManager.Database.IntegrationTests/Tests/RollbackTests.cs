using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RollbackTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Rollbacks_no_eliminan_datos_legacy_y_bloquean_datos_nuevos()
    {
        await ExecuteAsync("""
            with usuario_insertado as (
              insert into public.usuarios (usuario) values ('legacy-001') returning id
            )
            insert into public.usuarios_roles (usuario_id, rol_id)
            select u.id, r.id
            from usuario_insertado u
            cross join public.roles r
            where r.codigo = 'usuario';
            """);

        await MigrationRunner.RevertActiveAsync(fixture.DataSource);

        var legacyCount = await ScalarLongAsync("select count(*) from public.usuarios where usuario = 'legacy-001'");
        Assert.Equal(1, legacyCount);
        Assert.Equal("", await ScalarStringAsync("select coalesce(to_regclass('public.personas')::text, '')"));

        await MigrationRunner.ApplyActiveAsync(fixture.DataSource);
        await ExecuteAsync("insert into public.personas (nombres, apellidos) values ('Nueva', 'Persona')");

        await Assert.ThrowsAsync<PostgresException>(() => MigrationRunner.RevertAsync(fixture.DataSource, "003"));
    }

    [Fact]
    public async Task Rollback_010_reconstruye_rol_si_hay_una_asignacion_legacy_unica()
    {
        await ExecuteAsync("""
            with usuario_insertado as (
              insert into public.usuarios (usuario) values ('rollback-010-unico') returning id
            )
            insert into public.usuarios_roles (usuario_id, rol_id)
            select u.id, r.id from usuario_insertado u cross join public.roles r
            where r.codigo = 'padre';
            """);

        await MigrationRunner.RevertAsync(fixture.DataSource, "010");

        Assert.Equal("padre", await ScalarStringAsync(
            "select rol from public.usuarios where usuario = 'rollback-010-unico'"));
        await MigrationRunner.ApplyActiveAsync(fixture.DataSource);
    }

    [Fact]
    public async Task Rollback_010_aborta_si_un_usuario_tiene_multiples_roles()
    {
        await ExecuteAsync("""
            with usuario_insertado as (
              insert into public.usuarios (usuario) values ('rollback-010-multi') returning id
            )
            insert into public.usuarios_roles (usuario_id, rol_id)
            select u.id, r.id from usuario_insertado u
            cross join public.roles r where r.codigo in ('admin', 'operador');
            """);

        await Assert.ThrowsAsync<PostgresException>(
            () => MigrationRunner.RevertAsync(fixture.DataSource, "010"));

        await ExecuteAsync("""
            delete from public.usuarios_roles
            where usuario_id = (select id from public.usuarios where usuario = 'rollback-010-multi');
            delete from public.usuarios where usuario = 'rollback-010-multi';
            """);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarLongAsync(string sql)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> ScalarStringAsync(string sql)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
