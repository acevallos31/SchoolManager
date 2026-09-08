using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class ConfiguracionControllerTests(ConfiguracionApiFactory factory)
    : IClassFixture<ConfiguracionApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.PrepararAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static object Datos(string nombre = " Centro actualizado ", string? correo = null) => new
    {
        nombre, nombreCorto = " ", direccion = (string?)null, telefono = (string?)null, correo, logoUrl = (string?)null,
        identificadores = new { rneRequerido = true, identificacionCivilRequerida = false, codigoInternoRequerido = false, tiposIdentificacionPermitidos = new[] { " Identidad " } }
    };

    [Theory]
    [InlineData("GET", "contexto")]
    [InlineData("GET", "institucion")]
    [InlineData("PUT", "modo")]
    [InlineData("POST", "instituciones")]
    [InlineData("PUT", "instituciones/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")]
    public async Task SinSesion_Devuelve401(string metodo, string ruta)
    {
        using var client = factory.Cliente();
        using var request = new HttpRequestMessage(new HttpMethod(metodo), $"/api/configuracion/{ruta}") { Content = JsonContent.Create(Datos()) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Contexto_NoExigePermisoAdministrativo()
    {
        using var client = factory.Cliente(factory.SinPermisos);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/contexto");
        Assert.False(json.GetProperty("multiplesInstituciones").GetBoolean());
        Assert.Equal(factory.InstitucionA, json.GetProperty("institucion").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Contexto_IdentidadSinUsuarioInterno_Devuelve403()
    {
        using var client = factory.Cliente(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/configuracion/contexto")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Configuracion_PermiteVerOEditar(bool editor)
    {
        using var client = factory.Cliente(editor ? factory.EditorA : factory.LectorA);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/institucion");
        Assert.Equal(factory.InstitucionA, json.GetProperty("institucion").GetProperty("id").GetGuid());
        Assert.False(json.GetProperty("identificadores").GetProperty("rneRequerido").GetBoolean());
    }

    [Theory]
    [InlineData("GET", "institucion")]
    [InlineData("PUT", "modo")]
    [InlineData("POST", "instituciones")]
    [InlineData("PUT", "instituciones/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")]
    public async Task SinPermisos_Devuelve403(string metodo, string ruta)
    {
        using var client = factory.Cliente(factory.SinPermisos);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), $"/api/configuracion/{ruta}") { Content = JsonContent.Create(Datos()) };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Editar_PersisteInstitucionEIdentificadoresNormalizadosPorRpc()
    {
        using var client = factory.Cliente(factory.EditorA);
        var response = await client.PutAsJsonAsync($"/api/configuracion/instituciones/{factory.InstitucionA}", Datos());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/institucion");
        Assert.Equal("Centro actualizado", json.GetProperty("institucion").GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("institucion").GetProperty("nombreCorto").ValueKind);
        Assert.True(json.GetProperty("identificadores").GetProperty("rneRequerido").GetBoolean());
        Assert.Equal("identidad", json.GetProperty("identificadores").GetProperty("tiposIdentificacionPermitidos")[0].GetString());
    }

    [Fact]
    public async Task Crear_DesdeConfiguracionVacia()
    {
        await factory.SinInstitucionesAsync();
        using var client = factory.Cliente(factory.Admin);
        var vacio = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/institucion");
        Assert.Equal(JsonValueKind.Null, vacio.GetProperty("institucion").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/configuracion/instituciones", Datos())).StatusCode);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/contexto");
        Assert.Equal("Centro actualizado", json.GetProperty("institucion").GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task Crear_SingleExistente_ConservaSM004()
    {
        using var client = factory.Cliente(factory.Admin);
        await ErrorAsync(await client.PostAsJsonAsync("/api/configuracion/instituciones", Datos()), HttpStatusCode.BadRequest, "SM004");
    }

    [Fact]
    public async Task Modo_ActualizaContextoYConservaRechazoRpc()
    {
        using var client = factory.Cliente(factory.Admin);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/configuracion/modo", new { multiplesInstituciones = true })).StatusCode);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/contexto");
        Assert.True(json.GetProperty("multiplesInstituciones").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("institucion").ValueKind);
        await factory.ModoMultiAsync();
        await ErrorAsync(await client.PutAsJsonAsync("/api/configuracion/modo", new { multiplesInstituciones = false }), HttpStatusCode.BadRequest, "SM002");
        json = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/contexto");
        Assert.True(json.GetProperty("multiplesInstituciones").GetBoolean());
    }

    [Fact]
    public async Task Multi_ExigeContextoYDbRechazaOtraInstitucion()
    {
        await factory.ModoMultiAsync();
        using var client = factory.Cliente(factory.EditorA);
        await ErrorAsync(await client.GetAsync("/api/configuracion/institucion"), HttpStatusCode.BadRequest, "SM003");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/configuracion/institucion?institucionId={factory.InstitucionA}")).StatusCode);
        await ErrorAsync(await client.GetAsync($"/api/configuracion/institucion?institucionId={factory.InstitucionB}"), HttpStatusCode.Forbidden, "42501");
        await ErrorAsync(await client.PutAsJsonAsync($"/api/configuracion/instituciones/{factory.InstitucionB}", Datos()), HttpStatusCode.Forbidden, "42501");
        using var admin = factory.Cliente(factory.Admin);
        var json = await admin.GetFromJsonAsync<JsonElement>($"/api/configuracion/institucion?institucionId={factory.InstitucionB}");
        Assert.NotEqual("Centro actualizado", json.GetProperty("institucion").GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task Editar_Inexistente_Devuelve404()
    {
        using var client = factory.Cliente(factory.Admin);
        await ErrorAsync(await client.PutAsJsonAsync($"/api/configuracion/instituciones/{Guid.NewGuid()}", Datos()), HttpStatusCode.NotFound, "P0002");
    }

    [Fact]
    public async Task CorreoInvalido_NoModificaInstitucion()
    {
        using var client = factory.Cliente(factory.EditorA);
        await ErrorAsync(await client.PutAsJsonAsync($"/api/configuracion/instituciones/{factory.InstitucionA}", Datos(correo: "invalido")), HttpStatusCode.BadRequest, "22023");
        var json = await client.GetFromJsonAsync<JsonElement>("/api/configuracion/institucion");
        Assert.NotEqual("Centro actualizado", json.GetProperty("institucion").GetProperty("nombre").GetString());
        Assert.False(json.GetProperty("identificadores").GetProperty("rneRequerido").GetBoolean());
    }

    [Fact]
    public async Task DatosIncompletos_Devuelven400()
    {
        using var client = factory.Cliente(factory.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/configuracion/modo", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/configuracion/instituciones", new { nombre = "Centro" })).StatusCode);
    }

    [Fact]
    public async Task Contexto_SinInstitucion_ConservaSM001()
    {
        await factory.SinInstitucionesAsync();
        using var client = factory.Cliente(factory.Admin);
        await ErrorAsync(await client.GetAsync("/api/configuracion/contexto"), HttpStatusCode.BadRequest, "SM001");
    }

    private static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, json.GetProperty("code").GetString());
    }
}
