using Microsoft.AspNetCore.Authorization;
using SchoolManager.API.Identity;

namespace SchoolManager.API.Authorization;

public sealed class RolUsuarioHandler(IUsuarioActualService usuarioActualService)
    : AuthorizationHandler<RolUsuarioRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RolUsuarioRequirement requirement
    )
    {
        UsuarioActual usuarioActual;

        try
        {
            usuarioActual = await usuarioActualService.ObtenerAsync(context.User);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        if (requirement.RolesPermitidos.Contains(usuarioActual.Rol))
        {
            context.Succeed(requirement);
        }
    }
}
