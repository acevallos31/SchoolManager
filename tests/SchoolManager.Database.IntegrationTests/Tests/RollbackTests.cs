using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RollbackTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Rollbacks_no_eliminan_datos_legacy_y_bloquean_datos_nuevos()
    {
        await ExecuteAsync("insert into public.alumnos (nombres, apellidos, dni) values ('Legacy', 'Alumno', 'legacy-001')");

        await MigrationRunner.RevertActiveAsync(fixture.DataSource);

        var legacyCount = await ScalarLongAsync("select count(*) from public.alumnos where dni = 'legacy-001'");
        Assert.Equal(1, legacyCount);
        Assert.Equal("", await ScalarStringAsync("select coalesce(to_regclass('public.personas')::text, '')"));

        await MigrationRunner.ApplyActiveAsync(fixture.DataSource);
        await ExecuteAsync("insert into public.personas (nombres, apellidos) values ('Nueva', 'Persona')");

        await Assert.ThrowsAsync<PostgresException>(() => MigrationRunner.RevertAsync(fixture.DataSource, "003"));
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
