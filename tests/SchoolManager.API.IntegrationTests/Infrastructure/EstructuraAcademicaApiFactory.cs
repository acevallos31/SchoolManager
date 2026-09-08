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
using NpgsqlTypes;
using SchoolManager.API.Identity;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests.Infrastructure;

/// <summary>
/// Fixture de integracion para el bloque EstructuraAcademicaController
/// (grados, jornadas y secciones). Reutiliza la instalacion de base de datos
/// (bootstrap + migraciones 001-024 auto-aplicadas por MigrationRunner) sobre
/// un Postgres real de Testcontainers y levanta la API en memoria contra el
/// mismo Postgres.
///
/// Modo MONO-institucion (produccion): se registra una unica institucion activa.
/// Las RPC de configuracion (016/020) resuelven su ambito con
/// resolver_institucion_operacion(NULL), que solo es valido en modo mono.
/// Por eso este dominio se prueba en mono, igual que el bloque 030D de ciclos.
///
/// La autorizacion .NET (academico.estructura.*) se valida en memoria; la DB
/// (RLS + permisos configuracion.grados/jornadas/secciones.* en las RPC 016)
/// actua como segunda capa. La migracion 024 inserta academico.estructura.* en
/// el catalogo y los otorga al rol 'admin'.
/// </summary>
public sealed class EstructuraAcademicaApiFactory : IAsyncLifetime
{
    internal static readonly Guid AdminA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    internal static readonly Guid AdminB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    // Identificadores fijos y deterministas para las filas sembradas por SQL.
    // Al fijar el id podemos consultarlas directamente en los asserts de los tests.
    internal static readonly Guid CicloSembrado = Guid.Parse("10101010-1010-4101-8101-101010101010");
    internal static readonly Guid GradoSembrado = Guid.Parse("20202020-2020-4202-8202-202020202020");
    internal static readonly Guid JornadaSembrada = Guid.Parse("30303030-3030-4303-8303-303030303030");
    internal static readonly Guid SeccionSembrada = Guid.Parse("40404040-4040-4404-8404-404040404040");

    private readonly PostgreSqlFixture _db = new();
    private WebApplicationFactory<Program> _web = null!;
    private Guid _institucionA;

    public NpgsqlDataSource Datos => _db.DataSource;
    public Guid InstitucionA => _institucionA;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();

        _institucionA = await ScalarGuidAsync(
            "insert into public.instituciones (nombre, activo) values ($1, true) returning id",
            $"Inst Unica {Guid.NewGuid():N}");

        var rolAdmin = await ScalarGuidAsync("select id from public.roles where codigo = 'admin'");
        await CrearUsuarioConRolAsync(AdminA, rolAdmin, _institucionA);
        await CrearUsuarioConRolAsync(AdminB, rolAdmin, _institucionA);

        await SembrarEstructuraBaseAsync();

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

    // ----- Seeders del modelo academico (insert directo; el controller prueba
    // el CRUD vía RPC, no la siembra) -----

    // Ciclo, grado, jornada y seccion pre-existentes bajo la institucionA:
    // los tests de listado y de estado necesitan filas sembradas de antemano.
    private async Task SembrarEstructuraBaseAsync()
    {
        await ExecAsync(
            "insert into public.ciclos_escolares (id, institucion_id, nombre, fecha_inicio, fecha_fin) " +
            "values ($1, $2, $3, current_date, current_date + 365)",
            CicloSembrado, _institucionA, "Ciclo Estructura Sembrado");

        await ExecAsync(
            "insert into public.grados (id, institucion_id, nombre, orden) values ($1, $2, $3, $4)",
            GradoSembrado, _institucionA, "Grado Sembrado", 1);

        await ExecAsync(
            "insert into public.jornadas (id, institucion_id, nombre) values ($1, $2, $3)",
            JornadaSembrada, _institucionA, "Jornada Sembrada");

        await ExecAsync(
            "insert into public.secciones (id, institucion_id, ciclo_id, grado_id, jornada_id, nombre, cupo) " +
            "values ($1, $2, $3, $4, $5, $6, $7)",
            SeccionSembrada, _institucionA, CicloSembrado, GradoSembrado, JornadaSembrada, "Seccion Sembrada", 30);
    }

    public Task<Guid> CrearCicloAsync(string? nombre = null) => ScalarGuidAsync(
        "insert into public.ciclos_escolares (institucion_id, nombre, fecha_inicio, fecha_fin) " +
        "values ($1, $2, current_date, current_date + 365) returning id",
        _institucionA, nombre ?? $"Ciclo {Guid.NewGuid():N}");

    public Task<Guid> CrearGradoAsync(string? nombre = null, int orden = 1) => ScalarGuidAsync(
        "insert into public.grados (institucion_id, nombre, orden) " +
        "values ($1, $2, $3) returning id",
        _institucionA, nombre ?? $"Grado {Guid.NewGuid():N}", orden);

    public Task<Guid> CrearJornadaAsync(string? nombre = null) => ScalarGuidAsync(
        "insert into public.jornadas (institucion_id, nombre) " +
        "values ($1, $2) returning id",
        _institucionA, nombre ?? $"Jornada {Guid.NewGuid():N}");

    public Task<Guid> CrearSeccionAsync(
        Guid cicloId, Guid gradoId, Guid? jornadaId = null,
        string? nombre = null, int? cupo = null) => ScalarGuidAsync(
        "insert into public.secciones (institucion_id, ciclo_id, grado_id, jornada_id, nombre, cupo) " +
        "values ($1, $2, $3, $4, $5, $6) returning id",
        _institucionA, cicloId, gradoId,
        jornadaId ?? (object)DBNull.Value,
        nombre ?? $"Seccion {Guid.NewGuid():N}",
        cupo ?? (object)DBNull.Value);

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

    /// <summary>
    /// Permisos que el factory otorga a la identidad admin en memoria:
    /// academico.estructura.ver/editar/desactivar (migracion 024). Otras
    /// identidades no reciben rol ni permisos (la RPC 016 aun las rechazaria
    /// por DB si no fueran admin, segunda capa de defensa).
    /// </summary>
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
                        "academico.estructura.ver",
                        "academico.estructura.editar",
                        "academico.estructura.desactivar"
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
