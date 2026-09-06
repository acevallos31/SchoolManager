using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class PagosControllerTests : IClassFixture<PagosApiFactory>
{
    private readonly PagosApiFactory _factory;

    public PagosControllerTests(PagosApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => PagosApiFactory.AdminA.ToString();
    private string SubB => PagosApiFactory.AdminB.ToString();

    [Fact]
    public async Task Registrar_pago_total_deja_cargo_pagado_y_actualiza_resumen()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);
        var (cargo0, _) = (cargos[0], cargos[1]); // 500 y 900

        var registrar = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 500m, aplicaciones = new[] { new { cargoId = cargo0.Id, monto = 500m } } }));
        Assert.Equal(HttpStatusCode.Created, registrar.StatusCode);
        var pagoId = PagoIdDe(registrar);

        var cargosTras = await ListarCargosAsync(cliente, ctx);
        Assert.Equal("pagado", cargosTras[0].Estado);
        Assert.Equal(0m, cargosTras[0].Saldo);
        Assert.Equal(500m, cargosTras[0].Aplicado);

        var resumen = await GetAsync(cliente,
            $"/api/cargos/alumno/{ctx.AlumnoId}/resumen?institucionId={ctx.InstitucionId}");
        Assert.Equal(900m, resumen.GetProperty("totalPendiente").GetDecimal());
        Assert.Equal(500m, resumen.GetProperty("totalAplicado").GetDecimal());

        var pagos = await GetAsync(cliente,
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}");
        Assert.Equal(1, pagos.GetArrayLength());
        Assert.Equal("registrado", pagos[0].GetProperty("estado").GetString());
        Assert.Equal(500m, pagos[0].GetProperty("montoTotal").GetDecimal());
        Assert.True(pagos[0].GetProperty("numeroRecibo").GetInt64() >= 1);
    }

    [Fact]
    public async Task Registrar_pago_parcial_deja_cargo_parcial_y_saldo_derivado()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);

        var registrar = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 200m, aplicaciones = new[] { new { cargoId = cargos[0].Id, monto = 200m } } }));
        Assert.Equal(HttpStatusCode.Created, registrar.StatusCode);

        var cargosTras = await ListarCargosAsync(cliente, ctx);
        Assert.Equal("parcial", cargosTras[0].Estado);
        Assert.Equal(300m, cargosTras[0].Saldo);
        Assert.Equal(200m, cargosTras[0].Aplicado);

        var resumen = await GetAsync(cliente,
            $"/api/cargos/alumno/{ctx.AlumnoId}/resumen?institucionId={ctx.InstitucionId}");
        Assert.Equal(1200m, resumen.GetProperty("totalPendiente").GetDecimal());
        Assert.Equal(200m, resumen.GetProperty("totalAplicado").GetDecimal());
    }

    [Fact]
    public async Task Registrar_pago_a_varios_cargos_se_reparte_y_los_completa()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);

        var registrar = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new
            {
                montoTotal = 1400m,
                aplicaciones = new[]
                {
                    new { cargoId = cargos[0].Id, monto = 500m },
                    new { cargoId = cargos[1].Id, monto = 900m }
                }
            }));
        Assert.Equal(HttpStatusCode.Created, registrar.StatusCode);
        var pagoId = PagoIdDe(registrar);

        var cargosTras = await ListarCargosAsync(cliente, ctx);
        Assert.All(cargosTras, c => Assert.Equal("pagado", c.Estado));
        Assert.All(cargosTras, c => Assert.Equal(0m, c.Saldo));

        var aplicaciones = await GetAsync(cliente,
            $"/api/pagos/{pagoId}/aplicaciones?institucionId={ctx.InstitucionId}");
        Assert.Equal(2, aplicaciones.GetArrayLength());
        Assert.Equal("vigente", aplicaciones[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Sobrepago_a_un_cargo_es_rechazado_409()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);

        var registrar = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 600m, aplicaciones = new[] { new { cargoId = cargos[0].Id, monto = 600m } } }));
        Assert.Equal(HttpStatusCode.Conflict, registrar.StatusCode);

        var cargosTras = await ListarCargosAsync(cliente, ctx);
        Assert.Equal("pendiente", cargosTras[0].Estado);
    }

    [Fact]
    public async Task Suma_aplicaciones_distinta_de_monto_total_es_rechazada_409()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);

        var registrar = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 300m, aplicaciones = new[] { new { cargoId = cargos[0].Id, monto = 200m } } }));
        Assert.Equal(HttpStatusCode.Conflict, registrar.StatusCode);
    }

    [Fact]
    public async Task Referencia_externa_duplicada_en_misma_institucion_es_rechazada_409()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);
        const string referencia = "TRF-DUP";

        var primero = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 500m, aplicaciones = new[] { new { cargoId = cargos[0].Id, monto = 500m } }, referenciaExterna = referencia }));
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 900m, aplicaciones = new[] { new { cargoId = cargos[1].Id, monto = 900m } }, referenciaExterna = referencia }));
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Anular_pago_revierte_aplicaciones_y_recalcula_estado()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);

        var registrar = await cliente.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 300m, aplicaciones = new[] { new { cargoId = cargos[0].Id, monto = 300m } } }));
        var pagoId = PagoIdDe(registrar);

        var cargosParcial = await ListarCargosAsync(cliente, ctx);
        Assert.Equal("parcial", cargosParcial[0].Estado);
        Assert.Equal(200m, cargosParcial[0].Saldo);

        var anular = await cliente.PostAsync(
            $"/api/pagos/{pagoId}/anular?institucionId={ctx.InstitucionId}",
            Body(new { motivo = "Pago erroneo" }));
        Assert.Equal(HttpStatusCode.NoContent, anular.StatusCode);

        var pagos = await GetAsync(cliente,
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}");
        Assert.Equal("anulado", pagos[0].GetProperty("estado").GetString());
        Assert.Equal("Pago erroneo", pagos[0].GetProperty("motivoAnulacion").GetString());

        var cargosTras = await ListarCargosAsync(cliente, ctx);
        Assert.Equal("pendiente", cargosTras[0].Estado);
        Assert.Equal(500m, cargosTras[0].Saldo);
        Assert.Equal(0m, cargosTras[0].Aplicado);

        var resumen = await GetAsync(cliente,
            $"/api/cargos/alumno/{ctx.AlumnoId}/resumen?institucionId={ctx.InstitucionId}");
        Assert.Equal(1400m, resumen.GetProperty("totalPendiente").GetDecimal());
        Assert.Equal(0m, resumen.GetProperty("totalAplicado").GetDecimal());
    }

    [Fact]
    public async Task Sin_permiso_no_se_puede_registrar_ni_listar_403()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cargos = await PrepararCargosAsync(cliente, ctx);
        var anonimo = _factory.CrearCliente("sin-permiso");

        var registrar = await anonimo.PostAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}",
            Body(new { montoTotal = 500m, aplicaciones = new[] { new { cargoId = cargos[0].Id, monto = 500m } } }));
        Assert.Equal(HttpStatusCode.Forbidden, registrar.StatusCode);

        var listar = await anonimo.GetAsync(
            $"/api/pagos/alumno/{ctx.AlumnoId}?institucionId={ctx.InstitucionId}");
        Assert.Equal(HttpStatusCode.Forbidden, listar.StatusCode);
    }

    // ----- Helpers -----

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

        return await ListarCargosAsync(cliente, ctx);
    }

    private async Task<List<CargoInfo>> ListarCargosAsync(
        HttpClient cliente, PagosApiFactory.ContextoAcademico ctx)
    {
        var json = await GetAsync(cliente,
            $"/api/cargos/matricula/{ctx.MatriculaId}?institucionId={ctx.InstitucionId}");
        var lista = new List<CargoInfo>();
        foreach (var c in json.EnumerateArray())
        {
            lista.Add(new CargoInfo(
                c.GetProperty("id").GetGuid(),
                c.GetProperty("montoOriginal").GetDecimal(),
                c.GetProperty("estado").GetString()!,
                c.GetProperty("saldo").GetDecimal(),
                c.GetProperty("aplicado").GetDecimal()));
        }
        return lista;
    }

    private static Guid PagoIdDe(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetAsync(HttpClient cliente, string url)
    {
        var response = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");

    private sealed record CargoInfo(
        Guid Id, decimal MontoOriginal, string Estado, decimal Saldo, decimal Aplicado);
}
