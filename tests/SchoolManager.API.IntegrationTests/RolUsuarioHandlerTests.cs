using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManager.API.Authorization;
using SchoolManager.API.Identity;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class RolUsuarioHandlerTests
{
    [Fact]
    public async Task Admin_activo_satisface_SoloAdmin()
    {
        var autorizado = await AutorizarAsync("admin", new RolUsuarioRequirement("admin"));

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Padre_activo_no_satisface_SoloAdmin()
    {
        var autorizado = await AutorizarAsync(
            "padre",
            new RolUsuarioRequirement("admin"),
            claimRole: "admin"
        );

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Admin_activo_satisface_AdminOPadre()
    {
        var autorizado = await AutorizarAsync(
            "admin",
            new RolUsuarioRequirement("admin", "padre")
        );

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Padre_activo_satisface_AdminOPadre()
    {
        var autorizado = await AutorizarAsync(
            "padre",
            new RolUsuarioRequirement("admin", "padre")
        );

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Operador_activo_no_satisface_AdminOPadre()
    {
        var autorizado = await AutorizarAsync(
            "operador",
            new RolUsuarioRequirement("admin", "padre"),
            claimRole: "admin"
        );

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Usuario_no_resoluble_no_satisface_policy()
    {
        var service = new UsuarioActualControlado(null);
        var requirement = new RolUsuarioRequirement("admin", "padre");
        var context = CrearContexto(requirement);
        var handler = new RolUsuarioHandler(service);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static async Task<bool> AutorizarAsync(
        string rol,
        RolUsuarioRequirement requirement,
        string? claimRole = null
    )
    {
        var usuario = new UsuarioActual(Guid.NewGuid(), Guid.NewGuid(), rol);
        var context = CrearContexto(requirement, claimRole);
        var handler = new RolUsuarioHandler(new UsuarioActualControlado(usuario));

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static AuthorizationHandlerContext CrearContexto(
        RolUsuarioRequirement requirement,
        string? claimRole = null
    )
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };

        if (claimRole is not null)
        {
            claims.Add(new Claim("role", claimRole));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return new AuthorizationHandlerContext([requirement], principal, resource: null);
    }

    private sealed class UsuarioActualControlado(UsuarioActual? usuario) : IUsuarioActualService
    {
        public Task<UsuarioActual> ObtenerAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default
        )
        {
            return usuario is null
                ? Task.FromException<UsuarioActual>(
                    new UnauthorizedAccessException("Usuario no resoluble."))
                : Task.FromResult(usuario);
        }
    }
}
