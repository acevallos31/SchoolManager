using Microsoft.AspNetCore.Authorization;
using SchoolManager.API.Identity;

namespace SchoolManager.API.Authorization;

public sealed class PermisoAuthorizationHandler(IUsuarioActualService usuarioActualService)
    : AuthorizationHandler<PermisoRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermisoRequirement requirement
    )
    {
        try
        {
            var usuario = await usuarioActualService.ObtenerAsync(context.User);

            if (usuario.Permisos.Contains(requirement.Permiso, StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Una identidad no resoluble simplemente no satisface la autorizacion.
        }
    }
}
