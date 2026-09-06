using System.Security.Cryptography;
using Npgsql;

namespace SchoolManager.Database.IntegrationTests.Infrastructure;

public static class MigrationRunner
{
    public static async Task ApplyActiveAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        foreach (var path in GetActiveMigrationPaths())
        {
            await ApplyOneAsync(dataSource, path, cancellationToken);
        }
    }

    /// <summary>Aplica las migraciones activas hasta la version indicada (inclusive).</summary>
    public static async Task ApplyUpToAsync(
        NpgsqlDataSource dataSource,
        string version,
        CancellationToken cancellationToken = default)
    {
        foreach (var path in GetActiveMigrationPaths())
        {
            var current = ExtractVersion(path);
            if (string.Compare(current, version, StringComparison.Ordinal) > 0)
            {
                break;
            }

            await ApplyOneAsync(dataSource, path, cancellationToken);
        }
    }

    private static async Task ApplyOneAsync(
        NpgsqlDataSource dataSource,
        string path,
        CancellationToken cancellationToken)
    {
        var checksum = ComputeChecksum(path);

        if (await IsAppliedAsync(dataSource, path, cancellationToken))
        {
            await VerifyOrBackfillChecksumAsync(dataSource, path, checksum, cancellationToken);
            return;
        }

        await ExecuteFileAsync(dataSource, path, cancellationToken);
        await RecordChecksumAsync(dataSource, path, checksum, cancellationToken);
    }

    public static async Task RevertActiveAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        foreach (var path in GetActiveMigrationPaths().Reverse().Select(GetRollbackPath))
        {
            await ExecuteFileAsync(dataSource, path, cancellationToken);
        }
    }

    public static async Task RevertAsync(NpgsqlDataSource dataSource, string version, CancellationToken cancellationToken = default)
    {
        await ExecuteFileAsync(dataSource, GetRollbackPath(GetActiveMigrationPaths()
            .Single(path => Path.GetFileName(path).StartsWith(version + "_", StringComparison.Ordinal))), cancellationToken);
    }

    public static IReadOnlyList<string> GetActiveMigrationPaths()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "database", "migrations");
        return Directory.EnumerateFiles(directory, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Devuelve las rutas de los archivos de validacion de migraciones aplicadas.</summary>
    public static IReadOnlyList<string> GetActiveValidationPaths()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "database", "migrations", "validation");
        return Directory.EnumerateFiles(directory, "*.validation.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Hash SHA-256 (hex, minusculas) del contenido literal de una migracion.</summary>
    public static string ComputeChecksum(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static async Task VerifyOrBackfillChecksumAsync(
        NpgsqlDataSource dataSource,
        string path,
        string checksum,
        CancellationToken cancellationToken)
    {
        var version = ExtractVersion(path);
        var stored = await ReadStoredChecksumAsync(dataSource, version, cancellationToken);

        // Fila historica sin checksum (instalacion previa a este mecanismo):
        // se retro-rellena con el hash actual para no bloquear la instalacion y
        // dejar verificacion hacia adelante. No falla.
        if (stored is null)
        {
            await UpdateChecksumAsync(dataSource, version, checksum, cancellationToken);
            return;
        }

        if (!string.Equals(stored, checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationChecksumMismatchException(version, path, stored, checksum);
        }
    }

    private static async Task RecordChecksumAsync(
        NpgsqlDataSource dataSource,
        string path,
        string checksum,
        CancellationToken cancellationToken)
    {
        // Solo se actualiza una fila YA existente. Las migraciones 001-006 no
        // auto-registran su version en schema_migrations (y sus rollbacks no tocan
        // la tabla); insertar aqui una fila nueva romperia la simetria rollback/reapply.
        var version = ExtractVersion(path);
        await UpdateChecksumAsync(dataSource, version, checksum, cancellationToken);
    }

    private static async Task<string?> ReadStoredChecksumAsync(
        NpgsqlDataSource dataSource,
        string version,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "select checksum from public.schema_migrations where version = $1");
        command.Parameters.AddWithValue(version);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull ? null : (string)value;
    }

    private static async Task UpdateChecksumAsync(
        NpgsqlDataSource dataSource,
        string version,
        string checksum,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "update public.schema_migrations set checksum = $2 where version = $1");
        command.Parameters.AddWithValue(version);
        command.Parameters.AddWithValue(checksum);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ExtractVersion(string path) =>
        Path.GetFileName(path).Split('_', 2)[0];


    private static async Task ExecuteFileAsync(NpgsqlDataSource dataSource, string path, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(File.ReadAllText(path));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(
        NpgsqlDataSource dataSource,
        string path,
        CancellationToken cancellationToken)
    {
        var version = Path.GetFileName(path).Split('_', 2)[0];
        await using (var existenceCommand = dataSource.CreateCommand(
            "select to_regclass('public.schema_migrations') is not null"))
        {
            if (!(bool)(await existenceCommand.ExecuteScalarAsync(cancellationToken))!)
            {
                return false;
            }
        }

        await using var command = dataSource.CreateCommand(
            "select exists (select 1 from public.schema_migrations where version = $1)");
        command.Parameters.AddWithValue(version);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static string GetRollbackPath(string migrationPath) => Path.Combine(
        Path.GetDirectoryName(migrationPath)!,
        "rollback",
        Path.GetFileNameWithoutExtension(migrationPath) + ".rollback.sql");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "database", "migrations")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("No se encontro database/migrations desde el directorio de pruebas.");
    }
}
