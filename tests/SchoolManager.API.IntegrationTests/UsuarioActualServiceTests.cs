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
            FindRepositoryRoot(), "database", "baseline", "001_schoolmanager_fase1a.sql");
        var securityBootstrapPath = Path.Combine(
            FindRepositoryRoot(), "tests", "SchoolManager.Database.IntegrationTests",
            "Infrastructure", "SupabaseSecurityBootstrap.sql");
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
        if (_dataSource is not null) await _dataSource.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Rechaza_sub_ausente()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ObtenerAsync(CrearPrincipal()));
    }

    [Fact]
    public async Task Rechaza_sub_invalido()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ObtenerAsync(CrearPrincipal("no-es-un-uuid")));
    }

    [Fact]
    public async Task Rechaza_usuario_inexistente_como_identidad_no_vinculada()
    {
        var excepcion = await Assert.ThrowsAsync<IdentidadNoVinculadaException>(
            () => _service.ObtenerAsync(CrearPrincipal(Guid.NewGuid().ToString())));
        Assert.NotEqual(Guid.Empty, excepcion.AuthUserId);
    }

    [Fact]
    public async Task Rechaza_usuario_inactivo_con_excepcion_especifica()
    {
        var authUserId = Guid.NewGuid();
        var esperado = await InsertarUsuarioAsync(authUserId, ["padre"], activo: false);
        var excepcion = await Assert.ThrowsAsync<UsuarioInactivoException>(
            () => _service.ObtenerAsync(CrearPrincipal(authUserId.ToString())));
        Assert.Equal(esperado.Id, excepcion.UsuarioId);
    }

    [Fact]
    public async Task Resuelve_usuario_con_identidad_rol_y_permisos()
    {
        var authUserId = Guid.NewGuid();
        var esperado = await InsertarUsuarioAsync(authUserId, ["operador"], activo: true);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Equal(esperado.Id, actual.Id);
        Assert.Equal(esperado.PersonaId, actual.PersonaId);
        Assert.Equal("Usuario Prueba", actual.NombreCompleto);
        Assert.Equal(["operador"], actual.Roles);
        Assert.Contains("academico.alumnos.ver", actual.Permisos);
        Assert.Contains("academico.matriculas.crear", actual.Permisos);
        Assert.Equal(["operador"], actual.AmbitoGlobal.Roles);
        Assert.Contains("academico.alumnos.ver", actual.AmbitoGlobal.Permisos);
        Assert.Empty(actual.Instituciones);
        Assert.Empty(actual.InstitucionesAdministrables);
    }

    [Fact]
    public async Task Resuelve_usuario_multirol_y_combina_permisos_sin_duplicados()
    {
        var authUserId = Guid.NewGuid();
        var esperado = await InsertarUsuarioAsync(authUserId, ["consulta", "operador"], activo: true);
        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));
        Assert.Equal(esperado.Id, actual.Id);
        Assert.Equal(["consulta", "operador"], actual.Roles);
        Assert.Equal(actual.Permisos.Distinct(StringComparer.Ordinal), actual.Permisos);
    }

    [Fact]
    public async Task Separa_ambito_global_de_contexto_institucional_explicito()
    {
        var authUserId = Guid.NewGuid();
        var usuario = await InsertarUsuarioAsync(authUserId, ["consulta"], activo: true);
        var institucionId = await InsertarInstitucionAsync("Colegio Contexto", "CC");
        await AsignarRolInstitucionalAsync(usuario.Id, "operador", institucionId);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Equal(["consulta", "operador"], actual.Roles);
        Assert.Equal(["consulta"], actual.AmbitoGlobal.Roles);
        var institucion = Assert.Single(actual.Instituciones);
        Assert.Equal(institucionId, institucion.Id);
        Assert.Equal("Colegio Contexto", institucion.Nombre);
        Assert.Equal("CC", institucion.NombreCorto);
        Assert.Equal(["operador"], institucion.Roles);
        Assert.Contains("academico.alumnos.ver", institucion.Permisos);
        Assert.Empty(actual.InstitucionesAdministrables);
    }

    [Fact]
    public async Task Platform_admin_recibe_instituciones_administrables_sin_fabricar_membresias()
    {
        await CrearRolPlatformAdminAsync();
        var authUserId = Guid.NewGuid();
        await InsertarUsuarioAsync(authUserId, ["platform_admin"], activo: true);
        var activaA = await InsertarInstitucionAsync("Colegio Alfa", "A");
        var activaB = await InsertarInstitucionAsync("Colegio Beta", "B");
        await InsertarInstitucionAsync("Colegio Cerrado", "C", activo: false);

        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));

        Assert.Contains("platform_admin", actual.AmbitoGlobal.Roles);
        Assert.Empty(actual.Instituciones);
        Assert.Equal([activaA, activaB], actual.InstitucionesAdministrables.Select(i => i.Id).ToArray());
        Assert.All(actual.InstitucionesAdministrables, institucion =>
        {
            Assert.Empty(institucion.Roles);
            Assert.Empty(institucion.Permisos);
        });
    }

    [Fact]
    public async Task Institucion_inactiva_no_aporta_contexto_ni_permisos_efectivos()
    {
        var authUserId = Guid.NewGuid();
        var usuario = await InsertarUsuarioAsync(authUserId, [], activo: true);
        var institucionId = await InsertarInstitucionAsync("Institucion cerrada", activo: false);
        await AsignarRolInstitucionalAsync(usuario.Id, "operador", institucionId);
        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));
        Assert.Empty(actual.Roles);
        Assert.Empty(actual.Permisos);
        Assert.Empty(actual.AmbitoGlobal.Roles);
        Assert.Empty(actual.Instituciones);
    }

    [Fact]
    public async Task Rol_inactivo_no_retorna_rol_ni_concede_permisos()
    {
        var authUserId = Guid.NewGuid();
        await InsertarUsuarioAsync(authUserId, ["operador"], activo: true, rolActivo: false);
        var actual = await _service.ObtenerAsync(CrearPrincipal(authUserId.ToString()));
        Assert.Empty(actual.Roles);
        Assert.Empty(actual.Permisos);
        Assert.Empty(actual.AmbitoGlobal.Roles);
        Assert.Empty(actual.Instituciones);
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
                await using var deactivateCommand = _dataSource.CreateCommand(
                    "update public.roles set activo = false where codigo = $1");
                deactivateCommand.Parameters.AddWithValue(rol);
                await deactivateCommand.ExecuteNonQueryAsync();
            }
        }

        return new UsuarioActual(usuarioId, personaId, roles, []);
    }

    private async Task CrearRolPlatformAdminAsync()
    {
        await using var command = _dataSource.CreateCommand("""
            insert into public.roles(codigo, nombre, descripcion, es_sistema, activo)
            values ('platform_admin', 'Superadministrador', 'Prueba de plataforma', true, true)
            on conflict (codigo) do nothing
            """);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertarInstitucionAsync(
        string nombre,
        string? nombreCorto = null,
        bool activo = true)
    {
        var id = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand("""
            insert into public.instituciones (id, nombre, nombre_corto, activo)
            values ($1, $2, $3, $4)
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(nombre);
        command.Parameters.AddWithValue((object?)nombreCorto ?? DBNull.Value);
        command.Parameters.AddWithValue(activo);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task AsignarRolInstitucionalAsync(Guid usuarioId, string rol, Guid institucionId)
    {
        await using var command = _dataSource.CreateCommand("""
            insert into public.usuarios_roles (usuario_id, rol_id, institucion_id)
            select $1, id, $3 from public.roles where codigo = $2
            """);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(rol);
        command.Parameters.AddWithValue(institucionId);
        await command.ExecuteNonQueryAsync();
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
            if (File.Exists(Path.Combine(directory.FullName, "database", "baseline", "001_schoolmanager_fase1a.sql")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró el baseline Fase 1A.");
    }
}
