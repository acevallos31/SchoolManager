using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class DebugModeControllerTests(ConfiguracionApiFactory factory)
    : IClassFixture<ConfiguracionApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.PrepararAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SinSesion_Devuelve401()
    {
        using var client = factory.Cliente();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/configuracion/debug")).StatusCode);
    }

    [Fact]
    public async Task SinPermiso_Devuelve403()
    {
        using var client = factory.Cliente(factory.SinPermisos);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/configuracion/debug")).StatusCode);
    }

    [Fact]
    public async Task Admin_PuedeHabilitarYDeshabilitarDebugTemporal()
    {
        using var client = factory.Cliente(factory.Admin);

        var activar = await client.PutAsJsonAsync("/api/configuracion/debug",
            new { habilitado = true, minutos = 30 });
        Assert.Equal(HttpStatusCode.OK, activar.StatusCode);
        var activo = await activar.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(activo.GetProperty("habilitado").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, activo.GetProperty("expiraEn").ValueKind);
        Assert.True(activar.Headers.Contains("X-Request-ID"));

        var desactivar = await client.PutAsJsonAsync("/api/configuracion/debug",
            new { habilitado = false, minutos = 30 });
        Assert.Equal(HttpStatusCode.OK, desactivar.StatusCode);
        var inactivo = await desactivar.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(inactivo.GetProperty("habilitado").GetBoolean());
    }

    [Fact]
    public async Task DuracionFueraDeRango_Devuelve400()
    {
        using var client = factory.Cliente(factory.Admin);
        var response = await client.PutAsJsonAsync("/api/configuracion/debug",
            new { habilitado = true, minutos = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DetalleTecnico_SoloApareceConDebugActivoYPermiso()
    {
        using var admin = factory.Cliente(factory.Admin);
        await factory.SinInstitucionesAsync();

        await admin.PutAsJsonAsync("/api/configuracion/debug",
            new { habilitado = false, minutos = 30 });
        var normal = await admin.GetAsync("/api/configuracion/contexto");
        var normalJson = await normal.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(normalJson.TryGetProperty("debug", out _));
        Assert.True(normalJson.TryGetProperty("requestId", out _));

        var activar = await admin.PutAsJsonAsync("/api/configuracion/debug",
            new { habilitado = true, minutos = 30 });
        Assert.Equal(HttpStatusCode.OK, activar.StatusCode);

        var debug = await admin.GetAsync("/api/configuracion/contexto");
        var debugJson = await debug.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(debugJson.TryGetProperty("debug", out var detalle));
        Assert.Equal("SM001", detalle.GetProperty("sqlState").GetString());
        Assert.Equal("PostgreSQL/RPC", detalle.GetProperty("source").GetString());
        Assert.False(string.IsNullOrWhiteSpace(detalle.GetProperty("requestId").GetString()));

        await admin.PutAsJsonAsync("/api/configuracion/debug",
            new { habilitado = false, minutos = 30 });
    }
}
