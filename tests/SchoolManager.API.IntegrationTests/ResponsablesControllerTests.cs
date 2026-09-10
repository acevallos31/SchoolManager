using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class ResponsablesControllerTests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public ResponsablesControllerTests(MatriculasApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => MatriculasApiFactory.AdminA.ToString();
    private string SubB => MatriculasApiFactory.AdminB.ToString();

    [Fact]
    public async Task Crear_responsable_devuelve_created_y_es_legible()
    {
        var institucion = _factory.InstitucionA;
        var cliente = _factory.CrearCliente(SubA);

        var crear = await cliente.PostAsync("/api/responsables", Body(new
        {
            institucionId = institucion,
            nombres = "Gloria",
            apellidos = "Paredes",
            tipoIdentificacion = "CI",
            numeroIdentificacion = $"9000-{Guid.NewGuid():N}"[..8],
            telefono = "0999888777",
            correo = "gloria@test.com"
        }));

        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        using var creado = JsonDocument.Parse(await crear.Content.ReadAsStringAsync());
        var id = creado.RootElement.GetProperty("id").GetGuid();

        var detalle = await cliente.GetAsync($"/api/responsables/{id}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        using var json = JsonDocument.Parse(await detalle.Content.ReadAsStringAsync());
        Assert.Equal("Gloria", json.RootElement.GetProperty("nombres").GetString());
        Assert.Equal("Paredes", json.RootElement.GetProperty("apellidos").GetString());
        Assert.Equal("activo", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Crear_responsable_duplicado_devuelve_409()
    {
        var institucion = _factory.InstitucionA;
        var cliente = _factory.CrearCliente(SubA);
        var documento = $"1710-{Guid.NewGuid():N}"[..8];

        var body = Body(new
        {
            institucionId = institucion,
            nombres = "Juan",
            apellidos = "Torres",
            tipoIdentificacion = "CI",
            numeroIdentificacion = documento
        });

        var primero = await cliente.PostAsync("/api/responsables", body);
        var duplicado = await cliente.PostAsync("/api/responsables", body);

        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Crear_con_datos_incompletos_devuelve_400()
    {
        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/responsables", Body(new { institucionId = _factory.InstitucionA, nombres = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editar_responsable_devuelve_no_content_y_actualiza()
    {
        var institucion = _factory.InstitucionA;
        var cliente = _factory.CrearCliente(SubA);
        var id = await CrearResponsableAsync(institucion);

        var editar = await cliente.PutAsync($"/api/responsables/{id}", Body(new
        {
            nombres = "Gloria",
            apellidos = "Paredes Vega",
            telefono = "0999111000",
            correo = "gm@test.com"
        }));

        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);

        var detalle = await cliente.GetAsync($"/api/responsables/{id}");
        using var json = JsonDocument.Parse(await detalle.Content.ReadAsStringAsync());
        Assert.Equal("Paredes Vega", json.RootElement.GetProperty("apellidos").GetString());
        Assert.Equal("gm@test.com", json.RootElement.GetProperty("correo").GetString());
    }

    [Fact]
    public async Task Desactivar_sin_motivo_devuelve_400()
    {
        var id = await CrearResponsableAsync(_factory.InstitucionA);
        var response = await _factory.CrearCliente(SubA)
            .PutAsync($"/api/responsables/{id}/estado", Body(new { motivo = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Desactivar_y_reactivar_responsable()
    {
        var institucion = _factory.InstitucionA;
        var cliente = _factory.CrearCliente(SubA);
        var id = await CrearResponsableAsync(institucion);

        var desactivar = await cliente.PutAsync($"/api/responsables/{id}/estado", Body(new { motivo = "Solicitud del padre" }));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var detalleInactivo = await cliente.GetAsync($"/api/responsables/{id}");
        using var jsonInactivo = JsonDocument.Parse(await detalleInactivo.Content.ReadAsStringAsync());
        Assert.Equal("inactivo", jsonInactivo.RootElement.GetProperty("estado").GetString());

        var reactivar = await cliente.PostAsync($"/api/responsables/{id}/reactivar", Body(new { }));
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var detalleActivo = await cliente.GetAsync($"/api/responsables/{id}");
        using var jsonActivo = JsonDocument.Parse(await detalleActivo.Content.ReadAsStringAsync());
        Assert.Equal("activo", jsonActivo.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Responsable_inexistente_devuelve_404()
    {
        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/responsables/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Vincular_responsable_a_alumno_y_listar_vinculos()
    {
        var institucion = _factory.InstitucionA;
        var cliente = _factory.CrearCliente(SubA);
        var alumno = await _factory.CrearAlumnoAsync(institucion);
        var responsableId = await CrearResponsableAsync(institucion);

        var vincular = await cliente.PostAsync($"/api/responsables/alumno/{alumno}", Body(new
        {
            responsableId,
            parentesco = "Padre",
            esPrincipal = true,
            accesoFinanciero = true
        }));
        Assert.Equal(HttpStatusCode.Created, vincular.StatusCode);

        var lista = await cliente.GetAsync($"/api/responsables/alumno/{alumno}");
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        using var json = JsonDocument.Parse(await lista.Content.ReadAsStringAsync());
        Assert.Single(json.RootElement.EnumerateArray());
        Assert.True(json.RootElement[0].GetProperty("esPrincipal").GetBoolean());
    }

    [Fact]
    public async Task Desactivar_vinculo_devuelve_no_content()
    {
        var institucion = _factory.InstitucionA;
        var cliente = _factory.CrearCliente(SubA);
        var alumno = await _factory.CrearAlumnoAsync(institucion);
        var responsableId = await CrearResponsableAsync(institucion);

        await cliente.PostAsync($"/api/responsables/alumno/{alumno}", Body(new { responsableId, parentesco = "Madre" }));
        var lista = await cliente.GetAsync($"/api/responsables/alumno/{alumno}");
        using var doc = JsonDocument.Parse(await lista.Content.ReadAsStringAsync());
        var vinculoId = doc.RootElement[0].GetProperty("id").GetGuid();

        var desactivar = await cliente.PutAsync($"/api/responsables/vinculo/{vinculoId}/desactivar", Body(new { motivo = "Traslado" }));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var relista = await cliente.GetAsync($"/api/responsables/alumno/{alumno}");
        using var json = JsonDocument.Parse(await relista.Content.ReadAsStringAsync());
        Assert.Equal("inactivo", json.RootElement[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Sin_permiso_devuelve_403()
    {
        var response = await _factory.CrearCliente("sin-permiso")
            .GetAsync($"/api/responsables/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listado_monoinstancia_devuelve_200_por_institucion()
    {
        var institucion = _factory.InstitucionA;
        var cliente = _factory.CrearCliente(SubA);
        await CrearResponsableAsync(institucion);

        var response = await cliente.GetAsync($"/api/responsables?institucionId={institucion}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("items", out _));
        Assert.True(json.RootElement.GetProperty("totalItems").GetInt64() >= 1);
    }

    [Fact]
    public async Task Listado_multi_sin_institucion_devuelve_400_y_no_500()
    {
        var response = await _factory.CrearCliente(SubA).GetAsync("/api/responsables");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminA_no_ve_responsables_de_institucion_B()
    {
        var institucionA = _factory.InstitucionA;
        var institucionB = _factory.InstitucionB;

        var idB = await CrearResponsableAsync(institucionB, _factory.CrearCliente(SubB));

        // AdminA (rol solo en A) no puede leer ni listar responsables de B.
        using var clienteA = _factory.CrearCliente(SubA);
        var detalleB = await clienteA.GetAsync($"/api/responsables/{idB}");
        Assert.Equal(HttpStatusCode.NotFound, detalleB.StatusCode);

        var listaB = await clienteA.GetAsync($"/api/responsables?institucionId={institucionB}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listaB.StatusCode);
        using var json = JsonDocument.Parse(await listaB.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());

        // AdminB (rol en B) sí lo ve.
        using var clienteB = _factory.CrearCliente(SubB);
        var detalleB2 = await clienteB.GetAsync($"/api/responsables/{idB}");
        Assert.Equal(HttpStatusCode.OK, detalleB2.StatusCode);

        // AdminA vé el suyo por su institución.
        var idA = await CrearResponsableAsync(institucionA);
        var detalleA = await clienteA.GetAsync($"/api/responsables/{idA}");
        Assert.Equal(HttpStatusCode.OK, detalleA.StatusCode);
    }

    private async Task<Guid> CrearResponsableAsync(Guid institucion)
    {
        var cliente = _factory.CrearCliente(SubA);
        return await CrearResponsableAsync(institucion, cliente);
    }

    private async Task<Guid> CrearResponsableAsync(Guid institucion, HttpClient cliente)
    {
        var response = await cliente.PostAsync("/api/responsables", Body(new
        {
            institucionId = institucion,
            nombres = "Responsable",
            apellidos = $"Prueba {Guid.NewGuid():N}"[..8],
            tipoIdentificacion = "CI",
            numeroIdentificacion = $"8001-{Guid.NewGuid():N}"[..8]
        }));
        var bodyRespuesta = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(bodyRespuesta);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");
}