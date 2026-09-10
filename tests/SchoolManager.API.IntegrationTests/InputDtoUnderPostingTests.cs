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
    public async Task Matricula_con_guid_vacio_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/matriculas", Body(new
        {
            alumnoId = Guid.Empty,
            seccionId = Guid.NewGuid(),
            periodoMatriculaId = Guid.NewGuid()
        }));

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
    public async Task Alumno_con_institucion_vacia_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/alumnos", Body(new
        {
            institucionId = Guid.Empty,
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
    public async Task Responsable_con_institucion_vacia_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync("/api/responsables", Body(new
        {
            institucionId = Guid.Empty,
            nombres = "Prueba",
            apellidos = "UnderPosting",
            tipoIdentificacion = "CI",
            numeroIdentificacion = $"UP-{Guid.NewGuid():N}"[..10]
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Responsable_para_persona_con_ids_invalidos_devuelve_400()
    {
        using var client = Cliente();

        var sinPersona = await client.PostAsync("/api/responsables/para-persona", Body(new
        {
            institucionId = Guid.NewGuid()
        }));
        Assert.Equal(HttpStatusCode.BadRequest, sinPersona.StatusCode);
        await AssertErrorAsync(sinPersona);

        var sinInstitucion = await client.PostAsync("/api/responsables/para-persona", Body(new
        {
            personaId = Guid.NewGuid(),
            institucionId = Guid.Empty
        }));
        Assert.Equal(HttpStatusCode.BadRequest, sinInstitucion.StatusCode);
        await AssertErrorAsync(sinInstitucion);
    }

    [Fact]
    public async Task Vinculo_sin_responsable_o_con_guid_vacio_devuelve_400()
    {
        using var client = Cliente();
        var alumnoId = Guid.NewGuid();

        var omitido = await client.PostAsync($"/api/responsables/alumno/{alumnoId}", Body(new { }));
        Assert.Equal(HttpStatusCode.BadRequest, omitido.StatusCode);
        await AssertErrorAsync(omitido);

        var vacio = await client.PostAsync($"/api/responsables/alumno/{alumnoId}", Body(new
        {
            responsableId = Guid.Empty
        }));
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
        await AssertErrorAsync(vacio);
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
    public async Task Concepto_update_sin_monto_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PutAsync(
            $"/api/conceptosfinancieros/{Guid.NewGuid()}",
            Body(new { nombre = "Prueba" }));

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
    public async Task Pago_con_monto_cero_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync(
            $"/api/pagos/alumno/{Guid.NewGuid()}",
            Body(new { montoTotal = 0m, aplicaciones = Array.Empty<object>() }));

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
    public async Task Grado_update_sin_orden_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PutAsync(
            $"/api/estructura-academica/grados/{Guid.NewGuid()}",
            Body(new { nombre = "Prueba" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response);
    }

    [Fact]
    public async Task Seccion_sin_ids_obligatorios_devuelve_400()
    {
        using var client = Cliente();

        var sinCiclo = await client.PostAsync("/api/estructura-academica/secciones", Body(new
        {
            nombre = "A",
            gradoId = Guid.NewGuid()
        }));
        Assert.Equal(HttpStatusCode.BadRequest, sinCiclo.StatusCode);
        await AssertErrorAsync(sinCiclo);

        var sinGrado = await client.PostAsync("/api/estructura-academica/secciones", Body(new
        {
            nombre = "A",
            cicloId = Guid.NewGuid(),
            gradoId = Guid.Empty
        }));
        Assert.Equal(HttpStatusCode.BadRequest, sinGrado.StatusCode);
        await AssertErrorAsync(sinGrado);
    }

    [Fact]
    public async Task Seccion_update_sin_ids_obligatorios_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PutAsync(
            $"/api/estructura-academica/secciones/{Guid.NewGuid()}",
            Body(new { nombre = "A" }));

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

    [Fact]
    public async Task Asignar_plan_con_guid_vacio_devuelve_400()
    {
        using var client = Cliente();
        var response = await client.PostAsync(
            $"/api/cargos/matricula/{Guid.NewGuid()}/plan",
            Body(new { planPagoId = Guid.Empty }));

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
