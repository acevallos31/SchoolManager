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
/// Fixture de integracion del Bloque 022 (portal responsable, SOLO LECTURA).
/// Aplica bootstrap + migraciones 001-022 sobre Postgres real (Testcontainers)
/// y levanta la API en memoria. Siembra dos instituciones (A/B) con un admin
/// cada una, y en A un responsable financiero (PadreA) vinculado a su hijo
/// alumnoA (con matricula + plan). Provee helpers para registrar un pago real
/// vía rpc (como AdminA) de modo que el responsable pueda leer datos reales.
/// </summary>
public sealed class PortalResponsableApiFactory : IAsyncLifetime
{
    internal static readonly Guid AdminA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    internal static readonly Guid AdminB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    internal static readonly Guid PadreA = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    private readonly PostgreSqlFixture _db = new();
    private WebApplicationFactory<Program> _web = null!;
    private Guid _institucionA;
    private Guid _institucionB;

    public NpgsqlDataSource Datos => _db.DataSource;
    public Guid InstitucionA => _institucionA;
    public Guid InstitucionB => _institucionB;
    public Guid AlumnoA { get; private set; }
    public Guid MatriculaA { get; private set; }
    public Guid AlumnoB { get; private set; }

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

        var ctxA = await SembrarContextoAsync(_institucionA, "HijoA");
        AlumnoA = ctxA.AlumnoId;
        MatriculaA = ctxA.MatriculaId;
        await VincularResponsableFinancieroAsync(PadreA, _institucionA, AlumnoA, "Padre de A");

        var ctxB = await SembrarContextoAsync(_institucionB, "HijoB");
        AlumnoB = ctxB.AlumnoId;

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
        if (!string.IsNullOrWhiteSpace(identidad))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Test", identidad);
        return client;
    }

    /// <summary>Registra un pago real sobre el alumno/matricula indicados usando
    /// la cadena RPC de 021 como AdminA (identidad fijada via set_config sobre la
    /// conexion superuser de Datos).</summary>
    public async Task RegistrarPagoAlumnoAAsync()
    {
        var concepto = await AdminScalarAsync<Guid>(AdminA,
            "select public.rpc_crear_concepto_financiero($1, $2, null, $3)",
            "Colegiatura", 500m, _institucionA);
        var cuota = $"{{\"orden\":0,\"concepto_id\":\"{concepto:N}\",\"descripcion\":\"Colegiatura\",\"monto\":500,\"vencimiento_dias\":30}}";
        var plan = await AdminScalarAsync<Guid>(AdminA,
            "select public.rpc_crear_plan_pago($1, null, $2::jsonb, $3)",
            $"Plan {Guid.NewGuid():N}", $"[{cuota}]", _institucionA);
        await AdminExecAsync(AdminA, "select public.rpc_asignar_plan_pago_matricula($1,$2,$3)",
            MatriculaA, plan, _institucionA);
        await AdminExecAsync(AdminA, "select public.rpc_generar_cargos_matricula($1,$2)",
            MatriculaA, _institucionA);

        // El cargo generado: leer id para aplicarle el pago.
        var cargoId = await AdminScalarAsync<Guid>(AdminA,
            "select id from public.cargos where matricula_id = $1 order by orden limit 1", MatriculaA);
        var aplicacion = $"{{\"cargo_id\":\"{cargoId:N}\",\"monto\":500}}";
        await AdminScalarAsync<Guid>(AdminA,
            "select public.rpc_registrar_pago($1, $2::jsonb, $3, $4, null, null, null)",
            AlumnoA, $"[{aplicacion}]", 500m, _institucionA);
    }

    private async Task<Guid> VincularResponsableFinancieroAsync(
        Guid authPadre, Guid institucion, Guid alumnoId, string apellidoPadre)
    {
        var personaPadre = await ScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Padre', $1) returning id",
            Guid.NewGuid().ToString("N"));
        await ExecAsync(
            "insert into public.usuarios (persona_id, auth_user_id, activo) values ($1, $2, true)",
            personaPadre, authPadre);
        var responsable = await ScalarGuidAsync(
            "insert into public.responsables (persona_id, institucion_id) values ($1, $2) returning id",
            personaPadre, institucion);
        await ExecAsync(
            "insert into public.alumno_responsable (alumno_id, responsable_id, parentesco, es_principal, acceso_financiero, estado) values ($1,$2,'Padre',true,true,'activo')",
            alumnoId, responsable);
        return responsable;
    }

    private async Task<ContextoAcademico> SembrarContextoAsync(Guid institucion, string hijo)
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
        var personaHijo = await ScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ($1, $2) returning id",
            hijo, Guid.NewGuid().ToString("N"));
        var alumno = await ScalarGuidAsync(
            "insert into public.alumnos (persona_id, institucion_id) values ($1,$2) returning id",
            personaHijo, institucion);
        var matricula = await ScalarGuidAsync(
            "insert into public.matriculas (alumno_id, institucion_id, ciclo_id, seccion_id, periodo_matricula_id) values ($1,$2,$3,$4,$5) returning id",
            alumno, institucion, ciclo, seccion, periodo);
        return new ContextoAcademico(institucion, ciclo, alumno, matricula, Guid.Empty, cicloInicio);
    }

    private async Task CrearUsuarioConRolAsync(Guid authUserId, Guid rolId, Guid institucionId)
    {
        var persona = await ScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Admin', $1) returning id",
            Guid.NewGuid().ToString("N"));
        await ExecAsync(
            "insert into public.usuarios (persona_id, auth_user_id, activo) values ($1, $2, true)",
            persona, authUserId);
        var usuarioId = await ScalarGuidAsync(
            "select id from public.usuarios where auth_user_id = $1", authUserId);
        await ExecAsync(
            "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)",
            usuarioId, rolId, institucionId);
    }

    private async Task<object> AdminExecAsync(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await Datos.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand("select set_config('request.jwt.claim.sub', @sub, true)", connection, tx);
        cmd.Parameters.AddWithValue("sub", authUserId.ToString());
        await cmd.ExecuteNonQueryAsync();
        await using var run = new NpgsqlCommand(sql, connection, tx);
        foreach (var value in values) run.Parameters.AddWithValue(value);
        await run.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return null!;
    }

    private async Task<T> AdminScalarAsync<T>(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await Datos.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand("select set_config('request.jwt.claim.sub', @sub, true)", connection, tx);
        cmd.Parameters.AddWithValue("sub", authUserId.ToString());
        await cmd.ExecuteNonQueryAsync();
        await using var run = new NpgsqlCommand(sql, connection, tx);
        foreach (var value in values) run.Parameters.AddWithValue(value);
        var result = await run.ExecuteScalarAsync();
        await tx.CommitAsync();
        return (T)result!;
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
        Guid InstitucionId, Guid CicloId, Guid AlumnoId, Guid MatriculaId,
        Guid PlanId, DateOnly CicloInicio);

    private sealed class UsuarioActualControlado : IUsuarioActualService
    {
        public Task<UsuarioActual> ObtenerAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            var identidad = principal.FindFirstValue("sub");
            var esAdmin = identidad == AdminA.ToString() || identidad == AdminB.ToString();
            return Task.FromResult(new UsuarioActual(
                Guid.NewGuid(), Guid.NewGuid(),
                esAdmin ? ["admin"] : [],
                esAdmin ? ["academico.cargos.ver", "academico.pagos.ver"] : []));
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
