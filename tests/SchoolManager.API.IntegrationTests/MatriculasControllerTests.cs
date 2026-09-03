using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class MatriculasControllerTests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public MatriculasControllerTests(MatriculasApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => MatriculasApiFactory.AdminA.ToString();
    private string SubB => MatriculasApiFactory.AdminB.ToString();

    [Fact]
    public async Task Listado_con_varias_matriculas_devuelve_todas()
    {
        var institucion = _factory.InstitucionA;
        var ctx = await _factory.CrearContextoAsync(institucion);
        var alumno = await _factory.CrearAlumnoAsync(institucion);

        var m1 = await _factory.MatricularAsync(alumno, ctx.SeccionId, ctx.PeriodoId);
        var m2 = await MatricularEnCicloDiferenteAsync(institucion, ctx, alumno, "B");
        var m3 = await MatricularEnCicloDiferenteAsync(institucion, ctx, alumno, "C");

        var client = _factory.CrearCliente(SubA);
        var response = await client.GetAsync(
            $"/api/matriculas?institucionId={institucion}&alumnoId={alumno}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .Order()
            .ToArray();

        Assert.Equal(3, ids.Length);
        Assert.Contains(m1, ids);
        Assert.Contains(m2, ids);
        Assert.Contains(m3, ids);
    }

    [Fact]
    public async Task Detalle_valido_devuelve_200()
    {
        var institucion = _factory.InstitucionA;
        var ctx = await _factory.CrearContextoAsync(institucion);
        var alumno = await _factory.CrearAlumnoAsync(institucion);
        var matricula = await _factory.MatricularAsync(alumno, ctx.SeccionId, ctx.PeriodoId);

        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/matriculas/{matricula}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(matricula, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("pendiente", json.RootElement.GetProperty("estado").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.GetProperty("nombreAlumno").GetString()));
    }

    [Fact]
    public async Task Matricula_inexistente_devuelve_404()
    {
        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/matriculas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Matricula_duplicada_devuelve_409()
    {
        var institucion = _factory.InstitucionA;
        var ctx = await _factory.CrearContextoAsync(institucion);
        var alumno = await _factory.CrearAlumnoAsync(institucion);
        var body = Body(new
        {
            alumnoId = alumno,
            seccionId = ctx.SeccionId,
            periodoMatriculaId = ctx.PeriodoId
        });

        var client = _factory.CrearCliente(SubA);
        var primero = await client.PostAsync("/api/matriculas", body);
        var duplicada = await client.PostAsync("/api/matriculas", body);

        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicada.StatusCode);
    }

    [Fact]
    public async Task Cupo_lleno_devuelve_409()
    {
        var institucion = _factory.InstitucionA;
        var ctx = await _factory.CrearContextoAsync(institucion, cupo: 1);
        var alumno1 = await _factory.CrearAlumnoAsync(institucion);
        var alumno2 = await _factory.CrearAlumnoAsync(institucion);

        var client = _factory.CrearCliente(SubA);
        var primero = await client.PostAsync("/api/matriculas", Body(new
        {
            alumnoId = alumno1,
            seccionId = ctx.SeccionId,
            periodoMatriculaId = ctx.PeriodoId
        }));
        var lleno = await client.PostAsync("/api/matriculas", Body(new
        {
            alumnoId = alumno2,
            seccionId = ctx.SeccionId,
            periodoMatriculaId = ctx.PeriodoId
        }));

        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, lleno.StatusCode);
    }

    [Fact]
    public async Task Transicion_invalida_devuelve_400()
    {
        var institucion = _factory.InstitucionA;
        var ctx = await _factory.CrearContextoAsync(institucion);
        var alumno = await _factory.CrearAlumnoAsync(institucion);
        var matricula = await _factory.MatricularAsync(alumno, ctx.SeccionId, ctx.PeriodoId);

        // pendiente -> finalizada no esta permitida en el modelo de estados.
        var response = await _factory.CrearCliente(SubA)
            .PutAsync(
                $"/api/matriculas/{matricula}/estado",
                Body(new { estado = "finalizada", motivo = "no aplica" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Estado_terminal_sin_motivo_devuelve_400()
    {
        var institucion = _factory.InstitucionA;
        var ctx = await _factory.CrearContextoAsync(institucion);
        var alumno = await _factory.CrearAlumnoAsync(institucion);
        var matricula = await _factory.MatricularAsync(alumno, ctx.SeccionId, ctx.PeriodoId);

        // "anulada" es terminal: el motivo es obligatorio para ese estado.
        var response = await _factory.CrearCliente(SubA)
            .PutAsync(
                $"/api/matriculas/{matricula}/estado",
                Body(new { estado = "anulada" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Falta_permiso_devuelve_403()
    {
        var response = await _factory.CrearCliente("sin-permiso")
            .GetAsync($"/api/matriculas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Contexto_multiinstitucion_no_arroja_500_ni_filtra_datos_de_otra_institucion()
    {
        var ctxA = await _factory.CrearContextoAsync(_factory.InstitucionA);
        var ctxB = await _factory.CrearContextoAsync(_factory.InstitucionB);
        var alumnoA = await _factory.CrearAlumnoAsync(_factory.InstitucionA);
        var alumnoB = await _factory.CrearAlumnoAsync(_factory.InstitucionB);
        var matriculaA = await _factory.MatricularAsync(alumnoA, ctxA.SeccionId, ctxA.PeriodoId);
        var matriculaB = await _factory.MatricularAsync(alumnoB, ctxB.SeccionId, ctxB.PeriodoId);

        // AdminA solo tiene rol en la institucion A.
        using (var clienteA = _factory.CrearCliente(SubA))
        {
            // Detalle de una matricula de B: no se filtra ni cae en 500.
            var detalleB = await clienteA.GetAsync($"/api/matriculas/{matriculaB}");
            Assert.Equal(HttpStatusCode.NotFound, detalleB.StatusCode);

            // Listado por alumno de B: vacio, sin fugas.
            var listaAlumnoB = await clienteA.GetAsync($"/api/matriculas/alumno/{alumnoB}");
            Assert.Equal(HttpStatusCode.OK, listaAlumnoB.StatusCode);
            using var vacio = JsonDocument.Parse(await listaAlumnoB.Content.ReadAsStringAsync());
            Assert.Empty(vacio.RootElement.EnumerateArray());

            // Listado por institucion B: vacio (sin permiso en B), no 500.
            var listaB = await clienteA.GetAsync($"/api/matriculas?institucionId={_factory.InstitucionB}");
            Assert.Equal(HttpStatusCode.OK, listaB.StatusCode);
            using var listaBJson = JsonDocument.Parse(await listaB.Content.ReadAsStringAsync());
            Assert.Empty(listaBJson.RootElement.EnumerateArray());

            // En A si ve sus propias matriculas.
            var listaA = await clienteA.GetAsync(
                $"/api/matriculas?institucionId={_factory.InstitucionA}&alumnoId={alumnoA}");
            Assert.Equal(HttpStatusCode.OK, listaA.StatusCode);
            using var listaAJson = JsonDocument.Parse(await listaA.Content.ReadAsStringAsync());
            Assert.Single(listaAJson.RootElement.EnumerateArray());
            Assert.Equal(
                matriculaA,
                listaAJson.RootElement.EnumerateArray().First().GetProperty("id").GetGuid());
        }

        // AdminB (rol en B) si puede leer la matricula y el listado de B: las
        // lecturas funcionan correctamente en modo multiinstitucion.
        using (var clienteB = _factory.CrearCliente(SubB))
        {
            var detalleB = await clienteB.GetAsync($"/api/matriculas/{matriculaB}");
            Assert.Equal(HttpStatusCode.OK, detalleB.StatusCode);

            var listaAlumnoB = await clienteB.GetAsync($"/api/matriculas/alumno/{alumnoB}");
            Assert.Equal(HttpStatusCode.OK, listaAlumnoB.StatusCode);
            using var json = JsonDocument.Parse(await listaAlumnoB.Content.ReadAsStringAsync());
            Assert.Single(json.RootElement.EnumerateArray());
        }
    }

    [Fact]
    public async Task Contexto_multi_sin_institucion_devuelve_400_y_no_500()
    {
        // El fixture esta en modo multiinstitucion. Si el listado se pide sin
        // institucionId, resolver_institucion_operacion(NULL) lanza SM003; ToError
        // lo debe mapear a 400 Bad Request y nunca propagar un 500.
        var response = await _factory.CrearCliente(SubA)
            .GetAsync("/api/matriculas");
        var cuerpo = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(cuerpo);
        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.TryGetProperty("error", out var err) ? err.GetString() : null));
    }

    private async Task<Guid> MatricularEnCicloDiferenteAsync(
        Guid institucion,
        MatriculasApiFactory.Contexto contexto,
        Guid alumno,
        string nombreSeccion)
    {
        var ciclo = await _factory.CrearCicloAsync(institucion);
        var seccion = await _factory.CrearSeccionAsync(
            institucion, ciclo, contexto.GradoId, nombreSeccion);
        var periodo = await _factory.CrearPeriodoAsync(ciclo);
        return await _factory.MatricularAsync(alumno, seccion, periodo);
    }

    private static StringContent Body(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");
}