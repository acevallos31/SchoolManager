using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class CargosControllerTests : IClassFixture<CargosApiFactory>
{
    private readonly CargosApiFactory _factory;

    public CargosControllerTests(CargosApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => CargosApiFactory.AdminA.ToString();
    private string SubB => CargosApiFactory.AdminB.ToString();

    [Fact]
    public async Task Asignar_generar_listar_resumen_y_anular_flujo_completo()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);

        var asignar = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/plan?institucionId={ctx.InstitucionId}",
            Body(new { planPagoId = ctx.PlanId }));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        var generar = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/generar?institucionId={ctx.InstitucionId}",
            Body(new { }));
        Assert.Equal(HttpStatusCode.OK, generar.StatusCode);
        using (var gen = JsonDocument.Parse(await generar.Content.ReadAsStringAsync()))
            Assert.Equal(2, gen.RootElement.GetProperty("generados").GetInt32());

        // Listar por matricula: dos cargos pendientes.
        var lista = await cliente.GetAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}?institucionId={ctx.InstitucionId}");
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        using var json = JsonDocument.Parse(await lista.Content.ReadAsStringAsync());
        var cargos = json.RootElement;
        Assert.Equal(2, cargos.GetArrayLength());
        Assert.All(cargos.EnumerateArray(), c => Assert.Equal("pendiente", c.GetProperty("estado").GetString()));
        Assert.StartsWith("Colegiatura", cargos[0].GetProperty("conceptoNombre").GetString());
        Assert.Equal(900m, cargos[1].GetProperty("montoOriginal").GetDecimal());

        // Resumen: 2 obligaciones, 1400 pendiente, 0 vencido.
        var resumen = await cliente.GetAsync(
            $"/api/cargos/alumno/{ctx.AlumnoId}/resumen?institucionId={ctx.InstitucionId}");
        Assert.Equal(HttpStatusCode.OK, resumen.StatusCode);
        using var res = JsonDocument.Parse(await resumen.Content.ReadAsStringAsync());
        Assert.Equal(2, res.RootElement.GetProperty("totalObligaciones").GetInt64());
        Assert.Equal(1400m, res.RootElement.GetProperty("totalPendiente").GetDecimal());
        Assert.Equal(0m, res.RootElement.GetProperty("totalVencido").GetDecimal());

        // Anular el primer cargo (soft state).
        var cargoId = cargos[0].GetProperty("id").GetGuid();
        var anular = await cliente.PostAsync(
            $"/api/cargos/{cargoId}/anular?institucionId={ctx.InstitucionId}",
            Body(new { motivo = "Error de facturacion" }));
        Assert.Equal(HttpStatusCode.NoContent, anular.StatusCode);

        var lista2 = await cliente.GetAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}?institucionId={ctx.InstitucionId}");
        using var json2 = JsonDocument.Parse(await lista2.Content.ReadAsStringAsync());
        Assert.Equal("anulado", json2.RootElement[0].GetProperty("estado").GetString());
        Assert.Equal("Error de facturacion", json2.RootElement[0].GetProperty("motivoAnulacion").GetString());

        var resumen2 = await cliente.GetAsync(
            $"/api/cargos/alumno/{ctx.AlumnoId}/resumen?institucionId={ctx.InstitucionId}");
        using var res2 = JsonDocument.Parse(await resumen2.Content.ReadAsStringAsync());
        Assert.Equal(900m, res2.RootElement.GetProperty("totalPendiente").GetDecimal());
        Assert.Equal(500m, res2.RootElement.GetProperty("totalAnulado").GetDecimal());
    }

    [Fact]
    public async Task Generar_sin_plan_asignado_devuelve_404()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);

        var generar = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/generar?institucionId={ctx.InstitucionId}",
            Body(new { }));
        Assert.Equal(HttpStatusCode.NotFound, generar.StatusCode);
    }

    [Fact]
    public async Task Generar_duplicado_devuelve_409()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);

        var asignar = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/plan?institucionId={ctx.InstitucionId}",
            Body(new { planPagoId = ctx.PlanId }));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        var primero = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/generar?institucionId={ctx.InstitucionId}",
            Body(new { }));
        var duplicado = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/generar?institucionId={ctx.InstitucionId}",
            Body(new { }));
        Assert.Equal(HttpStatusCode.OK, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Anular_sin_motivo_devuelve_400()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);

        var response = await cliente.PostAsync(
            $"/api/cargos/{Guid.NewGuid()}/anular?institucionId={ctx.InstitucionId}",
            Body(new { motivo = "" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anular_cargo_inexistente_devuelve_404()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);

        var response = await cliente.PostAsync(
            $"/api/cargos/{Guid.NewGuid()}/anular?institucionId={ctx.InstitucionId}",
            Body(new { motivo = "Correccion" }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Listar_matricula_inexistente_devuelve_404()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);

        var response = await cliente.GetAsync(
            $"/api/cargos/matricula/{Guid.NewGuid()}?institucionId={ctx.InstitucionId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resumen_alumno_sin_cargos_devuelve_ceros()
    {
        var cliente = _factory.CrearCliente(SubA);
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);

        var resumen = await cliente.GetAsync(
            $"/api/cargos/alumno/{ctx.AlumnoId}/resumen?institucionId={ctx.InstitucionId}");
        Assert.Equal(HttpStatusCode.OK, resumen.StatusCode);
        using var res = JsonDocument.Parse(await resumen.Content.ReadAsStringAsync());
        Assert.Equal(0, res.RootElement.GetProperty("totalObligaciones").GetInt64());
        Assert.Equal(0m, res.RootElement.GetProperty("totalPendiente").GetDecimal());
    }

    [Fact]
    public async Task Sin_permiso_devuelve_403()
    {
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var cliente = _factory.CrearCliente("sin-permiso");

        var leer = await cliente.GetAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}?institucionId={ctx.InstitucionId}");
        Assert.Equal(HttpStatusCode.Forbidden, leer.StatusCode);

        var generar = await cliente.PostAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}/generar?institucionId={ctx.InstitucionId}",
            Body(new { }));
        Assert.Equal(HttpStatusCode.Forbidden, generar.StatusCode);
    }

    [Fact]
    public async Task AdminB_no_accede_a_cargos_de_matricula_de_AdminA()
    {
        var ctx = await _factory.SembrarContextoAsync(_factory.InstitucionA);
        var clienteB = _factory.CrearCliente(SubB);

        // AdminB (rol solo en B) consulta la matricula de A apuntando a su propia
        // institucion: la RPC resuelve B y, al no existir el recurso dentro de su
        // contexto visible, devuelve P0002 sin revelar que existe en A.
        var lista = await clienteB.GetAsync(
            $"/api/cargos/matricula/{ctx.MatriculaId}?institucionId={_factory.InstitucionB}");
        Assert.Equal(HttpStatusCode.NotFound, lista.StatusCode);

        var resumen = await clienteB.GetAsync(
            $"/api/cargos/alumno/{ctx.AlumnoId}/resumen?institucionId={_factory.InstitucionB}");
        Assert.Equal(HttpStatusCode.NotFound, resumen.StatusCode);
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");
}
