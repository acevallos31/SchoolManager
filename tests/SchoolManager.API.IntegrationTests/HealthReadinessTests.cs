using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

/// <summary>
/// Contrato de observabilidad (deuda #5):
///   GET /health        -> liveness: 200 siempre que el proceso este vivo.
///   GET /health/ready  -> readiness: 200 si PostgreSQL responde (SELECT 1);
///                         503 si la base esta caida. No expone secretos.
/// Ambos endpoints son anonimos (sin auth) para que un orquestador/balancer
/// de carga pueda sondearlos sin credenciales.
/// </summary>
public sealed class HealthReadinessTests : IClassFixture<HealthReadinessTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthReadinessTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Liveness_devuelve_200_con_estado_ok()
    {
        var client = _factory.CrearCliente();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("SchoolManager.API", json.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Liveness_no_depende_de_la_base_devuelve_200_aun_con_db_caida()
    {
        var client = _factory.CrearClienteConDbCaida();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_devuelve_200_cuando_postgres_responde()
    {
        var client = _factory.CrearCliente();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ready", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("ok", json.RootElement.GetProperty("database").GetString());
    }

    [Fact]
    public async Task Readiness_devuelve_503_cuando_postgres_esta_caido()
    {
        var client = _factory.CrearClienteConDbCaida();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_ready", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("unavailable", json.RootElement.GetProperty("database").GetString());
    }

    [Fact]
    public async Task Readiness_no_expone_connection_string_ni_secretos()
    {
        var client = _factory.CrearClienteConDbCaida();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Levanta Postgres real (bootstrap + migraciones) y la API apuntando a el.
    /// Para simular una caida controlada de la DB, expone un cliente cuyo host
    /// web usa un datasource que apunta a un puerto cerrado (sin tocar la base
    /// real de otros tests).
    /// </summary>
    public sealed class ApiFactory : IAsyncLifetime
    {
        private readonly PostgreSqlFixture _db = new();
        private WebApplicationFactory<Program> _webOk = null!;
        private WebApplicationFactory<Program> _webDbCaida = null!;

        // Puerto cerrado deliberadamente: fallo de conexion inmediato.
        private const string CadenaDbCaida =
            "Host=127.0.0.1;Port=1;Database=inexistente;Username=postgres;Password=postgres;Timeout=1";

        public async Task InitializeAsync()
        {
            await _db.InitializeAsync();
            _webOk = ConstruirWeb(_db.ConnectionString);
            _webDbCaida = ConstruirWeb(CadenaDbCaida);
        }

        public async Task DisposeAsync()
        {
            if (_webOk is not null) await _webOk.DisposeAsync();
            if (_webDbCaida is not null) await _webDbCaida.DisposeAsync();
            await _db.DisposeAsync();
        }

        public HttpClient CrearCliente() => _webOk.CreateClient();

        public HttpClient CrearClienteConDbCaida() => _webDbCaida.CreateClient();

        private static WebApplicationFactory<Program> ConstruirWeb(string connectionString)
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:PostgreSQL", connectionString);
            });
        }
    }
}
