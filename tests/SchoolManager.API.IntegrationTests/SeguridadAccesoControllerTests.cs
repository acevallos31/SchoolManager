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
}
