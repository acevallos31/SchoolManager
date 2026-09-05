using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class ConfiguracionFinancieraApiTests : IClassFixture<ConfiguracionFinancieraApiFactory>
{
    private readonly ConfiguracionFinancieraApiFactory _factory;

    public ConfiguracionFinancieraApiTests(ConfiguracionFinancieraApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => ConfiguracionFinancieraApiFactory.AdminA.ToString();
    private string SubB => ConfiguracionFinancieraApiFactory.AdminB.ToString();

    private HttpClient ClienteAdmin() => _factory.CrearCliente(SubA);
    private HttpClient ClienteSinPermisos() => _factory.CrearCliente("usuario-comun");

    // ========================= CONCEPTOS FINANCIEROS =========================

    [Fact]
    public async Task Crear_y_listar_conceptos_financieros()
    {
        var client = ClienteAdmin();
        var nombreConcepto = $"Colegiatura {Guid.NewGuid():N}";
        var crear = await client.PostAsJsonAsync("/api/conceptosfinancieros", new
        {
            nombre = nombreConcepto,
            monto = 1500.50m,
            descripcion = "Cuota mensual"
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var id = JsonNode.Parse(await crear.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();

        var listado = await client.GetAsync("/api/conceptosfinancieros");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var items = JsonSerializer.Deserialize<List<ConceptoJson>>(await listado.Content.ReadAsStringAsync())!;
        var el = Assert.Single(items, c => c.id == id);
        Assert.Equal(nombreConcepto, el.nombre);
        Assert.Equal(1500.50m, el.monto);
        Assert.True(el.activo);
    }

    [Fact]
    public async Task Crear_concepto_sin_nombre_devuelve_400()
    {
        var resp = await ClienteAdmin().PostAsJsonAsync("/api/conceptosfinancieros", new
        {
            nombre = "",
            monto = 10
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Crear_concepto_sin_permiso_devuelve_403()
    {
        var resp = await ClienteSinPermisos().PostAsJsonAsync("/api/conceptosfinancieros", new
        {
            nombre = "X",
            monto = 10
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Actualizar_nombre_y_monto_de_concepto()
    {
        var client = ClienteAdmin();
        var id = await CrearConceptoAsync(client, "Matricula inicial", 500);

        var put = await client.PutAsJsonAsync($"/api/conceptosfinancieros/{id}", new
        {
            nombre = "Matricula anual",
            monto = 750,
            descripcion = "Actualizado"
        });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var get = await client.GetAsync("/api/conceptosfinancieros");
        var el = Assert.Single(
            JsonSerializer.Deserialize<List<ConceptoJson>>(await get.Content.ReadAsStringAsync())!,
            c => c.id == id);
        Assert.Equal("Matricula anual", el.nombre);
        Assert.Equal(750, el.monto);
    }

    [Fact]
    public async Task Desactivar_y_reactivar_concepto()
    {
        var client = ClienteAdmin();
        var id = await CrearConceptoAsync(client, "Deportes", 200);

        // Se usa DELETE + body motivo para desactivar (convencion del controller).
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/conceptosfinancieros/{id}");
        req.Content = JsonContent.Create(new { motivo = "Ya no se cobra" });
        var del = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var inactivo = JsonSerializer.Deserialize<List<ConceptoJson>>(
            await (await client.GetAsync("/api/conceptosfinancieros")).Content.ReadAsStringAsync())!;
        var d = Assert.Single(inactivo, c => c.id == id);
        Assert.False(d.activo);
        Assert.Equal("Ya no se cobra", d.motivoDesactivacion);

        var reactivar = await client.PostAsync($"/api/conceptosfinancieros/{id}/reactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var activo = JsonSerializer.Deserialize<List<ConceptoJson>>(
            await (await client.GetAsync("/api/conceptosfinancieros")).Content.ReadAsStringAsync())!;
        Assert.True(Assert.Single(activo, c => c.id == id).activo);
    }

    [Fact]
    public async Task Desactivar_concepto_sin_motivo_devuelve_400()
    {
        var client = ClienteAdmin();
        var id = await CrearConceptoAsync(client, "Taller", 100);

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/conceptosfinancieros/{id}");
        req.Content = JsonContent.Create(new { });
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ============================ PLANES DE PAGO =============================

    [Fact]
    public async Task Crear_y_obtener_plan_con_cuotas()
    {
        var client = ClienteAdmin();
        var conceptoId = await CrearConceptoAsync(client, "Colegiatura", 1500);

        var crear = await client.PostAsJsonAsync("/api/planesPago", new
        {
            nombre = "Plan anual",
            descripcion = "12 cuotas",
            cuotas = new[]
            {
                new { orden = 1, conceptoId, descripcion = "Cuota 1", monto = 125.5m, vencimientoDias = 30 },
                new { orden = 2, conceptoId, descripcion = "Cuota 2", monto = 125.5m, vencimientoDias = 60 }
            }
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var planId = JsonNode.Parse(await crear.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();

        var get = await client.GetAsync($"/api/planesPago/{planId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var detalle = JsonSerializer.Deserialize<PlanDetalleJson>(await get.Content.ReadAsStringAsync())!;
        Assert.Equal("Plan anual", detalle.nombre);
        Assert.Equal(2, detalle.cuotas!.Count);
        Assert.Equal(125.5m, detalle.cuotas![1].monto);
    }

    [Fact]
    public async Task Crear_plan_sin_cuotas_devuelve_400()
    {
        var resp = await ClienteAdmin().PostAsJsonAsync("/api/planesPago", new
        {
            nombre = "Plan vacio",
            cuotas = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Crear_plan_sin_permiso_devuelve_403()
    {
        var resp = await ClienteSinPermisos().PostAsJsonAsync("/api/planesPago", new
        {
            nombre = "Plan",
            cuotas = new[] { new { orden = 1, monto = 10, vencimientoDias = 0 } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Plan_listado_incluye_totales()
    {
        var client = ClienteAdmin();
        var nombre = $"Plan A {Guid.NewGuid():N}";
        var cuotas = new[]
        {
            new { orden = 1, monto = 10, vencimientoDias = 30 },
            new { orden = 2, monto = 20, vencimientoDias = 60 },
            new { orden = 3, monto = 30, vencimientoDias = 90 }
        };
        var crear = await client.PostAsJsonAsync("/api/planesPago", new { nombre, cuotas });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var listado = await client.GetAsync("/api/planesPago");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var items = JsonSerializer.Deserialize<List<PlanListaJson>>(await listado.Content.ReadAsStringAsync())!;
        var el = Assert.Single(items, p => p.nombre == nombre);
        Assert.Equal(3, el.totalCuotas);
        Assert.Equal(60, el.montoTotal);
    }

    [Fact]
    public async Task Actualizar_plan_reemplaza_cuotas()
    {
        var client = ClienteAdmin();
        var planId = await CrearPlanAsync(client, "Plan B", [10, 20]);

        var put = await client.PutAsJsonAsync($"/api/planesPago/{planId}", new
        {
            nombre = "Plan B editado",
            descripcion = "nuevo",
            cuotas = new[]
            {
                new { orden = 1, monto = 100, vencimientoDias = 30 },
                new { orden = 2, monto = 200, vencimientoDias = 60 },
                new { orden = 3, monto = 300, vencimientoDias = 90 }
            }
        });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var detalle = JsonSerializer.Deserialize<PlanDetalleJson>(
            await (await client.GetAsync($"/api/planesPago/{planId}")).Content.ReadAsStringAsync())!;
        Assert.Equal("Plan B editado", detalle.nombre);
        Assert.Equal(3, detalle.cuotas!.Count);
        Assert.Equal(300, detalle.cuotas![2].monto);
    }

    // =============================== HELPERS ================================

    private async Task<Guid> CrearConceptoAsync(HttpClient client, string nombre, decimal monto)
    {
        var resp = await client.PostAsJsonAsync("/api/conceptosfinancieros", new
        {
            nombre = $"{nombre} {Guid.NewGuid():N}",
            monto
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();
    }

    private async Task<Guid> CrearPlanAsync(HttpClient client, string nombre, int[] montos)
    {
        var cuotas = montos.Select((m, i) => new { orden = i + 1, monto = m, vencimientoDias = (i + 1) * 30 }).ToArray();
        var resp = await client.PostAsJsonAsync("/api/planesPago", new { nombre = $"{nombre} {Guid.NewGuid():N}", cuotas });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();
    }

    private sealed record ConceptoJson(Guid id, string nombre, decimal monto, bool activo, string? motivoDesactivacion, string? descripcion, DateTime? fechaDesactivacion);
    private sealed record PlanDetalleJson(Guid id, string nombre, decimal? montoTotal, long? totalCuotas, List<CuotaJson>? cuotas);
    private sealed record PlanListaJson(Guid id, string nombre, long totalCuotas, decimal montoTotal);
    private sealed record CuotaJson(Guid? id, int orden, Guid? conceptoId, string? conceptoNombre, string? descripcion, decimal monto, int vencimientoDias);
}