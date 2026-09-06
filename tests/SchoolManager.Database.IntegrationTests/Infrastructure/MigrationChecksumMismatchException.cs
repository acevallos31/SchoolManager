namespace SchoolManager.Database.IntegrationTests.Infrastructure;

/// <summary>
/// Se lanza cuando una migracion ya aplicada tiene un checksum registrado que no
/// coincide con el hash SHA-256 del contenido actual del archivo en el repositorio.
/// Indica que una migracion fue modificada despues de aplicarse.
/// </summary>
public sealed class MigrationChecksumMismatchException : Exception
{
    public string Version { get; }
    public string MigrationPath { get; }
    public string StoredChecksum { get; }
    public string CurrentChecksum { get; }

    public MigrationChecksumMismatchException(
        string version,
        string migrationPath,
        string storedChecksum,
        string currentChecksum)
        : base(
            $"Migración modificada tras su aplicación: la versión {version} ya está registrada en " +
            $"schema_migrations con checksum {storedChecksum}, pero el contenido actual del archivo " +
            $"{Path.GetFileName(migrationPath)} produce {currentChecksum}. " +
            "Una migración aplicada no debe editarse; crea una nueva migración en su lugar.")
    {
        Version = version;
        MigrationPath = migrationPath;
        StoredChecksum = storedChecksum;
        CurrentChecksum = currentChecksum;
    }
}
