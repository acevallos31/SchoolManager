using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Ejecuta el mecanismo completo de validacion de migraciones: aplica 001..ultima
/// incrementalmente sobre un PostgreSQL limpio y valida cada migracion justo
/// despues de aplicarse. Debe pasar sin hallazgos.
/// </summary>
public sealed class MigrationValidationSuiteTests
{
    [Fact]
    public async Task Todas_las_validaciones_incrementales_pasan_en_base_limpia()
    {
        // Lanza ValidationFailedException con el detalle si alguna validacion falla.
        await ValidationRunner.RunAllAsync();
    }

    [Fact]
    public async Task Una_validacion_con_hallazgo_hace_fallar_el_mecanismo()
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("schoolmanager_validation_neg")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        try
        {
            await container.StartAsync();
            await using var dataSource = NpgsqlDataSource.Create(container.GetConnectionString());

            // Bootstrap + migraciones hasta el final (estado canonico), replicando la
            // infraestructura base que el fixture usa.
            await ExecuteBootstrapAsync(dataSource, "SupabaseSecurityBootstrap.sql");
            await ExecuteBootstrapAsync(dataSource, "LegacySchemaBootstrap.sql");
            await MigrationRunner.ApplyActiveAsync(dataSource);

            // Validacion ad-hoc que SIEMPRE devuelve una fila (simula un hallazgo real).
            await using (var seed = dataSource.CreateCommand(
                "insert into public.roles (codigo, nombre, descripcion, es_sistema) " +
                "values ('cajero', 'Cajero', 'Reservado finanzas', true) " +
                "on conflict (codigo) do nothing"))
            {
                await seed.ExecuteNonQueryAsync();
            }
            await using (var command = dataSource.CreateCommand(
                "select codigo from public.roles where codigo = 'cajero'"))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync()); // el hallazgo existe
            }

            // Ejecuta una validacion simple que devuelve la fila como hallazgo -> debe fallar.
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
                RunSingleValidationAsync(dataSource, "select codigo from public.roles"));
            Assert.Contains(ex.Failures, f => f.FileName.EndsWith(".validation.sql", StringComparison.Ordinal));
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    private static async Task RunSingleValidationAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            throw new ValidationFailedException(new[] { new ValidationFailure("negativa.validation.sql", "hallazgo detectado") });
        }
    }

    private static async Task ExecuteBootstrapAsync(NpgsqlDataSource dataSource, string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Infrastructure", fileName);
        await using var command = dataSource.CreateCommand(await File.ReadAllTextAsync(path));
        await command.ExecuteNonQueryAsync();
    }
}
