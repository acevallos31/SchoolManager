using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

/// <summary>
/// Prueba E2E de persistencia del flujo de matrícula dentro del entorno de integración:
/// HTTP POST -> API .NET -> RPC/DB PostgreSQL -> HTTP GET.
/// No usa mocks del frontend ni inserta la matrícula directamente con helpers de DB.
/// </summary>
public sealed class MatriculasPersistenceE2ETests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public MatriculasPersistenceE2ETests(MatriculasApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Registrar_matricula_por_api_persiste_y_puede_leerse_por_api()
    {
        var institucion = _factory.InstitucionA;
        var contexto = await _factory.CrearContextoAsync(institucion);
        var alumno = await _factory.CrearAlumnoAsync(institucion);
        using var client = _factory.CrearCliente(MatriculasApiFactory.AdminA.ToString());

        var crear = await client.PostAsync(
            "/api/matriculas",
            Body(new
            {
                alumnoId = alumno,
                seccionId = contexto.SeccionId,
                periodoMatriculaId = contexto.PeriodoId
            }));

        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        using var creadoJson = JsonDocument.Parse(await crear.Content.ReadAsStringAsync());
        var matriculaId = creadoJson.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, matriculaId);

        var detalle = await client.GetAsync($"/api/matriculas/{matriculaId}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);

        using var detalleJson = JsonDocument.Parse(await detalle.Content.ReadAsStringAsync());
        Assert.Equal(matriculaId, detalleJson.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(alumno, detalleJson.RootElement.GetProperty("alumnoId").GetGuid());
        Assert.Equal(contexto.SeccionId, detalleJson.RootElement.GetProperty("seccionId").GetGuid());
        Assert.Equal(contexto.PeriodoId, detalleJson.RootElement.GetProperty("periodoMatriculaId").GetGuid());
        Assert.Equal("pendiente", detalleJson.RootElement.GetProperty("estado").GetString());

        var listado = await client.GetAsync(
            $"/api/matriculas?institucionId={institucion}&alumnoId={alumno}");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);

        using var listadoJson = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.Contains(
            listadoJson.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == matriculaId);
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");
}
