using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class InputDtoUnderPostingTests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public InputDtoUnderPostingTests(MatriculasApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Cliente() =>
        _factory.CrearCliente(MatriculasApiFactory.AdminA.ToString());

    [Fact]
    public async Task Matricula_con_campos_guid_omitidos_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/matriculas", Body(new { alumnoId = Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Alumno_sin_institucion_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/alumnos", Body(new
        {
            nombres = "Prueba",
            apellidos = "UnderPosting",
            tipoIdentificacion = "CI",
            numeroIdentificacion = $"UP-{Guid.NewGuid():N}"[..10]
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Responsable_sin_institucion_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/responsables", Body(new
        {
            nombres = "Prueba",
            apellidos = "UnderPosting",
            tipoIdentificacion = "CI",
            numeroIdentificacion = $"UP-{Guid.NewGuid():N}"[..10]
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Concepto_sin_monto_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/conceptosfinancieros", Body(new { nombre = "Prueba" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Pago_sin_monto_total_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync(
            $"/api/pagos/alumno/{Guid.NewGuid()}",
            Body(new { aplicaciones = Array.Empty<object>() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Grado_sin_orden_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/estructura-academica/grados", Body(new { nombre = "Prueba" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Asignar_plan_sin_plan_id_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync(
            $"/api/cargos/matricula/{Guid.NewGuid()}/plan",
            Body(new { }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("error", out var error));
        Assert.False(string.IsNullOrWhiteSpace(error.GetString()));
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");
}
