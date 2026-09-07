using System.Net;
using System.Text;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

// Tests de integración de AlumnosController contra una instancia real de
// Postgres (Testcontainers). Siguen el patrón de MatriculasControllerTests y
// reutilizan el fixture compartido MatriculasApiFactory (modo multiinstitucion).
//
// La autorización .NET (policies de Permisos.Alumnos.*) se valida ANTES de
// invocar la RPC; la DB (RLS + usuario_tiene_permiso_actual en las RPC)
// actúa como segunda capa de aislamiento. Cubren: listado autorizado,
// búsqueda paginada, detalle, 404, crear, desactivar, reactivar, 401 sin
// autenticación, 403 sin permiso y aislamiento institucional.
public sealed class AlumnosControllerTests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public AlumnosControllerTests(MatriculasApiFactory factory)
    {
        _factory = factory;
    }

    private string SubA => MatriculasApiFactory.AdminA.ToString();
    private string SubB => MatriculasApiFactory.AdminB.ToString();

    [Fact]
    public async Task Listado_autorizado_incluye_alumnos_de_la_institucion()
    {
        var institucion = _factory.InstitucionA;
        var alumno = await _factory.CrearAlumnoAsync(institucion);

        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/alumnos?institucionId={institucion}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToArray();

        // La DB del fixture es compartida: se comprueba que el alumno sembrado
        // aparece, sin asumir un recuento exacto.
        Assert.Contains(alumno, ids);
    }

    [Fact]
    public async Task Busqueda_paginada_devuelve_pagina_y_total()
    {
        var institucion = _factory.InstitucionA;
        for (var i = 0; i < 5; i++)
        {
            await _factory.CrearAlumnoAsync(institucion);
        }

        var client = _factory.CrearCliente(SubA);
        var url = $"/api/alumnos?institucionId={institucion}";

        var j1 = await GetJsonAsync(client, $"{url}&page=1&pageSize=2");
        Assert.Equal(2, j1.GetProperty("items").GetArrayLength());
        Assert.Equal(1, j1.GetProperty("page").GetInt32());
        Assert.Equal(2, j1.GetProperty("pageSize").GetInt32());
        // Al menos los 5 sembrados (la DB compartida puede tener más).
        Assert.True(j1.GetProperty("totalItems").GetInt64() >= 5);
        Assert.True(j1.GetProperty("totalPages").GetInt32() >= 3);

        var j2 = await GetJsonAsync(client, $"{url}&page=2&pageSize=2");
        Assert.Equal(2, j2.GetProperty("items").GetArrayLength());
        var primerIdPag1 = j1.GetProperty("items")[0].GetProperty("id").GetGuid();
        var idsPag2 = j2.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.DoesNotContain(primerIdPag1, idsPag2);
    }

    [Fact]
    public async Task Detalle_existente_devuelve_200()
    {
        var institucion = _factory.InstitucionA;
        var alumno = await _factory.CrearAlumnoAsync(institucion);

        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/alumnos/{alumno}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(alumno, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(institucion, json.RootElement.GetProperty("institucionId").GetGuid());
        Assert.Equal("activo", json.RootElement.GetProperty("estado").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.GetProperty("nombreCompleto").GetString()));
    }

    [Fact]
    public async Task Detalle_inexistente_devuelve_404()
    {
        var response = await _factory.CrearCliente(SubA)
            .GetAsync($"/api/alumnos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Crear_devuelve_201_y_persiste_el_alumno()
    {
        var institucion = _factory.InstitucionA;
        var numero = $"V{Guid.NewGuid():N}";
        var body = Body(new
        {
            institucionId = institucion,
            nombres = "Ana",
            apellidos = "Perez Test",
            tipoIdentificacion = "identidad",
            numeroIdentificacion = numero,
            fechaNacimiento = (DateOnly?)null,
            rne = (string?)null,
            codigoInterno = (string?)null
        });

        var response = await _factory.CrearCliente(SubA)
            .PostAsync("/api/alumnos", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var creado = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = creado.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);

        // Se puede leer el alumno recién creado por su id.
        var detalle = await _factory.CrearCliente(SubA).GetAsync($"/api/alumnos/{id}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        using var detalleJson = JsonDocument.Parse(await detalle.Content.ReadAsStringAsync());
        Assert.Equal("Ana", detalleJson.RootElement.GetProperty("nombreCompleto")
            .GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        Assert.Equal("activo", detalleJson.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Crear_documento_duplicado_devuelve_409()
    {
        var institucion = _factory.InstitucionA;
        var numero = $"V{Guid.NewGuid():N}";
        var body = Body(new
        {
            institucionId = institucion,
            nombres = "Maria",
            apellidos = "Duplicado Test",
            tipoIdentificacion = "identidad",
            numeroIdentificacion = numero,
            fechaNacimiento = (DateOnly?)null,
            rne = (string?)null,
            codigoInterno = (string?)null
        });

        var client = _factory.CrearCliente(SubA);
        var primero = await client.PostAsync("/api/alumnos", body);
        var duplicado = await client.PostAsync("/api/alumnos", body);

        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Desactivar_devuelve_204_y_marca_inactivo()
    {
        var institucion = _factory.InstitucionA;
        var alumno = await _factory.CrearAlumnoAsync(institucion);

        var response = await _factory.CrearCliente(SubA)
            .PostAsync($"/api/alumnos/{alumno}/desactivar",
                Body(new { motivo = "Baja voluntaria" }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detalle = await GetJsonAsync(_factory.CrearCliente(SubA), $"/api/alumnos/{alumno}");
        Assert.Equal("inactivo", detalle.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Desactivar_sin_motivo_devuelve_400()
    {
        var institucion = _factory.InstitucionA;
        var alumno = await _factory.CrearAlumnoAsync(institucion);

        var response = await _factory.CrearCliente(SubA)
            .PostAsync($"/api/alumnos/{alumno}/desactivar", Body(new { motivo = " " }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reactivar_devuelve_204_y_marca_activo()
    {
        var institucion = _factory.InstitucionA;
        var alumno = await _factory.CrearAlumnoAsync(institucion);

        var client = _factory.CrearCliente(SubA);
        var desactivar = await client.PostAsync($"/api/alumnos/{alumno}/desactivar",
            Body(new { motivo = "Baja temporal" }));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var reactivar = await client.PostAsync($"/api/alumnos/{alumno}/reactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var detalle = await GetJsonAsync(client, $"/api/alumnos/{alumno}");
        Assert.Equal("activo", detalle.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Sin_autenticacion_devuelve_401()
    {
        // Cabecera de autorización "Test" con parámetro vacío => el TestAuthHandler
        // devuelve NoResult (no autenticado) => challenge 401.
        var response = await _factory.CrearCliente(string.Empty)
            .GetAsync($"/api/alumnos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sin_permiso_devuelve_403()
    {
        // Identidad desconocida: la policy de Permisos.Alumnos.Ver no se cumple.
        var response = await _factory.CrearCliente("sin-permiso")
            .GetAsync($"/api/alumnos?institucionId={_factory.InstitucionA}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Aislamiento_multiinstitucion_no_filtra_datos_de_otra_institucion()
    {
        var alumnoA = await _factory.CrearAlumnoAsync(_factory.InstitucionA);
        var alumnoB = await _factory.CrearAlumnoAsync(_factory.InstitucionB);

        // AdminA solo tiene rol/permiso en la institucion A.
        using (var clienteA = _factory.CrearCliente(SubA))
        {
            // Detalle de un alumno de B: no se filtra ni cae en 500.
            var detalleB = await clienteA.GetAsync($"/api/alumnos/{alumnoB}");
            Assert.Equal(HttpStatusCode.NotFound, detalleB.StatusCode);

            // Listado de B: vacío, sin fugas.
            var listaB = await clienteA.GetAsync(
                $"/api/alumnos?institucionId={_factory.InstitucionB}");
            Assert.Equal(HttpStatusCode.OK, listaB.StatusCode);
            using var listaBJson = JsonDocument.Parse(await listaB.Content.ReadAsStringAsync());
            Assert.Empty(listaBJson.RootElement.EnumerateArray());

            // En A sí ve sus propios alumnos.
            var listaA = await clienteA.GetAsync(
                $"/api/alumnos?institucionId={_factory.InstitucionA}");
            Assert.Equal(HttpStatusCode.OK, listaA.StatusCode);
            using var listaAJson = JsonDocument.Parse(await listaA.Content.ReadAsStringAsync());
            Assert.Contains(alumnoA, listaAJson.RootElement.EnumerateArray()
                .Select(x => x.GetProperty("id").GetGuid()));
        }

        // AdminB (rol en B) sí puede leer su alumno y su listado.
        using (var clienteB = _factory.CrearCliente(SubB))
        {
            var detalleB = await clienteB.GetAsync($"/api/alumnos/{alumnoB}");
            Assert.Equal(HttpStatusCode.OK, detalleB.StatusCode);

            var listaB = await GetJsonAsync(clienteB,
                $"/api/alumnos?institucionId={_factory.InstitucionB}");
            Assert.Contains(alumnoB, listaB.EnumerateArray()
                .Select(x => x.GetProperty("id").GetGuid()));
        }
    }

    private async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
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
