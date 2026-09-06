using System.Net;
using System.Net.Http.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

/// <summary>
/// Bloque 022: endpoint de lectura del portal responsable. Verifica que un
/// responsable autenticado ve SOLO los datos de sus hijos vinculados con
/// acceso financiero, en su institucion; que no ve alumnos ajenos ni de otra
/// institucion (403, sin revelar existencia); y que sin sesion responde 401.
/// </summary>
public sealed class PortalResponsableControllerTests : IAsyncLifetime
{
    private readonly PortalResponsableApiFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    // ---- happy path ----

    [Fact]
    public async Task Responsable_ve_solo_a_sus_hijos_vinculados()
    {
        await _factory.RegistrarPagoAlumnoAAsync();
        var client = _factory.CrearCliente(PortalResponsableApiFactory.PadreA.ToString());

        var hijos = await client.GetFromJsonAsync<MisAlumno[]>(
            "/api/PortalResponsable/mis-alumnos");

        Assert.NotNull(hijos);
        var hijo = Assert.Single(hijos);
        Assert.Equal(_factory.AlumnoA, hijo.Id);
        Assert.Equal(_factory.InstitucionA, hijo.InstitucionId);
        Assert.False(string.IsNullOrWhiteSpace(hijo.Apellidos));
        Assert.Equal("Padre", hijo.Parentesco);
        Assert.True(hijo.EsPrincipal);
    }

    [Fact]
    public async Task Responsable_ve_resumen_cargos_y_pagos_reales_del_hijo()
    {
        await _factory.RegistrarPagoAlumnoAAsync();
        var client = _factory.CrearCliente(PortalResponsableApiFactory.PadreA.ToString());
        var url = $"/api/PortalResponsable/alumnos/{_factory.AlumnoA}";

        var resumen = await client.GetFromJsonAsync<Resumen>(
            $"{url}/resumen");
        Assert.NotNull(resumen);
        Assert.Equal(_factory.AlumnoA, resumen.AlumnoId);
        // Cuota pagada en su totalidad: aplicado 500, sin pendientes (obligaciones es COUNT).
        Assert.Equal(500m, resumen.TotalAplicado);
        Assert.Equal(500m, resumen.TotalMontoOriginal);
        Assert.Equal(0m, resumen.TotalPendiente);
        Assert.Equal(0L, resumen.TotalObligaciones);

        var cargos = await client.GetFromJsonAsync<Cargo[]>(
            $"{url}/cargos");
        var cargo = Assert.Single(cargos!);
        Assert.Equal("pagado", cargo.Estado);
        Assert.Equal(500m, cargo.MontoOriginal);
        Assert.Equal(0m, cargo.Saldo);

        var pagos = await client.GetFromJsonAsync<Pago[]>(
            $"{url}/pagos");
        var pago = Assert.Single(pagos!);
        Assert.Equal("registrado", pago.Estado);
        Assert.Equal(500m, pago.MontoTotal);

        var aplicaciones = await client.GetFromJsonAsync<Aplicacion[]>(
            $"/api/PortalResponsable/pagos/{pago.Id}/aplicaciones");
        var ap = Assert.Single(aplicaciones!);
        Assert.Equal(cargo.Id, ap.CargoId);
        Assert.Equal(500m, ap.MontoAplicado);
    }

    // ---- aislamiento / no revelar existencia ----

    [Fact]
    public async Task Responsable_no_ve_alumno_de_otra_institucion()
    {
        await _factory.RegistrarPagoAlumnoAAsync();
        var client = _factory.CrearCliente(PortalResponsableApiFactory.PadreA.ToString());

        var resumen = await client.GetAsync(
            $"/api/PortalResponsable/alumnos/{_factory.AlumnoB}/resumen");
        Assert.Equal(HttpStatusCode.Forbidden, resumen.StatusCode);

        var cargos = await client.GetAsync(
            $"/api/PortalResponsable/alumnos/{_factory.AlumnoB}/cargos");
        Assert.Equal(HttpStatusCode.Forbidden, cargos.StatusCode);
    }

    [Fact]
    public async Task Responsable_no_ve_resumen_de_alumno_inexistente()
    {
        var client = _factory.CrearCliente(PortalResponsableApiFactory.PadreA.ToString());

        var resumen = await client.GetAsync(
            $"/api/PortalResponsable/alumnos/{Guid.NewGuid()}/resumen");
        Assert.Equal(HttpStatusCode.Forbidden, resumen.StatusCode);
    }

    // ---- sin sesion ----

    [Fact]
    public async Task Sin_sesion_devuelve_401()
    {
        var client = _factory.CrearCliente(string.Empty);

        var resumen = await client.GetAsync(
            $"/api/PortalResponsable/alumnos/{_factory.AlumnoA}/resumen");
        Assert.Equal(HttpStatusCode.Unauthorized, resumen.StatusCode);

        var hijos = await client.GetAsync("/api/PortalResponsable/mis-alumnos");
        Assert.Equal(HttpStatusCode.Unauthorized, hijos.StatusCode);
    }

    // ---- DTOs (espejo JSON) ----

    private sealed record MisAlumno(
        Guid Id, Guid InstitucionId, string? Nombres, string? Apellidos,
        string? Parentesco, bool EsPrincipal);
    private sealed record Resumen(
        Guid AlumnoId, Guid InstitucionId, long TotalObligaciones,
        decimal TotalMontoOriginal, decimal TotalPendiente, decimal TotalVencido,
        decimal TotalAnulado, decimal TotalAplicado);
    private sealed record Cargo(
        Guid Id, Guid MatriculaId, Guid AlumnoId, Guid? PlanPagoId, int Orden,
        Guid? ConceptoId, string? ConceptoNombre, string? Descripcion,
        decimal MontoOriginal, string? FechaVencimiento, string Estado,
        string? FechaGeneracion, string? FechaAnulacion, string? MotivoAnulacion,
        bool EsVencido, decimal Saldo, decimal Aplicado);
    private sealed record Pago(
        Guid Id, Guid InstitucionId, Guid AlumnoId, Guid? ResponsableId,
        long NumeroRecibo, decimal MontoTotal, DateTimeOffset? FechaPago,
        string? MetodoPago, string? ReferenciaExterna, string Estado,
        Guid? RegistradoPor, DateTimeOffset? FechaAnulacion, Guid? AnuladoPor,
        string? MotivoAnulacion, DateTimeOffset? CreatedAt);
    private sealed record Aplicacion(
        Guid AplicacionId, Guid PagoId, Guid CargoId, Guid InstitucionId,
        decimal MontoAplicado, string Estado, DateTimeOffset? FechaReversion,
        string? CargoEstado, string? ConceptoNombre, decimal MontoOriginal);
}
