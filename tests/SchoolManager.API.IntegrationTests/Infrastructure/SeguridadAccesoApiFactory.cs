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

public sealed class SeguridadAccesoApiFactory : IAsyncLifetime
{
    private readonly PostgreSqlFixture db = new();
    private WebApplicationFactory<Program> web = null!;

    public Guid InstitucionA { get; private set; }
    public Guid InstitucionB { get; private set; }
    public Guid AdministradorA { get; private set; }
    public Guid AdminGlobal { get; private set; }
    public Guid SinPermisos { get; private set; }
    public Guid UsuarioDestinoId { get; private set; }

    public async Task InitializeAsync()
    {
        await db.InitializeAsync();
        web = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:PostgreSQL", db.ConnectionString);
            builder.ConfigureTestServices(services => services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "SeguridadAccesoTest";
                options.DefaultChallengeScheme = "SeguridadAccesoTest";
                options.DefaultForbidScheme = "SeguridadAccesoTest";
            }).AddScheme<AuthenticationSchemeOptions, SeguridadAccesoTestAuthHandler>(
                "SeguridadAccesoTest", _ => { }));
        });
    }

    public async Task PrepararAsync()
    {
        InstitucionA = await CrearInstitucionAsync();
        InstitucionB = await CrearInstitucionAsync();
        AdministradorA = await CrearAdministradorInstitucionalAsync(InstitucionA);
        AdminGlobal = await CrearAdminGlobalAsync();
        SinPermisos = (await CrearUsuarioAsync()).AuthUserId;
        UsuarioDestinoId = (await CrearUsuarioAsync()).UsuarioId;
    }

    public HttpClient Cliente(Guid? identidad = null)
    {
        var client = web.CreateClient();
        if (identidad.HasValue)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Test", identidad.ToString());
        return client;
    }

    private async Task<Guid> CrearAdministradorInstitucionalAsync(Guid institucionId)
    {
        var usuario = await CrearUsuarioAsync();
        var rolId = await ScalarGuidAsync("""
            insert into public.roles(codigo, nombre, tipo, institucion_id, activo)
            values ($1, 'Gestor de seguridad', 'institucional', $2, true)
            returning id
            """, $"seguridad_{Guid.NewGuid():N}", institucionId);

        await EjecutarAsync("""
            insert into public.roles_permisos(rol_id, permiso_id)
            select $1, id
            from public.permisos
            where codigo in (
              'identidad.roles.ver',
              'identidad.roles.crear',
              'identidad.roles.editar',
              'identidad.roles.asignar_permisos',
              'identidad.usuarios.ver',
              'identidad.usuarios.asignar_roles'
            )
            """, rolId);
        await EjecutarAsync("""
            insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
            values ($1, $2, $3)
            """, usuario.UsuarioId, rolId, institucionId);

        return usuario.AuthUserId;
    }

    private async Task<Guid> CrearAdminGlobalAsync()
    {
        var usuario = await CrearUsuarioAsync();
        var adminId = await ScalarGuidAsync(
            "select id from public.roles where codigo='admin' and institucion_id is null");
        await EjecutarAsync(
            "insert into public.usuarios_roles(usuario_id, rol_id) values ($1, $2)",
            usuario.UsuarioId, adminId);
        return usuario.AuthUserId;
    }

    private async Task<Identidad> CrearUsuarioAsync()
    {
        var personaId = await ScalarGuidAsync(
            "insert into public.personas(nombres, apellidos) values ('Prueba', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUserId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync("""
            insert into public.usuarios(persona_id, auth_user_id, activo)
            values ($1, $2, true) returning id
            """, personaId, authUserId);
        return new Identidad(usuarioId, authUserId);
    }

    private Task<Guid> CrearInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones(nombre, activo) values ($1, true) returning id",
        $"Centro {Guid.NewGuid():N}");

    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values)
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

    private sealed record Identidad(Guid UsuarioId, Guid AuthUserId);
}

public sealed class SeguridadAccesoTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || header.Scheme != "Test"
            || !Guid.TryParse(header.Parameter, out _))
            return Task.FromResult(AuthenticateResult.NoResult());

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", header.Parameter!)], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name)));
    }
}
