using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Pruebas del mecanismo de checksum (SHA-256) sobre schema_migrations.
/// </summary>
public sealed class ChecksumMigrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Migracion_aplicada_queda_registrada_con_checksum()
    {
        // Las migraciones 007-019 auto-registran su fila; el runner puebla el checksum.
        foreach (var version in new[] { "007", "019" })
        {
            var path = MigrationRunner.GetActiveMigrationPaths()
                .Single(p => Path.GetFileName(p).StartsWith(version + "_", StringComparison.Ordinal));
            var expected = MigrationRunner.ComputeChecksum(path);
            Assert.Equal(expected, await ScalarStringAsync(
                "select checksum from public.schema_migrations where version = $1", version));
        }
    }

    [Fact]
    public async Task Reaplicar_migraciones_con_archivo_intacto_pasa()
    {
        await MigrationRunner.ApplyActiveAsync(fixture.DataSource); // no debe lanzar
    }

    [Fact]
    public async Task Contenido_modificado_falla_con_error_explicito()
    {
        var version = "008";
        var original = await ScalarStringAsync(
            "select checksum from public.schema_migrations where version = $1", version);
        try
        {
            await SetChecksumAsync(version, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            var ex = await Assert.ThrowsAsync<MigrationChecksumMismatchException>(
                () => MigrationRunner.ApplyActiveAsync(fixture.DataSource));
            Assert.Equal(version, ex.Version);
            Assert.Contains("modificada", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await SetChecksumAsync(version, original);
        }
    }

    [Fact]
    public async Task Fila_historica_con_checksum_nulo_se_retrorellena_y_no_bloquea()
    {
        // Simula una instalacion previa a este mecanismo (checksum NULL) sobre una
        // migracion ya aplicada: no debe lanzar y debe quedar con checksum poblado.
        var version = "009";
        var path = MigrationRunner.GetActiveMigrationPaths()
            .Single(p => Path.GetFileName(p).StartsWith(version + "_", StringComparison.Ordinal));
        var expected = MigrationRunner.ComputeChecksum(path);

        await SetChecksumAsync(version, null);
        await MigrationRunner.ApplyActiveAsync(fixture.DataSource); // no debe lanzar

        Assert.Equal(expected, await ScalarStringAsync(
            "select checksum from public.schema_migrations where version = $1", version));
    }

    [Fact]
    public async Task Todas_las_migraciones_autoregistradas_tienen_checksum()
    {
        // Secuencia limpia 001->019: las versiones con fila (007-019) deben tener checksum.
        foreach (var version in Enumerable.Range(7, 13).Select(n => n.ToString("D3")))
        {
            var stored = await ScalarStringAsync(
                "select checksum from public.schema_migrations where version = $1", version);
            Assert.False(string.IsNullOrEmpty(stored), $"version {version} deberia tener checksum");
        }
    }

    private async Task SetChecksumAsync(string version, string? checksum)
    {
        await using var command = fixture.DataSource.CreateCommand(
            "update public.schema_migrations set checksum = $2 where version = $1");
        command.Parameters.AddWithValue(version);
        command.Parameters.AddWithValue(checksum is null ? DBNull.Value : checksum);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string?> ScalarStringAsync(string sql, string version)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(version);
        var value = await command.ExecuteScalarAsync();
        return value is null || value is DBNull ? null : (string)value;
    }
}
