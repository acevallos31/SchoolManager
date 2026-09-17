using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class ResponsablesAccesoControllerTests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public ResponsablesAccesoControllerTests(MatriculasApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preparar_invitacion_responsable_devuelve_pendiente_y_reutiliza_persona()
    {
        var cliente = _factory.CrearCliente(MatriculasApiFactory.AdminA.ToString());
        var correo = $"portal.{Guid.NewGuid():N}@schoolmanager.test";
        var crear = await cliente.PostAsync("/api/responsables", Body(new
        {
            institucionId = _factory.InstitucionA,
            nombres = "Portal",
            apellidos = "Responsable",
            tipoIdentificacion = "CI",
            numeroIdentificacion = $"R-{Guid.NewGuid():N}"[..16],
            correo
        }));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        using var creado = JsonDocument.Parse(await crear.Content.ReadAsStringAsync());
        var responsableId = creado.RootElement.GetProperty("id").GetGuid();

        var detalle = await cliente.GetAsync($"/api/responsables/{responsableId}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        using var detalleJson = JsonDocument.Parse(await detalle.Content.ReadAsStringAsync());
        var personaId = detalleJson.RootElement.GetProperty("personaId").GetGuid();

        var response = await cliente.PostAsync(
            $"/api/responsables/{responsableId}/invitacion-acceso/preparar", Body(new { }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(responsableId, json.RootElement.GetProperty("responsableId").GetGuid());
        Assert.Equal(personaId, json.RootElement.GetProperty("personaId").GetGuid());
        Assert.Equal("pendiente", json.RootElement.GetProperty("estado").GetString());
        Assert.Equal(correo, json.RootElement.GetProperty("correo").GetString());
        Assert.True(json.RootElement.GetProperty("invitacionCreada").GetBoolean());

        var segunda = await cliente.PostAsync(
            $"/api/responsables/{responsableId}/invitacion-acceso/preparar", Body(new { }));
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        using var json2 = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        Assert.False(json2.RootElement.GetProperty("invitacionCreada").GetBoolean());
        Assert.Equal(
            json.RootElement.GetProperty("invitacionId").GetGuid(),
            json2.RootElement.GetProperty("invitacionId").GetGuid());
    }

    [Fact]
    public async Task Preparar_invitacion_responsable_sin_autenticacion_devuelve_401()
    {
        var cliente = _factory.CrearCliente();
        var response = await cliente.PostAsync(
            $"/api/responsables/{Guid.NewGuid()}/invitacion-acceso/preparar", Body(new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
