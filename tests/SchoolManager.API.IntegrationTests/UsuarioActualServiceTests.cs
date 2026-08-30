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
        var securityBootstrapPath = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "SchoolManager.Database.IntegrationTests",
            "Infrastructure",
            "SupabaseSecurityBootstrap.sql"
        );
        await using (var securityCommand = _dataSource.CreateCommand(
            await File.ReadAllTextAsync(securityBootstrapPath)))
        {
            await securityCommand.ExecuteNonQueryAsync();
        }
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
        await InsertarUsuarioAsync(authUserId, ["padre"], activo: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()))
        );
    }

    [Fact]
    public async Task Resuelve_usuario_con_un_rol_y_sus_permisos()
    {
        var authUserId = Guid.NewGuid();
        var esperado = await InsertarUsuarioAsync(authUserId, ["operador"], activo: true);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Equal(esperado.Id, actual.Id);
        Assert.Equal(esperado.PersonaId, actual.PersonaId);
        Assert.Equal(["operador"], actual.Roles);
        Assert.Contains("academico.alumnos.ver", actual.Permisos);
        Assert.Contains("academico.matriculas.crear", actual.Permisos);
    }

    [Fact]
    public async Task Resuelve_usuario_multirol_y_combina_permisos_sin_duplicados()
    {
        var authUserId = Guid.NewGuid();
        var esperado = await InsertarUsuarioAsync(authUserId, ["consulta", "operador"], activo: true);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Equal(esperado.Id, actual.Id);
        Assert.Equal(esperado.PersonaId, actual.PersonaId);
        Assert.Equal(["consulta", "operador"], actual.Roles);
        Assert.Equal(actual.Permisos.Distinct(StringComparer.Ordinal), actual.Permisos);
        Assert.Contains("academico.alumnos.ver", actual.Permisos);
    }

    [Fact]
    public async Task Rol_inactivo_no_retorna_rol_ni_concede_permisos()
    {
        var authUserId = Guid.NewGuid();
        await InsertarUsuarioAsync(authUserId, ["operador"], activo: true, rolActivo: false);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Empty(actual.Roles);
        Assert.Empty(actual.Permisos);
    }

    private async Task<UsuarioActual> InsertarUsuarioAsync(
        Guid authUserId,
        IReadOnlyList<string> roles,
        bool activo,
        bool rolActivo = true)
    {
        var personaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        await using var command = _dataSource.CreateCommand("""
            with persona_insertada as (
              insert into public.personas (id, nombres, apellidos)
              values ($1, 'Usuario', 'Prueba')
              returning id
            )
            insert into public.usuarios (id, persona_id, auth_user_id, activo)
            select $2, id, $3, $4
            from persona_insertada;
            """);
        command.Parameters.AddWithValue(personaId);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(authUserId);
        command.Parameters.AddWithValue(activo);
        await command.ExecuteNonQueryAsync();

        foreach (var rol in roles)
        {
            await using var roleCommand = _dataSource.CreateCommand("""
                insert into public.usuarios_roles (usuario_id, rol_id)
                select $1, id from public.roles where codigo = $2;
                """);
            roleCommand.Parameters.AddWithValue(usuarioId);
            roleCommand.Parameters.AddWithValue(rol);
            await roleCommand.ExecuteNonQueryAsync();

            if (!rolActivo)
            {
                await using var deactivateCommand = _dataSource.CreateCommand("""
                    update public.roles set activo = false where codigo = $1
                    """);
                deactivateCommand.Parameters.AddWithValue(rol);
                await deactivateCommand.ExecuteNonQueryAsync();
            }
        }

        return new UsuarioActual(usuarioId, personaId, roles, []);
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
