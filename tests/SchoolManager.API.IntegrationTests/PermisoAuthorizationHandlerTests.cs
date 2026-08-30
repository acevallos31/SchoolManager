using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManager.API.Authorization;
using SchoolManager.API.Identity;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class PermisoAuthorizationHandlerTests
{
    [Fact]
    public async Task Permiso_presente_autoriza()
    {
        Assert.True(await AutorizarAsync(["academico.alumnos.ver"], "academico.alumnos.ver"));
    }

    [Fact]
    public async Task Permiso_ausente_no_autoriza()
    {
        Assert.False(await AutorizarAsync([], "academico.alumnos.ver"));
    }

    [Fact]
    public async Task Dos_roles_combinan_permisos_para_autorizar()
    {
        var usuario = new UsuarioActual(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["consulta", "operador"],
            ["academico.alumnos.ver", "academico.matriculas.crear"]
        );

        Assert.True(await AutorizarAsync(usuario, "academico.matriculas.crear"));
    }

    [Fact]
    public async Task Claim_role_no_concede_permiso()
    {
        Assert.False(await AutorizarAsync([], "academico.alumnos.ver", claimRole: "admin"));
    }

    [Fact]
    public async Task Usuario_no_resoluble_no_autoriza()
    {
        var requirement = new PermisoRequirement("academico.alumnos.ver");
        var context = CrearContexto(requirement);
        var handler = new PermisoAuthorizationHandler(new UsuarioActualControlado(null));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static Task<bool> AutorizarAsync(
        IReadOnlyList<string> permisos,
        string permiso,
        string? claimRole = null
    ) => AutorizarAsync(
        new UsuarioActual(Guid.NewGuid(), Guid.NewGuid(), ["usuario"], permisos),
        permiso,
        claimRole
    );

    private static async Task<bool> AutorizarAsync(
        UsuarioActual usuario,
        string permiso,
        string? claimRole = null
    )
    {
        var requirement = new PermisoRequirement(permiso);
        var context = CrearContexto(requirement, claimRole);
        var handler = new PermisoAuthorizationHandler(new UsuarioActualControlado(usuario));

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static AuthorizationHandlerContext CrearContexto(
        PermisoRequirement requirement,
        string? claimRole = null
    )
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        if (claimRole is not null)
        {
            claims.Add(new Claim("role", claimRole));
        }

        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            resource: null
        );
    }

    private sealed class UsuarioActualControlado(UsuarioActual? usuario) : IUsuarioActualService
    {
        public Task<UsuarioActual> ObtenerAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default
        ) => usuario is null
            ? Task.FromException<UsuarioActual>(new UnauthorizedAccessException())
            : Task.FromResult(usuario);
    }
}
