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
/// Fixture de integracion del Bloque 021 (pagos/cobranza). Aplica bootstrap +
/// migraciones 001-021 sobre un Postgres real de Testcontainers y levanta la API
/// en memoria contra ese mismo Postgres. Los admins en memoria cargan permisos de
/// cargos (para generar los cargos via endpoint) y de pagos (ver/registrar/anular).
/// El factory siembra la cadena academica y un plan de pago de dos cuotas
/// (500 y 900), listo para asignar/generar cargos en los tests.
/// </summary>
public sealed class PagosApiFactory : IAsyncLifetime
{
    internal static readonly Guid AdminA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    internal static readonly Guid AdminB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private readonly PostgreSqlFixture _db = new();
    private WebApplicationFactory<Program> _web = null!;
    private Guid _institucionA;
    private Guid _institucionB;

    public NpgsqlDataSource Datos => _db.DataSource;
    public Guid InstitucionA => _institucionA;
    public Guid InstitucionB => _institucionB;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();

        await ExecAsync(
            "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");

        _institucionA = await ScalarGuidAsync(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Inst A {Guid.NewGuid():N}");
        _institucionB = await ScalarGuidAsync(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Inst B {Guid.NewGuid():N}");

        var rolAdmin = await ScalarGuidAsync("select id from public.roles where codigo = 'admin'");
        await CrearUsuarioConRolAsync(AdminA, rolAdmin, _institucionA);
        await CrearUsuarioConRolAsync(AdminB, rolAdmin, _institucionB);

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

    /// <summary>Siembra matricula + plan de 2 cuotas (500 y 900) sin asignar.</summary>
    public async Task<ContextoAcademico> SembrarContextoAsync(Guid institucion)
    {
        var cicloInicio = DateOnly.FromDateTime(DateTime.Today);
        var ciclo = await ScalarGuidAsync(
            "insert into public.ciclos_escolares (institucion_id, nombre, fecha_inicio, fecha_fin) values ($1,$2,$3,$4) returning id",
            institucion, $"Ciclo {Guid.NewGuid():N}", cicloInicio, cicloInicio.AddDays(180));
        var grado = await ScalarGuidAsync(
            "insert into public.grados (nombre, orden, institucion_id) values ($1, 0, $2) returning id",
            $"Grado {Guid.NewGuid():N}", institucion);
        var seccion = await ScalarGuidAsync(
            "insert into public.secciones (nombre, institucion_id, ciclo_id, grado_id) values ($1,$2,$3,$4) returning id",
            $"Seccion {Guid.NewGuid():N}", institucion, ciclo, grado);
        var periodo = await ScalarGuidAsync(
            "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1,$2,$3,$4) returning id",
            ciclo, $"Periodo {Guid.NewGuid():N}", cicloInicio, cicloInicio.AddDays(180));
        var persona = await ScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Alumno', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var alumno = await ScalarGuidAsync(
            "insert into public.alumnos (persona_id, institucion_id) values ($1,$2) returning id",
            persona, institucion);
        var matricula = await ScalarGuidAsync(
            "insert into public.matriculas (alumno_id, institucion_id, ciclo_id, seccion_id, periodo_matricula_id) values ($1,$2,$3,$4,$5) returning id",
            alumno, institucion, ciclo, seccion, periodo);

        var plan = await CrearPlanAsync(institucion);

        return new ContextoAcademico(institucion, ciclo, alumno, matricula, plan, cicloInicio);
    }

    private async Task<Guid> CrearPlanAsync(Guid institucion)
    {
        var concepto = await ScalarGuidAsync(
            "insert into public.conceptos_financieros (institucion_id, nombre, monto) values ($1,$2,0) returning id",
            institucion, $"Colegiatura {Guid.NewGuid():N}");
        var plan = await ScalarGuidAsync(
            "insert into public.planes_pago (institucion_id, nombre) values ($1,$2) returning id",
            institucion, $"Plan {Guid.NewGuid():N}");
        await ExecAsync(
            "insert into public.plan_cuotas (plan_id, orden, concepto_id, descripcion, monto, vencimiento_dias) values ($1,$2,$3,$4,$5,$6)",
            plan, 0, concepto, "Colegiatura", 500m, 30);
        await ExecAsync(
            "insert into public.plan_cuotas (plan_id, orden, concepto_id, descripcion, monto, vencimiento_dias) values ($1,$2,$3,$4,$5,$6)",
            plan, 1, concepto, "Mensualidad 2", 900m, 60);
        return plan;
    }

    private async Task CrearUsuarioConRolAsync(Guid authUserId, Guid rolId, Guid institucionId)
    {
        await ExecAsync(
            "insert into public.usuarios (auth_user_id, activo) values ($1, true)", authUserId);
        var usuarioId = await ScalarGuidAsync(
            "select id from public.usuarios where auth_user_id = $1", authUserId);
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

    public sealed record ContextoAcademico(
        Guid InstitucionId,
        Guid CicloId,
        Guid AlumnoId,
        Guid MatriculaId,
        Guid PlanId,
        DateOnly CicloInicio);

    /// <summary>Permisos que el factory otorga a los admins en memoria (cargos + pagos).</summary>
    private sealed class UsuarioActualControlado : IUsuarioActualService
    {
        public Task<UsuarioActual> ObtenerAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            var identidad = principal.FindFirstValue("sub");
            var esAdmin = identidad == PagosApiFactory.AdminA.ToString()
                || identidad == PagosApiFactory.AdminB.ToString();
            return Task.FromResult(new UsuarioActual(
                Guid.NewGuid(),
                Guid.NewGuid(),
                esAdmin ? ["admin"] : [],
                esAdmin
                    ? [
                        "academico.cargos.ver",
                        "academico.cargos.generar",
                        "academico.cargos.anular",
                        "academico.pagos.ver",
                        "academico.pagos.registrar",
                        "academico.pagos.anular"
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
