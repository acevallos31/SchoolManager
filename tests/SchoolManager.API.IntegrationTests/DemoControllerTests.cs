using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManager.API.Services;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class DemoControllerTests : IClassFixture<DemoControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public DemoControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sin_autenticacion_devuelve_401()
    {
        var response = await _client.PostAsync("/api/demo/session", null);
        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
    }

    [Fact]
    public async Task Usuario_permanente_no_puede_iniciar_demo()
    {
        var response = await PostAsync("/api/demo/session", "user");
        await AssertStatusAsync(HttpStatusCode.Forbidden, response);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "DEMO_REQUIERE_SESION_ANONIMA",
            json.RootElement.GetProperty("codigo").GetString()
        );
    }

    [Fact]
    public async Task Usuario_anonimo_crea_o_reutiliza_sandbox()
    {
        var response = await PostAsync("/api/demo/session", "anon");
        await AssertStatusAsync(HttpStatusCode.OK, response);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiFactory.SessionId, json.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal(ApiFactory.SandboxId, json.RootElement.GetProperty("institucionId").GetGuid());
        Assert.False(json.RootElement.GetProperty("reused").GetBoolean());
    }

    [Fact]
    public async Task Usuario_anonimo_puede_reiniciar_sandbox()
    {
        var response = await PostAsync("/api/demo/session/reset", "anon");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiFactory.ResetSessionId, json.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal(ApiFactory.ResetSandboxId, json.RootElement.GetProperty("institucionId").GetGuid());
    }

    [Fact]
    public async Task Feature_flag_apagado_oculta_endpoint_demo()
    {
        using var disabled = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("Demo:Enabled", "false"))
            .CreateClient();

        using var request = CrearRequest("/api/demo/session", "anon");
        var response = await disabled.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.NotFound, response);
    }

    private static async Task AssertStatusAsync(
        HttpStatusCode expected,
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expected,
            $"Expected {(int)expected} {expected}, actual {(int)response.StatusCode} {response.StatusCode}. Body: {body}"
        );
    }

    private async Task<HttpResponseMessage> PostAsync(string path, string tipo)
    {
        using var request = CrearRequest(path, tipo);
        return await _client.SendAsync(request);
    }

    private static HttpRequestMessage CrearRequest(string path, string tipo)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.SchemeName,
            $"{tipo}-{ApiFactory.AuthUserId:D}"
        );
        return request;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public static readonly Guid AuthUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid TemplateId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid SessionId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        public static readonly Guid SandboxId = Guid.Parse("40000000-0000-0000-0000-000000000004");
        public static readonly Guid ResetSessionId = Guid.Parse("50000000-0000-0000-0000-000000000005");
        public static readonly Guid ResetSandboxId = Guid.Parse("60000000-0000-0000-0000-000000000006");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options => options.SingleLine = true);
                logging.SetMinimumLevel(LogLevel.Debug);
            });

            builder.UseSetting("Observability:ExposeExceptionTypeForTests", "true");
            builder.UseSetting("Demo:Enabled", "true");
            builder.UseSetting("Demo:TemplateInstitutionId", TemplateId.ToString());

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                        options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { }
                    );

                services.RemoveAll<IDemoSandboxService>();
                services.AddSingleton<IDemoSandboxService, DemoSandboxControlado>();
            });
        }
    }

    private sealed class DemoSandboxControlado : IDemoSandboxService
    {
        public Task<DemoSessionResult> CrearOReutilizarAsync(
            Guid authUserId,
            Guid templateInstitutionId,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(ApiFactory.AuthUserId, authUserId);
            Assert.Equal(ApiFactory.TemplateId, templateInstitutionId);
            return Task.FromResult(new DemoSessionResult(
                ApiFactory.SessionId,
                ApiFactory.SandboxId,
                false
            ));
        }

        public Task<DemoSessionResult> ReiniciarAsync(
            Guid authUserId,
            Guid templateInstitutionId,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(ApiFactory.AuthUserId, authUserId);
            Assert.Equal(ApiFactory.TemplateId, templateInstitutionId);
            return Task.FromResult(new DemoSessionResult(
                ApiFactory.ResetSessionId,
                ApiFactory.ResetSandboxId,
                false
            ));
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "DemoTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
                || authorization.Scheme != SchemeName
                || string.IsNullOrWhiteSpace(authorization.Parameter))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parameter = authorization.Parameter;
            var anonymous = parameter.StartsWith("anon-", StringComparison.Ordinal);
            var prefixLength = anonymous ? "anon-".Length : "user-".Length;
            if (parameter.Length <= prefixLength
                || !Guid.TryParse(parameter[prefixLength..], out var authUserId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Credencial de prueba inválida."));
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim("sub", authUserId.ToString()),
                    new Claim("is_anonymous", anonymous ? "true" : "false")
                ],
                SchemeName
            );
            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                SchemeName
            );
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
