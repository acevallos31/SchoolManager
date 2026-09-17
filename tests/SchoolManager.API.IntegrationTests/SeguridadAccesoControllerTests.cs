using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class SeguridadAccesoControllerTests(SeguridadAccesoApiFactory factory)
    : IClassFixture<SeguridadAccesoApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.PrepararAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SinSesion_Devuelve401()
    {
        using var client = factory.Cliente();
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad?institucionId={factory.InstitucionA}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Administrador_explicito_consulta_snapshot_de_su_institucion()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad?institucionId={factory.InstitucionA}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(factory.InstitucionA, json.GetProperty("institucionId").GetGuid());
        Assert.True(json.GetProperty("capacidades").GetProperty("rolesVer").GetBoolean());
        Assert.True(json.GetProperty("capacidades").GetProperty("usuariosVer").GetBoolean());
        Assert.Equal(JsonValueKind.Array, json.GetProperty("roles").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.GetProperty("plantillas").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.GetProperty("permisosDelegables").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.GetProperty("asignaciones").ValueKind);
    }

    [Fact]
    public async Task Administrador_explicito_lista_solo_usuarios_con_acceso_a_su_institucion()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad/usuarios?institucionId={factory.InstitucionA}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var usuarios = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, usuarios.ValueKind);
        Assert.NotEmpty(usuarios.EnumerateArray());
        Assert.DoesNotContain(usuarios.EnumerateArray(),
            usuario => usuario.GetProperty("id").GetGuid() == factory.UsuarioDestinoId);
        Assert.All(usuarios.EnumerateArray(), usuario =>
        {
            Assert.Equal(JsonValueKind.Array, usuario.GetProperty("roles").ValueKind);
            Assert.True(usuario.GetProperty("identidadVinculada").GetBoolean());
            Assert.True(usuario.GetProperty("puedeEditar").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(usuario.GetProperty("nombres").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(usuario.GetProperty("apellidos").GetString()));
        });
    }

    [Fact]
    public async Task Superadministrador_lista_directorio_global_para_gestionar_accesos()
    {
        using var client = factory.Cliente(factory.Superadmin);
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad/usuarios?institucionId={factory.InstitucionA}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var usuarios = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(usuarios.EnumerateArray(),
            usuario => usuario.GetProperty("id").GetGuid() == factory.UsuarioDestinoId);
    }

    [Fact]
    public async Task Administrador_edita_su_persona_interna_sin_modificar_identidad_externa()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var usuarios = await client.GetFromJsonAsync<JsonElement>(
            $"/api/configuracion/seguridad/usuarios?institucionId={factory.InstitucionA}");
        var usuario = usuarios.EnumerateArray().First();
        var usuarioId = usuario.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync(
            $"/api/configuracion/seguridad/usuarios/{usuarioId}",
            new
            {
                institucionId = factory.InstitucionA,
                nombres = " Ana María ",
                apellidos = " Pérez López ",
                correo = " ANA.NUEVA@EXAMPLE.COM "
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var actualizado = await client.GetFromJsonAsync<JsonElement>(
            $"/api/configuracion/seguridad/usuarios?institucionId={factory.InstitucionA}");
        var item = actualizado.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == usuarioId);
        Assert.Equal("Ana María", item.GetProperty("nombres").GetString());
        Assert.Equal("Pérez López", item.GetProperty("apellidos").GetString());
        Assert.Equal("ana.nueva@example.com", item.GetProperty("correo").GetString());
        Assert.True(item.GetProperty("identidadVinculada").GetBoolean());
    }

    [Fact]
    public async Task EditarUsuario_valida_campos_permiso_y_contexto()
    {
        using var admin = factory.Cliente(factory.AdministradorA);
        var usuarios = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/configuracion/seguridad/usuarios?institucionId={factory.InstitucionA}");
        var usuarioId = usuarios.EnumerateArray().First().GetProperty("id").GetGuid();

        var invalido = await admin.PutAsJsonAsync(
            $"/api/configuracion/seguridad/usuarios/{usuarioId}",
            new { institucionId = factory.InstitucionA, nombres = " ", apellidos = "Pérez", correo = "ana@example.com" });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var ajeno = await admin.PutAsJsonAsync(
            $"/api/configuracion/seguridad/usuarios/{usuarioId}",
            new { institucionId = factory.InstitucionB, nombres = "Ana", apellidos = "Pérez", correo = "ana@example.com" });
        Assert.Equal(HttpStatusCode.Forbidden, ajeno.StatusCode);

        var inexistente = await admin.PutAsJsonAsync(
            $"/api/configuracion/seguridad/usuarios/{Guid.NewGuid()}",
            new { institucionId = factory.InstitucionA, nombres = "Ana", apellidos = "Pérez", correo = "" });
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        using var sinPermiso = factory.Cliente(factory.SinPermisos);
        var prohibido = await sinPermiso.PutAsJsonAsync(
            $"/api/configuracion/seguridad/usuarios/{usuarioId}",
            new { institucionId = factory.InstitucionA, nombres = "Ana", apellidos = "Pérez", correo = "ana@example.com" });
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
    }

    [Fact]
    public async Task Admin_global_legacy_no_puede_consultar_directorio_de_usuarios()
    {
        using var client = factory.Cliente(factory.AdminGlobal);
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad/usuarios?institucionId={factory.InstitucionA}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_global_legacy_no_puede_usar_autoridad_institucional_implicita()
    {
        using var client = factory.Cliente(factory.AdminGlobal);
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad?institucionId={factory.InstitucionA}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrador_de_A_no_puede_consultar_B()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad?institucionId={factory.InstitucionB}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Usuario_sin_permisos_Devuelve403()
    {
        using var client = factory.Cliente(factory.SinPermisos);
        var response = await client.GetAsync(
            $"/api/configuracion/seguridad?institucionId={factory.InstitucionA}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrador_prepara_invitacion_idempotente_por_API()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var correo = $"invitado.{Guid.NewGuid():N}@schoolmanager.test";
        var solicitud = new
        {
            institucionId = factory.InstitucionA,
            nombres = "María",
            apellidos = "Invitada",
            correo,
            rolId = factory.RolDestinoA,
            origen = "administracion"
        };

        var primeraRespuesta = await client.PostAsJsonAsync(
            "/api/configuracion/seguridad/usuarios/invitaciones/preparar", solicitud);
        var segundaRespuesta = await client.PostAsJsonAsync(
            "/api/configuracion/seguridad/usuarios/invitaciones/preparar", solicitud);

        Assert.Equal(HttpStatusCode.OK, primeraRespuesta.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segundaRespuesta.StatusCode);
        var primera = await primeraRespuesta.Content.ReadFromJsonAsync<JsonElement>();
        var segunda = await segundaRespuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pendiente", primera.GetProperty("estado").GetString());
        Assert.Equal(correo, primera.GetProperty("correo").GetString());
        Assert.True(primera.GetProperty("personaCreada").GetBoolean());
        Assert.True(primera.GetProperty("usuarioCreado").GetBoolean());
        Assert.True(primera.GetProperty("asignacionCreada").GetBoolean());
        Assert.True(primera.GetProperty("invitacionCreada").GetBoolean());
        Assert.False(segunda.GetProperty("personaCreada").GetBoolean());
        Assert.False(segunda.GetProperty("usuarioCreado").GetBoolean());
        Assert.False(segunda.GetProperty("asignacionCreada").GetBoolean());
        Assert.False(segunda.GetProperty("invitacionCreada").GetBoolean());
        Assert.Equal(
            primera.GetProperty("usuarioId").GetGuid(),
            segunda.GetProperty("usuarioId").GetGuid());
        Assert.Equal(
            primera.GetProperty("invitacionId").GetGuid(),
            segunda.GetProperty("invitacionId").GetGuid());
    }

    [Fact]
    public async Task Administrador_no_prepara_invitacion_en_otra_institucion()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var response = await client.PostAsJsonAsync(
            "/api/configuracion/seguridad/usuarios/invitaciones/preparar", new
            {
                institucionId = factory.InstitucionB,
                nombres = "Usuario",
                apellidos = "Ajeno",
                correo = $"ajeno.{Guid.NewGuid():N}@schoolmanager.test",
                rolId = factory.RolDestinoA,
                origen = "administracion"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Preparar_invitacion_con_correo_invalido_Devuelve400()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var response = await client.PostAsJsonAsync(
            "/api/configuracion/seguridad/usuarios/invitaciones/preparar", new
            {
                institucionId = factory.InstitucionA,
                nombres = "Usuario",
                apellidos = "Invalido",
                correo = "correo-invalido",
                rolId = factory.RolDestinoA,
                origen = "administracion"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Administrador_explicito_gestiona_rol_y_asignacion_de_extremo_a_extremo()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var codigo = $"secretaria_{Guid.NewGuid():N}";

        var crear = await client.PostAsJsonAsync("/api/configuracion/seguridad/roles", new
        {
            institucionId = factory.InstitucionA,
            codigo,
            nombre = "Secretaría",
            descripcion = "Rol de prueba"
        });
        Assert.Equal(HttpStatusCode.OK, crear.StatusCode);
        var rolId = (await crear.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var editar = await client.PutAsJsonAsync(
            $"/api/configuracion/seguridad/roles/{rolId}",
            new { nombre = "Secretaría académica", descripcion = "Actualizado" });
        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);

        var permisos = await client.PutAsJsonAsync(
            $"/api/configuracion/seguridad/roles/{rolId}/permisos",
            new { permisos = new[] { "identidad.roles.ver" } });
        Assert.Equal(HttpStatusCode.NoContent, permisos.StatusCode);

        var asignar = await client.PostAsJsonAsync(
            $"/api/configuracion/seguridad/roles/{rolId}/asignaciones",
            new { usuarioId = factory.UsuarioDestinoId });
        Assert.Equal(HttpStatusCode.OK, asignar.StatusCode);
        var asignacionId = (await asignar.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var snapshot = await client.GetFromJsonAsync<JsonElement>(
            $"/api/configuracion/seguridad?institucionId={factory.InstitucionA}");
        Assert.Contains(snapshot.GetProperty("roles").EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == rolId
                 && r.GetProperty("nombre").GetString() == "Secretaría académica");
        Assert.Contains(snapshot.GetProperty("asignaciones").EnumerateArray(),
            a => a.GetProperty("id").GetGuid() == asignacionId);

        var retirar = await client.PostAsJsonAsync(
            $"/api/configuracion/seguridad/asignaciones/{asignacionId}/desactivar",
            new { motivo = "Fin de prueba" });
        Assert.Equal(HttpStatusCode.NoContent, retirar.StatusCode);

        var desactivar = await client.PostAsJsonAsync(
            $"/api/configuracion/seguridad/roles/{rolId}/desactivar",
            new { motivo = "Fin de prueba" });
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);
    }

    [Fact]
    public async Task Admin_global_legacy_no_puede_crear_rol_institucional_por_API()
    {
        using var client = factory.Cliente(factory.AdminGlobal);
        var response = await client.PostAsJsonAsync("/api/configuracion/seguridad/roles", new
        {
            institucionId = factory.InstitucionA,
            codigo = $"bloqueado_{Guid.NewGuid():N}",
            nombre = "Bloqueado"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrador_de_A_no_puede_crear_rol_en_B()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var response = await client.PostAsJsonAsync("/api/configuracion/seguridad/roles", new
        {
            institucionId = factory.InstitucionB,
            codigo = $"ajeno_{Guid.NewGuid():N}",
            nombre = "Ajeno"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrearRol_con_nombre_vacio_Devuelve400_por_contrato_API()
    {
        using var client = factory.Cliente(factory.AdministradorA);
        var response = await client.PostAsJsonAsync("/api/configuracion/seguridad/roles", new
        {
            institucionId = factory.InstitucionA,
            codigo = $"invalido_{Guid.NewGuid():N}",
            nombre = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
