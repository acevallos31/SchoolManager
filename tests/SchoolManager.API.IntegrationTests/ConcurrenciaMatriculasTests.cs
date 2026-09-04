using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

/// <summary>
/// PERF-06 — Concurrencia real contra PostgreSQL local (Testcontainers).
/// Verifica que la defensa de integridad (SELECT ... FOR UPDATE sobre la
/// sección + restricción única alumno+ciclo) garantiza:
///   - cupo nunca excedido (exactamente una creación cuando queda 1 cupo)
///   - nunca duplica matrículas del mismo alumno/ciclo bajo concurrencia
/// Las peticiones se lanzan simultáneamente (sin encolar) contra la API real.
/// </summary>
public sealed class ConcurrenciaMatriculasTests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public ConcurrenciaMatriculasTests(MatriculasApiFactory factory)
    {
        _factory = factory;
    }

    private string Sub => MatriculasApiFactory.AdminA.ToString();

    [Fact]
    public async Task Cupo_uno_con_muchas_solicitudes_concurrentes_crea_exactamente_una()
    {
        var institucion = _factory.InstitucionA;
        // Sección con cupo = 1 (una de las matrículas ya consolida ese cupo en
        // 'pendiente'/'activa' o queda libre). Creamos cupo 1 vacío.
        var ctx = await _factory.CrearContextoAsync(institucion, cupo: 1);
        const int intentos = 8;
        var alumnos = new Guid[intentos];
        for (var i = 0; i < intentos; i++)
        {
            alumnos[i] = await _factory.CrearAlumnoAsync(institucion);
        }

        var client = _factory.CrearCliente(Sub);
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < intentos; i++)
        {
            tasks.Add(PostAsync(client,
                new { alumnoId = alumnos[i], seccionId = ctx.SeccionId, periodoMatriculaId = ctx.PeriodoId }));
        }

        var resultados = await Task.WhenAll(tasks);

        var creados = resultados.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rechazados = resultados.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Exactamente uno creado; el resto rechazados de forma controlada.
        Assert.Equal(1, creados);
        Assert.Equal(intentos - 1, rechazados);

        // Nunca se excede cupo: count en la sección == 1.
        var ocupados = await ContarMatriculasVigentesAsync(ctx.SeccionId);
        Assert.Equal(1, ocupados);
    }

    [Fact]
    public async Task Mismo_alumno_ciclo_concurrente_crea_exactamente_una()
    {
        var institucion = _factory.InstitucionA;
        var ctx = await _factory.CrearContextoAsync(institucion); // cupo null = sin límite
        var alumno = await _factory.CrearAlumnoAsync(institucion);

        var client = _factory.CrearCliente(Sub);
        const int intentos = 10;
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < intentos; i++)
        {
            tasks.Add(PostAsync(client,
                new { alumnoId = alumno, seccionId = ctx.SeccionId, periodoMatriculaId = ctx.PeriodoId }));
        }

        var resultados = await Task.WhenAll(tasks);

        var creados = resultados.Count(r => r.StatusCode == HttpStatusCode.Created);
        var duplicados = resultados.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, creados);
        Assert.Equal(intentos - 1, duplicados);

        var total = await ContarMatriculasAsync(alumno);
        Assert.Equal(1, total);
    }

    private Task<HttpResponseMessage> PostAsync(HttpClient client, object body)
        => client.PostAsync("/api/matriculas", new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

    private async Task<long> ContarMatriculasVigentesAsync(Guid seccion)
    {
        await using var cmd = _factory.DatosAcademicos.CreateCommand(
            "select count(*) from public.matriculas where seccion_id = $1 and estado in ('pendiente','activa')");
        cmd.Parameters.AddWithValue(seccion);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<long> ContarMatriculasAsync(Guid alumno)
    {
        await using var cmd = _factory.DatosAcademicos.CreateCommand(
            "select count(*) from public.matriculas where alumno_id = $1");
        cmd.Parameters.AddWithValue(alumno);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}