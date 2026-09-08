using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class EstructuraValidationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("select 1", null)]
    [InlineData("update public.permisos set codigo = 'academico.estructura.prueba' where codigo = 'academico.estructura.ver'", "permiso_aplicacion_faltante")]
    [InlineData("update public.permisos set codigo = 'configuracion.grados.prueba' where codigo = 'configuracion.grados.ver'", "permiso_interno_faltante")]
    [InlineData("update public.roles set activo = false where codigo = 'admin'", "permiso_sin_grant_admin")]
    [InlineData("update public.schema_migrations set version = '924' where version = '024'", "migracion_no_registrada")]
    public async Task Validador_conserva_diagnosticos_y_rechaza_inconsistencias(string cambio, string? esperado)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        // Fallos sintéticos solo en el contenedor; dispose revierte la transacción.
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var mutation = new NpgsqlCommand(cambio, connection, transaction))
            await mutation.ExecuteNonQueryAsync();
        var path = MigrationRunner.GetActiveValidationPaths()
            .Single(p => Path.GetFileName(p).StartsWith("024_", StringComparison.Ordinal));
        await using var validation = new NpgsqlCommand(await File.ReadAllTextAsync(path), connection, transaction);
        await using var reader = await validation.ExecuteReaderAsync();
        var diagnosticos = new List<string>();
        while (await reader.ReadAsync()) diagnosticos.Add(reader.GetString(0));
        if (esperado is null) Assert.Empty(diagnosticos);
        else Assert.Contains(esperado, diagnosticos);
    }
}
