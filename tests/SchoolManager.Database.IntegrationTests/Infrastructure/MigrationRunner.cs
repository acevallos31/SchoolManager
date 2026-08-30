using Npgsql;

namespace SchoolManager.Database.IntegrationTests.Infrastructure;

public static class MigrationRunner
{
    public static async Task ApplyActiveAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        foreach (var path in GetActiveMigrationPaths())
        {
            if (await IsAppliedAsync(dataSource, path, cancellationToken))
            {
                continue;
            }

            await ExecuteFileAsync(dataSource, path, cancellationToken);
        }
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
