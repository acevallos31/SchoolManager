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
/// Fixture compartida para los tests de integracion de MatriculasController.
/// Reutiliza la instalacion de base de datos de SchoolManager.Database.IntegrationTests
/// (bootstrap + migraciones 001-016) sobre un Postgres real de Testcontainers y
/// levanta la API de ASP.NET Core en memoria apuntando a ese mismo Postgres.
/// La implementacion se configura en modo multiinstitucion para reproducir el
/// defecto original (resolver_institucion_operacion(NULL) -> SM003) y validar
/// que las lecturas quedan acotadas al ambito institucional real de cada usuario.
/// </summary>
public sealed class MatriculasApiFactory : IAsyncLifetime
{
    internal static readonly Guid AdminA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    internal static readonly Guid AdminB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private readonly PostgreSqlFixture _db = new();
    private WebApplicationFactory<Program> _web = null!;
    private Guid _institucionA;
    private Guid _institucionB;

    public NpgsqlDataSource DatosAcademicos => _db.DataSource;
    public Guid InstitucionA => _institucionA;
    public Guid InstitucionB => _institucionB;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();

        // Multiinstitucion activa: reproduce el contexto donde las lecturas
        // que pasaban NULL a resolver_institucion_operacion fallaban con SM003.
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
        if (_web is not null)
        {
            await _web.DisposeAsync();
        }

        await _db.DisposeAsync();
    }

    public HttpClient CrearCliente(string identidad)
    {
        var client = _web.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", identidad);
        return client;
    }

    // ----- Seeders del modelo academico -----

    public Task<Guid> CrearAlumnoAsync(Guid institucion) => ScalarGuidAsync(
        "select public.crear_alumno_nueva_persona($1, $2, $3)",
        institucion, "Alumno", Guid.NewGuid().ToString("N"));

    public Task<Guid> CrearCicloAsync(Guid institucion) => ScalarGuidAsync(
        "insert into public.ciclos_escolares (institucion_id, nombre) values ($1, $2) returning id",
        institucion, $"Ciclo {Guid.NewGuid():N}");

    public Task<Guid> CrearGradoAsync() => ScalarGuidAsync(
        "insert into public.grados (nombre) values ($1) returning id",
        $"Grado {Guid.NewGuid():N}");

    public Task<Guid> CrearPeriodoAsync(Guid ciclo) => ScalarGuidAsync(
        "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1, $2, current_date, current_date + 30) returning id",
        ciclo, $"Periodo {Guid.NewGuid():N}");

    public Task<Guid> CrearSeccionAsync(
        Guid institucion, Guid ciclo, Guid grado, string nombre, int? cupo = null) => cupo.HasValue
        ? ScalarGuidAsync(
            "select public.crear_seccion($1, $2, $3, null, $4, $5)",
            institucion, ciclo, grado, nombre, cupo.Value)
        : ScalarGuidAsync(
            "select public.crear_seccion($1, $2, $3, null, $4)",
            institucion, ciclo, grado, nombre);

    public Task<Guid> MatricularAsync(Guid alumno, Guid seccion, Guid periodo) => ScalarGuidAsync(
        "select public.matricular_alumno($1, $2, $3)",
        alumno, seccion, periodo);

    public async Task CambiarEstadoAsync(Guid matricula, string estado, string motivo)
    {
        var adminUsuarioId = await ScalarGuidAsync(
            "select id from public.usuarios where auth_user_id = $1", AdminA);
        await using var command = DatosAcademicos.CreateCommand(
            "select public.cambiar_estado_matricula($1, $2, $3, $4)");
        command.Parameters.AddWithValue(matricula);
        command.Parameters.AddWithValue(estado);
        command.Parameters.AddWithValue(adminUsuarioId);
        command.Parameters.AddWithValue((object?)motivo ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Contexto> CrearContextoAsync(Guid institucion, int? cupo = null)
    {
        var ciclo = await CrearCicloAsync(institucion);
        var grado = await CrearGradoAsync();
        var periodo = await CrearPeriodoAsync(ciclo);
        var seccion = await CrearSeccionAsync(institucion, ciclo, grado, "A", cupo);
        return new Contexto(institucion, ciclo, grado, seccion, periodo);
    }

    // ----- Helpers internos -----

    private async Task CrearUsuarioConRolAsync(Guid authUserId, Guid rolId, Guid institucionId)
    {
        await ExecAsync(
            "insert into public.usuarios (auth_user_id, activo) values ($1, true)",
            authUserId);

        var usuarioId = await ScalarGuidAsync(
            "select id from public.usuarios where auth_user_id = $1", authUserId);

        await ExecAsync(
            "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)",
            usuarioId, rolId, institucionId);
    }

    private async Task ExecAsync(string sql, params object[] values)
    {
        await using var command = DatosAcademicos.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values)
    {
        await using var command = DatosAcademicos.CreateCommand(sql);
        AddParameters(command, values);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    public sealed record Contexto(
        Guid InstitucionId,
        Guid CicloId,
        Guid GradoId,
        Guid SeccionId,
        Guid PeriodoId);

    private sealed class UsuarioActualControlado : IUsuarioActualService
    {
        public Task<UsuarioActual> ObtenerAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            var identidad = principal.FindFirstValue("sub");

            if (identidad == MatriculasApiFactory.AdminA.ToString()
                || identidad == MatriculasApiFactory.AdminB.ToString())
            {
                return Task.FromResult(new UsuarioActual(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ["admin"],
                    [
                        "academico.alumnos.ver",
                        "academico.matriculas.ver",
                        "academico.matriculas.crear",
                        "academico.matriculas.cambiar_estado"
                    ]));
            }

            return Task.FromResult(new UsuarioActual(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [],
                []));
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
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim("sub", authorization.Parameter)],
                SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}