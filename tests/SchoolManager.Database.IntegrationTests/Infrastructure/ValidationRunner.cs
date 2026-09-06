using Npgsql;
using Testcontainers.PostgreSql;

namespace SchoolManager.Database.IntegrationTests.Infrastructure;

/// <summary>
/// Ejecuta las validaciones de migraciones de forma incremental sobre un
/// PostgreSQL desechable limpio: aplica la secuencia 001..N y, tras aplicar cada
/// migracion, ejecuta su archivo database/migrations/validation/NNN_*.validation.sql.
///
/// Contrato: una validacion PASSA si y solo si ejecutar el archivo no lanza error
/// SQL y ninguna de sus consultas devuelve filas. Cualquier fila es un hallazgo.
///
/// Las validaciones NO son autocontenidas contra el esquema final: cada una valida
/// el estado inmediatamente posterior a su propia migracion (p. ej. la 006 usa
/// matriculas.grado_id que la 008 normaliza; la 007 usa usuarios.rol que la 010
/// retira). Por eso se ejecutan incrementalmente y sobre una base limpia, no en el
/// fixture compartido (que ademas inyecta datos legacy para probar rollbacks).
/// </summary>
public static class ValidationRunner
{
    /// <summary>
    /// Crea un Postgres limpio, aplica el bootstrap base (como el fixture), las
    /// migraciones incrementalmente y valida cada una. Lanza
    /// <see cref="ValidationFailedException"/> si alguna falla.
    /// </summary>
    public static async Task RunAllAsync(CancellationToken cancellationToken = default)
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("schoolmanager_validation")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        try
        {
            await container.StartAsync(cancellationToken);
            await using var dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
            await RunAllAsync(dataSource, cancellationToken);
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    /// <summary>Ejecuta validaciones incrementales contra un dataSource dado.</summary>
    public static async Task RunAllAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        await ExecuteBootstrapAsync(dataSource, "SupabaseSecurityBootstrap.sql", cancellationToken);
        await ExecuteBootstrapAsync(dataSource, "LegacySchemaBootstrap.sql", cancellationToken);

        var failures = new List<ValidationFailure>();

        foreach (var migration in MigrationRunner.GetActiveMigrationPaths())
        {
            var version = ExtractVersion(migration);
            await MigrationRunner.ApplyUpToAsync(dataSource, version, cancellationToken);

            var validationPath = MigrationRunner.GetActiveValidationPaths()
                .FirstOrDefault(p => ExtractVersion(p) == version);

            if (validationPath is not null &&
                await TryRunFileAsync(dataSource, validationPath, cancellationToken) is { } failure)
            {
                failures.Add(failure);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationFailedException(failures);
        }
    }

    private static async Task ExecuteBootstrapAsync(
        NpgsqlDataSource dataSource,
        string fileName,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Infrastructure", fileName);
        await using var command = dataSource.CreateCommand(await File.ReadAllTextAsync(path, cancellationToken));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ValidationFailure?> TryRunFileAsync(
        NpgsqlDataSource dataSource,
        string path,
        CancellationToken cancellationToken)
    {
        var file = Path.GetFileName(path);

        await using (var command = dataSource.CreateCommand(await File.ReadAllTextAsync(path, cancellationToken)))
        {
            try
            {
                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    do
                    {
                        if (reader.FieldCount > 0)
                        {
                            var count = 0;
                            while (await reader.ReadAsync(cancellationToken))
                            {
                                count++;
                            }

                            if (count > 0)
                            {
                                return new ValidationFailure(file, $"{count} fila(s) devuelta(s) por la consulta {DescribeResult(reader)}");
                            }
                        }
                    } while (await reader.NextResultAsync(cancellationToken));
                }
            }
            catch (PostgresException ex)
            {
                return new ValidationFailure(file, $"error SQL: {ex.MessageText}");
            }
        }

        return null;
    }

    private static string DescribeResult(NpgsqlDataReader reader)
    {
        try
        {
            return string.Join(", ", Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i)));
        }
        catch
        {
            return "(sin columnas)";
        }
    }

    private static string ExtractVersion(string path) =>
        Path.GetFileName(path).Split('_', 2)[0];
}

/// <summary>Una validacion individual que no paso.</summary>
public sealed record ValidationFailure(string FileName, string Reason);

/// <summary>Se lanza cuando una o mas validaciones fallan.</summary>
public sealed class ValidationFailedException : Exception
{
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public ValidationFailedException(IReadOnlyList<ValidationFailure> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures;
    }

    private static string BuildMessage(IReadOnlyList<ValidationFailure> failures) =>
        "Fallaron validaciones de migraciones:\n" +
        string.Join("\n", failures.Select(f => $"  - {f.FileName}: {f.Reason}"));
}
