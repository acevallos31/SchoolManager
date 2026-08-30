using System.Security.Claims;

namespace SchoolManager.API.Identity;

public interface IUsuarioActualService
{
    Task<UsuarioActual> ObtenerAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default
    );
}
