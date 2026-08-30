using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("schoolmanager_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        DataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
        await ExecuteScriptAsync("SupabaseSecurityBootstrap.sql");
        await ExecuteScriptAsync("LegacySchemaBootstrap.sql");
        await using (var legacyUser = DataSource.CreateCommand("""
            insert into public.usuarios (usuario, rol)
            values ('fixture-backfill-007', 'padre')
            """))
        {
            await legacyUser.ExecuteNonQueryAsync();
        }
        await MigrationRunner.ApplyActiveAsync(DataSource);
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    private async Task ExecuteScriptAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Infrastructure", fileName);
        await using var command = DataSource.CreateCommand(await File.ReadAllTextAsync(path));
        await command.ExecuteNonQueryAsync();
    }
}
