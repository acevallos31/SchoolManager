using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

/// <summary>
/// Integración real de 047B: usa PagosApiFactory + WebApplicationFactory y el
/// EstadoCuentaService registrado por la API. No sustituye el servicio por mocks.
/// </summary>
public sealed class EstadoCuentaControllerTests : IClassFixture<PagosApiFactory>
{
    private readonly PagosApiFactory _factory;

    public EstadoCuentaControllerTests(PagosApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => PagosApiFactory.AdminA.ToString();
    private string SubB => PagosApiFactory.AdminB.ToString();

    [Fact]
    public async Task Estado_cuenta_compone_resumen_cargos_y_pago_registrado()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);

        var registrar = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new
            {
                montoTotal = 500m,
                aplicaciones = new[] { new { cargoId = cargos[0].Id, monto = 500m } }
            }));
        Assert.Equal(HttpStatusCode.Created, registrar.StatusCode);

        var response = await cliente.GetAsync($"/api/estado-cuenta/alumno/{ctx.AlumnoId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal(ctx.InstitucionId, root.GetProperty("institucion").GetProperty("id").GetGuid());
        Assert.Equal(ctx.AlumnoId, root.GetProperty("alumno").GetProperty("id").GetGuid());

        var resumen = root.GetProperty("resumen");
        // totalObligaciones cuenta obligaciones con saldo pendiente/parcial; no suma montos.
        Assert.Equal(1L, resumen.GetProperty("totalObligaciones").GetInt64());
        Assert.Equal(1400m, resumen.GetProperty("totalMontoOriginal").GetDecimal());
        Assert.Equal(900m, resumen.GetProperty("totalPendiente").GetDecimal());
        Assert.Equal(500m, resumen.GetProperty("totalAplicado").GetDecimal());
        Assert.Equal(0m, resumen.GetProperty("totalAnulado").GetDecimal());

        var detalleCargos = root.GetProperty("cargos");
        Assert.Equal(2, detalleCargos.GetArrayLength());
        Assert.Contains(
            detalleCargos.EnumerateArray(),
            cargo => cargo.GetProperty("id").GetGuid() == cargos[0].Id
                && cargo.GetProperty("estado").GetString() == "pagado"
                && cargo.GetProperty("saldo").GetDecimal() == 0m);
        Assert.Contains(
            detalleCargos.EnumerateArray(),
            cargo => cargo.GetProperty("id").GetGuid() == cargos[1].Id
                && cargo.GetProperty("saldo").GetDecimal() == 900m);

        var pagos = root.GetProperty("pagos");
        Assert.Single(pagos.EnumerateArray());
        Assert.Equal(500m, pagos[0].GetProperty("montoTotal").GetDecimal());
        Assert.Equal("registrado", pagos[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Estado_cuenta_de_alumno_inexistente_es_404()
    {
        var cliente = _factory.CrearCliente(SubA);

        var response = await cliente.GetAsync($"/api/estado-cuenta/alumno/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Estado_cuenta_sin_permiso_Cargos_Ver_es_403()
    {
        var admin = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        await PrepararCargosAsync(admin, ctx);

        var sinPermiso = _factory.CrearCliente("sin-permiso");
        var response = await sinPermiso.GetAsync($"/api/estado-cuenta/alumno/{ctx.AlumnoId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Estado_cuenta_no_revela_alumno_de_otra_institucion()
    {
        var clienteA = _factory.CrearCliente(SubA);
        var ctxA = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        await PrepararCargosAsync(clienteA, ctxA);

        var clienteB = _factory.CrearCliente(SubB);
        var response = await clienteB.GetAsync($"/api/estado-cuenta/alumno/{ctxA.AlumnoId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<List<CargoInfo>> PrepararCargosAsync(
        HttpClient cliente, PagosApiFactory.ContextoAcademico ctx)
    {
        var asignar = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/plan?institucionId={ctx.InstitucionId}",
            Body(new { planPagoId = ctx.PlanId }));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        var generar = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/generar?institucionId={ctx.InstitucionId}",
            Body(new { }));
        Assert.Equal(HttpStatusCode.OK, generar.StatusCode);

        var response = await cliente.GetAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}?institucionId={ctx.InstitucionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray()
            .Select(c => new CargoInfo(
                c.GetProperty("id").GetGuid(),
                c.GetProperty("montoOriginal").GetDecimal()))
            .ToList();
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");

    private sealed record CargoInfo(Guid Id, decimal MontoOriginal);
}
