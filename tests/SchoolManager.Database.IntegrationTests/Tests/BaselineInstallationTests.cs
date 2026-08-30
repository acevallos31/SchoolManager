using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class BaselineInstallationTests
{
    [Fact]
    public async Task Baseline_se_instala_en_PostgreSQL_limpio()
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("schoolmanager_baseline_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        try
        {
            await container.StartAsync();
            await using var dataSource = NpgsqlDataSource.Create(container.GetConnectionString());

            var baselinePath = Path.Combine(
                FindRepositoryRoot(),
                "database",
                "baseline",
                "001_schoolmanager_fase1a.sql");

            await using (var command = dataSource.CreateCommand(await File.ReadAllTextAsync(baselinePath)))
            {
                await command.ExecuteNonQueryAsync();
            }

            var expectedTables = new[]
            {
                "alumno_responsable",
                "alumnos",
                "ciclos_escolares",
                "configuracion_identificadores",
                "grados",
                "instituciones",
                "jornadas",
                "matriculas",
                "periodos_matricula",
                "personas",
                "responsables",
                "schema_migrations",
                "secciones",
                "usuarios"
            };

            await using (var command = dataSource.CreateCommand("""
                select table_name
                from information_schema.tables
                where table_schema = 'public'
                order by table_name
                """))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                var tables = new List<string>();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }

                Assert.Equal(expectedTables, tables);
            }

            await using (var command = dataSource.CreateCommand("""
                select version, nombre, coalesce(checksum, 'NULL')
                from public.schema_migrations
                where version = 'baseline-001-fase1a'
                """))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal("baseline-001-fase1a", reader.GetString(0));
                Assert.Equal("schoolmanager_fase1a", reader.GetString(1));
                Assert.Equal("NULL", reader.GetString(2));
                Assert.False(await reader.ReadAsync());
            }
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "baseline", "001_schoolmanager_fase1a.sql")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("No se encontro el baseline desde el directorio de pruebas.");
    }
}
