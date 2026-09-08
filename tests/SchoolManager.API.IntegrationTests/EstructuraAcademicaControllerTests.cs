using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

// Tests de integración de EstructuraAcademicaController (grados, jornadas y
// secciones) contra Postgres real (Testcontainers). Usan el fixture
// mono-institucion (EstructuraAcademicaApiFactory), que reutiliza el bootstrap
// con migraciones 001-024 auto-aplicadas por MigrationRunner. Las RPC de
// configuracion (016/020) resuelven su ambito con resolver_institucion_operacion(NULL),
// valido solo en modo mono (produccion).
//
// Autorizacion en dos capas: la policy .NET exige academico.estructura.*
// (migracion 024; se valida ANTES de tocar DB), que el fixture otorga en memoria
// a las identidades admin (AdminA/AdminB); la RPC revalida
// configuracion.grados/jornadas/secciones.* contra el rol admin sembrado. Cubren
// listar, crear, actualizar, desactivar y reactivar para grados, jornadas y
// secciones, mas 400/401/403/404. Los grados/jornadas no exigen motivo al
// desactivar; las secciones si (motivo obligatorio en el body).
public sealed class EstructuraAcademicaControllerTests : IClassFixture<EstructuraAcademicaApiFactory>
{
    private readonly EstructuraAcademicaApiFactory _factory;

    public EstructuraAcademicaControllerTests(EstructuraAcademicaApiFactory factory)
    {
        _factory = factory;
    }

    private static string SubA => EstructuraAcademicaApiFactory.AdminA.ToString();

    // ----- Grados -----

    [Fact]
    public async Task Listar_grados_autorizado_devuelve_los_sembrados()
    {
        var grado = await _factory.CrearGradoAsync();

        var response = await _factory.CrearCliente(SubA).GetAsync("/api/estructura-academica/grados");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(grado, ids);
    }

