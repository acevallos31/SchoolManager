using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolManager.API.Identity;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests.Infrastructure;

/// <summary>
/// Fixture de integracion para el bloque 019 (configuracion financiera:
/// conceptos financieros y planes de pago). Reutiliza la instalacion de
/// base de datos existente (bootstrap + migraciones 001-018) sobre un
/// Postgres real de Testcontainers y levanta la API en memoria apuntando
/// al mismo Postgres.
///
/// Modo mono-institucion (produccion): se registra una unica institucion
/// activa, de modo que resolver_institucion_operacion(NULL) --padron usado
/// por los controllers de este bloque-- resuelve a esa institucion unica.
/// </summary>
public sealed class ConfiguracionFinancieraApiFactory : IAsyncLifetime
{
    internal static readonly Guid AdminA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    internal static readonly Guid AdminB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private readonly PostgreSqlFixture _db = new();
    private WebApplicationFactory<Program> _web = null!;
    private Guid _institucionA;

    public NpgsqlDataSource Datos => _db.DataSource;
    public Guid InstitucionA => _institucionA;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();

        // Una unica institucion activa => modo mono-institucion (produccion).
        _institucionA = await ScalarGuidAsync(
            "insert into public.instituciones (nombre, activo) values ($1, true) returning id",
            $"Inst Unica {Guid.NewGuid():N}");

        var rolAdmin = await ScalarGuidAsync("select id from public.roles where codigo = 'admin'");
        await CrearUsuarioConRolAsync(AdminA, rolAdmin, _institucionA);
        await CrearUsuarioConRolAsync(AdminB, rolAdmin, _institucionA);

        _web = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:PostgreSQL", _db.ConnectionString);
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
                        _ => { });

                    services.RemoveAll<IUsuarioActualService>();
                    services.AddScoped<IUsuarioActualService, UsuarioActualControlado>();
                });
            });
    }

    public async Task DisposeAsync()
    {
        if (_web is not null) await _web.DisposeAsync();
        await _db.DisposeAsync();
    }

    public HttpClient CrearCliente(string identidad)
    {
        var client = _web.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", identidad);
        return client;
    }

    private async Task CrearUsuarioConRolAsync(Guid authUserId, Guid rolId, Guid institucionId)
    {
        await ExecAsync("insert into public.usuarios (auth_user_id, activo) values ($1, true)", authUserId);
        var usuarioId = await ScalarGuidAsync("select id from public.usuarios where auth_user_id = $1", authUserId);
        await ExecAsync(
            "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)",
            usuarioId, rolId, institucionId);
    }

    private async Task ExecAsync(string sql, params object[] values)
    {
        await using var command = Datos.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values)
    {
        await using var command = Datos.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Permisos que el factory otorga a la identidad admin en memoria.</summary>
    private sealed class UsuarioActualControlado : IUsuarioActualService
    {
        public Task<UsuarioActual> ObtenerAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            var identidad = principal.FindFirstValue("sub");
            var esAdmin = identidad == AdminA.ToString() || identidad == AdminB.ToString();
            return Task.FromResult(new UsuarioActual(
                Guid.NewGuid(),
                Guid.NewGuid(),
                esAdmin ? ["admin"] : [],
                esAdmin
                    ? [
                        "configuracion.conceptos_financieros.ver",
                        "configuracion.conceptos_financieros.crear",
                        "configuracion.conceptos_financieros.editar",
                        "configuracion.conceptos_financieros.desactivar",
                        "configuracion.planes_pago.ver",
                        "configuracion.planes_pago.crear",
                        "configuracion.planes_pago.editar",
                        "configuracion.planes_pago.desactivar"
                      ]
                    : []));
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
                || authorization.Scheme != SchemeName
                || string.IsNullOrWhiteSpace(authorization.Parameter))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim("sub", authorization.Parameter)], SchemeName);
            return Task.FromResult(
                AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}