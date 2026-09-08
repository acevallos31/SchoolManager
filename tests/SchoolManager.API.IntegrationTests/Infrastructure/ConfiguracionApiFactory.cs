using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests.Infrastructure;

// Solo la autenticación JWT se sustituye. UsuarioActualService, policies .NET
// y permisos internos de las RPC consultan la misma DB desechable real.
public sealed class ConfiguracionApiFactory : IAsyncLifetime
{
    private readonly PostgreSqlFixture db = new();
    private WebApplicationFactory<Program> web = null!;
    public Guid InstitucionA { get; private set; }
    public Guid InstitucionB { get; private set; }
    public Guid Admin { get; private set; }
    public Guid EditorA { get; private set; }
    public Guid LectorA { get; private set; }
    public Guid SinPermisos { get; private set; }

    public async Task InitializeAsync()
    {
        await db.InitializeAsync();
        web = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:PostgreSQL", db.ConnectionString);
            builder.ConfigureTestServices(services => services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "ConfiguracionTest";
                options.DefaultChallengeScheme = "ConfiguracionTest";
                options.DefaultForbidScheme = "ConfiguracionTest";
            }).AddScheme<AuthenticationSchemeOptions, ConfiguracionTestAuthHandler>("ConfiguracionTest", _ => { }));
        });
    }

    public async Task PrepararAsync()
    {
        await EjecutarAsync("update public.instituciones set activo = false; update public.configuracion_implementacion set multiples_instituciones = false");
        InstitucionA = await CrearInstitucionAsync(true);
        InstitucionB = await CrearInstitucionAsync(false);
        Admin = await CrearUsuarioAsync(null, null, true);
        EditorA = await CrearUsuarioAsync("configuracion.instituciones.editar", InstitucionA);
        LectorA = await CrearUsuarioAsync("configuracion.instituciones.ver", InstitucionA);
        SinPermisos = await CrearUsuarioAsync(null, null);
    }

    public async Task ModoMultiAsync()
    {
        await EjecutarAsync("update public.instituciones set activo = true where id = $1", InstitucionB);
        await EjecutarAsync("update public.configuracion_implementacion set multiples_instituciones = true");
    }

    public Task SinInstitucionesAsync() => EjecutarAsync("update public.instituciones set activo = false");

    public HttpClient Cliente(Guid? identidad = null)
    {
        var client = web.CreateClient();
        if (identidad.HasValue)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", identidad.ToString());
        return client;
    }

    private async Task<Guid> CrearInstitucionAsync(bool activo) => await ScalarAsync(
        "insert into public.instituciones(nombre, activo) values ($1, $2) returning id", $"Centro {Guid.NewGuid():N}", activo);

    private async Task<Guid> CrearUsuarioAsync(string? permiso, Guid? institucion, bool admin = false)
    {
        var auth = Guid.NewGuid();
        var persona = await ScalarAsync("insert into public.personas(nombres,apellidos) values ('Prueba','030F') returning id");
        var usuario = await ScalarAsync("insert into public.usuarios(persona_id, auth_user_id, activo) values ($1,$2,true) returning id", persona, auth);
        if (admin || permiso is not null)
        {
            var rol = admin
                ? await ScalarAsync("select id from public.roles where codigo = 'admin'")
                : await ScalarAsync("insert into public.roles(codigo,nombre) values ($1,'Prueba 030F') returning id", $"test_{Guid.NewGuid():N}");
            if (permiso is not null)
                await EjecutarAsync("insert into public.roles_permisos(rol_id,permiso_id) select $1,id from public.permisos where codigo=$2", rol, permiso);
            await EjecutarAsync("insert into public.usuarios_roles(usuario_id,rol_id,institucion_id) values ($1,$2,$3)", usuario, rol, (object?)institucion ?? DBNull.Value);
        }
        return auth;
    }

    private async Task<Guid> ScalarAsync(string sql, params object[] values)
    {
        await using var command = db.DataSource.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task EjecutarAsync(string sql, params object[] values)
    {
        await using var command = db.DataSource.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (web is not null) await web.DisposeAsync();
        await db.DisposeAsync();
    }
}

public sealed class ConfiguracionTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || header.Scheme != "Test" || !Guid.TryParse(header.Parameter, out _))
            return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", header.Parameter!)], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
