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
using SchoolManager.API.Identity;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class AuthControllerTests : IClassFixture<AuthControllerTests.ApiFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sin_autenticacion_devuelve_401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_activo_devuelve_200()
    {
        var response = await GetMeAsync("admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Padre_activo_devuelve_200()
    {
        var response = await GetMeAsync("padre");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Respuesta_contiene_solo_id_personaId_y_rol()
    {
        var response = await GetMeAsync("admin");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var propiedades = json.RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray();

        Assert.Equal(["id", "personaId", "rol"], propiedades);
    }

    [Fact]
    public async Task Respuesta_no_expone_datos_internos()
    {
        var response = await GetMeAsync("admin");
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("auth_user_id", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nombre", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("claims", contenido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Identidad_no_resoluble_devuelve_403()
    {
        var response = await GetMeAsync("no-resoluble");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> GetMeAsync(string identidad)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test", identidad);
        return await _client.SendAsync(request);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
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

                services.RemoveAll<IUsuarioActualService>();
                services.AddScoped<IUsuarioActualService, UsuarioActualControlado>();
            });
        }
    }

    private sealed class UsuarioActualControlado : IUsuarioActualService
    {
        public Task<UsuarioActual> ObtenerAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default
        )
        {
            var identidad = principal.FindFirstValue("sub");
            if (identidad == "no-resoluble")
            {
                throw new UnauthorizedAccessException();
            }

            var rol = identidad == "padre" ? "padre" : "admin";
            return Task.FromResult(new UsuarioActual(Guid.NewGuid(), Guid.NewGuid(), rol));
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
                || authorization.Scheme != SchemeName
                || string.IsNullOrWhiteSpace(authorization.Parameter))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim("sub", authorization.Parameter)],
                SchemeName
            );
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
