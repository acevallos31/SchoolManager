using System.Security.Claims;
using Npgsql;
using SchoolManager.API.Identity;
using Testcontainers.PostgreSql;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class UsuarioActualServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("schoolmanager_api_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private NpgsqlDataSource _dataSource = null!;
    private UsuarioActualService _service = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
        _service = new UsuarioActualService(_dataSource);

        var baselinePath = Path.Combine(
            FindRepositoryRoot(),
            "database",
            "baseline",
            "001_schoolmanager_fase1a.sql"
        );
        await using var command = _dataSource.CreateCommand(await File.ReadAllTextAsync(baselinePath));
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Rechaza_sub_ausente()
    {
        var principal = CrearPrincipal();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ObtenerAsync(principal)
        );
    }

    [Fact]
    public async Task Rechaza_sub_invalido()
    {
        var principal = CrearPrincipal("no-es-un-uuid");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ObtenerAsync(principal)
        );
    }

    [Fact]
    public async Task Rechaza_usuario_inexistente()
    {
        var principal = CrearPrincipal(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ObtenerAsync(principal)
        );
    }

    [Fact]
    public async Task Rechaza_usuario_inactivo()
    {
        var authUserId = Guid.NewGuid();
        await InsertarUsuarioAsync(authUserId, "padre", activo: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()))
        );
    }

    [Fact]
    public async Task Resuelve_usuario_admin_activo()
    {
        var authUserId = Guid.NewGuid();
        var esperado = await InsertarUsuarioAsync(authUserId, "admin", activo: true);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Equal(esperado.Id, actual.Id);
        Assert.Equal(esperado.PersonaId, actual.PersonaId);
        Assert.Equal("admin", actual.Rol);
    }

    [Fact]
    public async Task Resuelve_usuario_padre_activo()
    {
        var authUserId = Guid.NewGuid();
        var esperado = await InsertarUsuarioAsync(authUserId, "padre", activo: true);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Equal(esperado.Id, actual.Id);
        Assert.Equal(esperado.PersonaId, actual.PersonaId);
        Assert.Equal("padre", actual.Rol);
    }

    private async Task<UsuarioActual> InsertarUsuarioAsync(Guid authUserId, string rol, bool activo)
    {
        var personaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        await using var command = _dataSource.CreateCommand("""
            with persona_insertada as (
              insert into public.personas (id, nombres, apellidos)
              values ($1, 'Usuario', 'Prueba')
              returning id
            )
            insert into public.usuarios (id, persona_id, auth_user_id, rol, activo)
            select $2, id, $3, $4, $5
            from persona_insertada;
            """);
        command.Parameters.AddWithValue(personaId);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(authUserId);
        command.Parameters.AddWithValue(rol);
        command.Parameters.AddWithValue(activo);
        await command.ExecuteNonQueryAsync();

        return new UsuarioActual(usuarioId, personaId, rol);
    }

    private static ClaimsPrincipal CrearPrincipal(string? sub = null)
    {
        var claims = sub is null ? [] : new[] { new Claim("sub", sub) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "database",
                    "baseline",
                    "001_schoolmanager_fase1a.sql"
                )))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró el baseline Fase 1A.");
    }
}
