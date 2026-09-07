using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

// Tests de integración de CiclosEscolaresController contra Postgres real
// (Testcontainers). Usan el fixture mono-institucion (CiclosEscolaresApiFactory)
// porque los RPC de periodos de matricula (014) resuelven su ambito con
// resolver_institucion_operacion(NULL), valido solo en modo mono (produccion).
//
// Autorización en dos capas: la policy .NET exige academico.ciclos.* (se valida
// ANTES de tocar DB); la RPC revalida configuracion.ciclos.* / periodos_matricula.*
// contra el rol del usuario (admin, sembrado por el fixture). Cubren listar,
// crear, actualizar (preserva activo), desactivar, reactivar para ciclos y
// periodos, más 400/404/401/403.
public sealed class CiclosEscolaresControllerTests : IClassFixture<CiclosEscolaresApiFactory>
{
    private readonly CiclosEscolaresApiFactory _factory;

    public CiclosEscolaresControllerTests(CiclosEscolaresApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => CiclosEscolaresApiFactory.AdminA.ToString();

    // ----- Ciclos -----

    [Fact]
    public async Task Listar_ciclos_autorizado_devuelve_los_sembrados()
    {
        var ciclo = await _factory.CrearCicloAsync();

        var response = await _factory.CrearCliente(SubA).GetAsync("/api/ciclos-escolares");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(ciclo, ids);
    }

    [Fact]
    public async Task Crear_ciclo_devuelve_201_y_persiste()
    {
        var body = Body(new
        {
            nombre = "Año lectivo 2026",
            fechaInicio = "2026-01-01",
            fechaFin = "2026-12-31"
        });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/ciclos-escolares", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Aparece al listar (única institución mono).
        var lista = await GetOkAsync(_factory.CrearCliente(SubA), "/api/ciclos-escolares");
        Assert.Contains("Año lectivo 2026",
            lista.EnumerateArray().Select(x => x.GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task Crear_ciclo_con_rango_invertido_devuelve_400()
    {
        var body = Body(new
        {
            nombre = "Ciclo malo",
            fechaInicio = "2026-12-31",
            fechaFin = "2026-01-01"
        });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/ciclos-escolares", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Actualizar_ciclo_preserva_activo_y_devuelve_204()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var body = Body(new
        {
            nombre = "Año lectivo 2026 actualizado",
            fechaInicio = "2026-01-01",
            fechaFin = "2026-12-31"
        });

        var response = await _factory.CrearCliente(SubA)
            .PutAsync($"/api/ciclos-escolares/{ciclo}", body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var lista = await GetOkAsync(_factory.CrearCliente(SubA), "/api/ciclos-escolares");
        var fila = lista.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == ciclo);
        Assert.Equal("Año lectivo 2026 actualizado", fila.GetProperty("nombre").GetString());
        Assert.True(fila.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Desactivar_ciclo_devuelve_204_y_reactivar_restaura()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var client = _factory.CrearCliente(SubA);

        var desactivar = await client.PostAsync(
            $"/api/ciclos-escolares/{ciclo}/desactivar",
            Body(new { motivo = "Cierre de año" }));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var listaInactivo = await GetOkAsync(client, "/api/ciclos-escolares");
        Assert.False(listaInactivo.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == ciclo)
            .GetProperty("activo").GetBoolean());

        var reactivar = await client.PostAsync(
            $"/api/ciclos-escolares/{ciclo}/reactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var listaActivo = await GetOkAsync(client, "/api/ciclos-escolares");
        Assert.True(listaActivo.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == ciclo)
            .GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Desactivar_sin_motivo_devuelve_400()
    {
        var ciclo = await _factory.CrearCicloAsync();

        var response = await _factory.CrearCliente(SubA).PostAsync(
            $"/api/ciclos-escolares/{ciclo}/desactivar",
            Body(new { motivo = " " }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ----- Periodos -----

    [Fact]
    public async Task Crear_periodo_dentro_del_ciclo_devuelve_201()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var body = Body(new
        {
            nombre = "Primer periodo",
            tipo = "regular",
            fechaInicio = "2026-03-01",
            fechaFin = "2026-06-30"
        });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync($"/api/ciclos-escolares/{ciclo}/periodos", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Listar_periodos_de_un_ciclo_devuelve_los_sembrados()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var periodo = await _factory.CrearPeriodoAsync(ciclo);

        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/ciclos-escolares/{ciclo}/periodos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(periodo, ids);
    }

    [Fact]
    public async Task Crear_periodo_con_rango_invertido_devuelve_400()
    {
        var ciclo = await _factory.CrearCicloAsync();
        // 015 independiza las ventanas del ciclo: lo unico que invalida es
        // nombre/fechas vacios o inicio posterior a fin.
        var body = Body(new
        {
            nombre = "Rango invertido",
            fechaInicio = "2026-06-30",
            fechaFin = "2026-03-01"
        });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync($"/api/ciclos-escolares/{ciclo}/periodos", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Desactivar_y_reactivar_periodo_devuelve_204()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var periodo = await _factory.CrearPeriodoAsync(ciclo);
        var client = _factory.CrearCliente(SubA);

        var desactivar = await client.PostAsync(
            $"/api/ciclos-escolares/{ciclo}/periodos/{periodo}/desactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var lista = await GetOkAsync(client, $"/api/ciclos-escolares/{ciclo}/periodos");
        Assert.False(lista.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == periodo)
            .GetProperty("activo").GetBoolean());

        var reactivar = await client.PostAsync(
            $"/api/ciclos-escolares/{ciclo}/periodos/{periodo}/reactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);
    }

    // ----- Seguridad / errores -----

    [Fact]
    public async Task Sin_autenticacion_devuelve_401()
    {
        var response = await _factory.CrearCliente(string.Empty)
            .GetAsync("/api/ciclos-escolares");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sin_permiso_devuelve_403()
    {
        var response = await _factory.CrearCliente("sin-permiso")
            .GetAsync("/api/ciclos-escolares");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Actualizar_ciclo_inexistente_devuelve_404()
    {
        var body = Body(new
        {
            nombre = "No existe",
            fechaInicio = "2026-01-01",
            fechaFin = "2026-12-31"
        });

        var response = await _factory.CrearCliente(SubA)
            .PutAsync($"/api/ciclos-escolares/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement> GetOkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");
}