    [Fact]
    public async Task Crear_grado_devuelve_201_y_persiste()
    {
        var body = Body(new { nombre = "Primero Basico", orden = 2 });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/estructura-academica/grados", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Aparece al listar (única institución mono).
        var lista = await GetOkAsync(_factory.CrearCliente(SubA), "/api/estructura-academica/grados");
        Assert.Contains("Primero Basico",
            lista.EnumerateArray().Select(x => x.GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task Crear_grado_con_nombre_en_blanco_devuelve_400()
    {
        var body = Body(new { nombre = "   ", orden = 1 });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/estructura-academica/grados", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Actualizar_grado_devuelve_204_y_cambia_nombre_y_orden()
    {
        var grado = await _factory.CrearGradoAsync();
        var body = Body(new { nombre = "Grado renombrado", orden = 5 });

        var response = await _factory.CrearCliente(SubA)
            .PutAsync($"/api/estructura-academica/grados/{grado}", body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var lista = await GetOkAsync(_factory.CrearCliente(SubA), "/api/estructura-academica/grados");
        var fila = lista.EnumerateArray().First(x => x.GetProperty("id").GetGuid() == grado);
        Assert.Equal("Grado renombrado", fila.GetProperty("nombre").GetString());
        Assert.Equal(5, fila.GetProperty("orden").GetInt32());
        Assert.True(fila.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Desactivar_y_reactivar_grado_devuelve_204_y_cambia_activo()
    {
        var grado = await _factory.CrearGradoAsync();
        var client = _factory.CrearCliente(SubA);

        var desactivar = await client.PostAsync(
            $"/api/estructura-academica/grados/{grado}/desactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var listaInactivo = await GetOkAsync(client, "/api/estructura-academica/grados");
        Assert.False(listaInactivo.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == grado)
            .GetProperty("activo").GetBoolean());

        var reactivar = await client.PostAsync(
            $"/api/estructura-academica/grados/{grado}/reactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var listaActivo = await GetOkAsync(client, "/api/estructura-academica/grados");
        Assert.True(listaActivo.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == grado)
            .GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Actualizar_grado_inexistente_devuelve_404()
    {
        var body = Body(new { nombre = "No existe", orden = 1 });

        var response = await _factory.CrearCliente(SubA)
            .PutAsync($"/api/estructura-academica/grados/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ----- Jornadas -----

    [Fact]
    public async Task Listar_jornadas_autorizado_devuelve_las_sembradas()
    {
        var jornada = await _factory.CrearJornadaAsync();

        var response = await _factory.CrearCliente(SubA).GetAsync("/api/estructura-academica/jornadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(jornada, ids);
    }

    [Fact]
    public async Task Crear_jornada_devuelve_201_y_persiste()
    {
        var body = Body(new { nombre = "Tarde" });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/estructura-academica/jornadas", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var lista = await GetOkAsync(_factory.CrearCliente(SubA), "/api/estructura-academica/jornadas");
        Assert.Contains("Tarde",
            lista.EnumerateArray().Select(x => x.GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task Actualizar_jornada_devuelve_204_y_cambia_nombre()
    {
        var jornada = await _factory.CrearJornadaAsync();
        var body = Body(new { nombre = "Jornada ampliada" });

        var response = await _factory.CrearCliente(SubA)
            .PutAsync($"/api/estructura-academica/jornadas/{jornada}", body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var lista = await GetOkAsync(_factory.CrearCliente(SubA), "/api/estructura-academica/jornadas");
        var fila = lista.EnumerateArray().First(x => x.GetProperty("id").GetGuid() == jornada);
        Assert.Equal("Jornada ampliada", fila.GetProperty("nombre").GetString());
        Assert.True(fila.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Desactivar_y_reactivar_jornada_devuelve_204_y_cambia_activo()
    {
        var jornada = await _factory.CrearJornadaAsync();
        var client = _factory.CrearCliente(SubA);

        var desactivar = await client.PostAsync(
            $"/api/estructura-academica/jornadas/{jornada}/desactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var listaInactivo = await GetOkAsync(client, "/api/estructura-academica/jornadas");
        Assert.False(listaInactivo.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == jornada)
            .GetProperty("activo").GetBoolean());

        var reactivar = await client.PostAsync(
            $"/api/estructura-academica/jornadas/{jornada}/reactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var listaActivo = await GetOkAsync(client, "/api/estructura-academica/jornadas");
        Assert.True(listaActivo.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == jornada)
            .GetProperty("activo").GetBoolean());
    }

    // ----- Secciones -----

    [Fact]
    public async Task Crear_seccion_dentro_del_ciclo_devuelve_201_y_persiste()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var grado = await _factory.CrearGradoAsync();
        var jornada = await _factory.CrearJornadaAsync();
        var body = Body(new
        {
            cicloId = ciclo,
            gradoId = grado,
            jornadaId = jornada,
            nombre = "A",
            cupo = 30
        });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/estructura-academica/secciones", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var lista = await GetOkAsync(_factory.CrearCliente(SubA),
            $"/api/estructura-academica/secciones?cicloId={ciclo}");
        Assert.Contains("A",
            lista.EnumerateArray().Select(x => x.GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task Listar_secciones_de_un_ciclo_devuelve_las_sembradas()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var grado = await _factory.CrearGradoAsync();
        var seccion = await _factory.CrearSeccionAsync(ciclo, grado, nombre: "B");

        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/estructura-academica/secciones?cicloId={ciclo}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(seccion, ids);
    }

    [Fact]
    public async Task Listar_secciones_sin_ciclo_id_devuelve_400()
    {
        // El listado de secciones exige acotar por ciclo escolar.
        var response = await _factory.CrearCliente(SubA)
            .GetAsync("/api/estructura-academica/secciones");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Desactivar_seccion_sin_motivo_devuelve_400()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var grado = await _factory.CrearGradoAsync();
        var seccion = await _factory.CrearSeccionAsync(ciclo, grado);

        var response = await _factory.CrearCliente(SubA).PostAsync(
            $"/api/estructura-academica/secciones/{seccion}/desactivar",
            Body(new { motivo = " " }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Desactivar_y_reactivar_seccion_devuelve_204_y_cambia_activo()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var grado = await _factory.CrearGradoAsync();
        var seccion = await _factory.CrearSeccionAsync(ciclo, grado);
        var client = _factory.CrearCliente(SubA);

        var desactivar = await client.PostAsync(
            $"/api/estructura-academica/secciones/{seccion}/desactivar",
            Body(new { motivo = "Baja matricula" }));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var listaInactivo = await GetOkAsync(client,
            $"/api/estructura-academica/secciones?cicloId={ciclo}");
        var fila = listaInactivo.EnumerateArray().First(x => x.GetProperty("id").GetGuid() == seccion);
        Assert.False(fila.GetProperty("activo").GetBoolean());
        Assert.Equal("Baja matricula", fila.GetProperty("motivoDesactivacion").GetString());

        var reactivar = await client.PostAsync(
            $"/api/estructura-academica/secciones/{seccion}/reactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var listaActivo = await GetOkAsync(client,
            $"/api/estructura-academica/secciones?cicloId={ciclo}");
        Assert.True(listaActivo.EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == seccion)
            .GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Actualizar_seccion_devuelve_204_y_cambia_cupo_y_nombre()
    {
        var ciclo = await _factory.CrearCicloAsync();
        var grado = await _factory.CrearGradoAsync();
        var seccion = await _factory.CrearSeccionAsync(ciclo, grado, cupo: 20);
        var body = Body(new
        {
            cicloId = ciclo,
            gradoId = grado,
            nombre = "Seccion B renovada",
            cupo = 25
        });

        var response = await _factory.CrearCliente(SubA)
            .PutAsync($"/api/estructura-academica/secciones/{seccion}", body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var lista = await GetOkAsync(_factory.CrearCliente(SubA),
            $"/api/estructura-academica/secciones?cicloId={ciclo}");
        var fila = lista.EnumerateArray().First(x => x.GetProperty("id").GetGuid() == seccion);
        Assert.Equal("Seccion B renovada", fila.GetProperty("nombre").GetString());
        Assert.Equal(25, fila.GetProperty("cupo").GetInt32());
    }

    // ----- Seguridad / errores -----

    [Fact]
    public async Task Sin_autenticacion_devuelve_401()
    {
        var response = await _factory.CrearCliente(string.Empty)
            .GetAsync("/api/estructura-academica/grados");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Identidad_sin_permiso_devuelve_403()
    {
        // La identidad "sin-permiso" no recibe academico.estructura.* en memoria;
        // la policy .NET la rechaza antes de tocar la DB.
        var response = await _factory.CrearCliente("sin-permiso")
            .GetAsync("/api/estructura-academica/grados");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<JsonElement> GetOkAsync(HttpClient client, string url)
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
