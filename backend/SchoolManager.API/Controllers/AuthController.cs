using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.Identity;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(IUsuarioActualService usuarioActualService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioActual>> Me(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await usuarioActualService.ObtenerAsync(User, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            // Token valido sin claim sub utilizable: sesion inservible.
            return PerfilNoDisponible("SESION_INVALIDA", "La sesion no identifica un usuario valido.");
        }
        catch (IdentidadNoVinculadaException)
        {
            // Caso Google recien autenticado y vinculacion 027 aun no ejecutada.
            return PerfilNoDisponible(
                "IDENTIDAD_NO_VINCULADA",
                "Tu cuenta no esta vinculada a un usuario de SchoolManager. Solicita al administrador que vincule tu identidad."
            );
        }
        catch (UsuarioInactivoException)
        {
            return PerfilNoDisponible(
                "USUARIO_INACTIVO",
                "Tu usuario esta inactivo. Contacta al administrador."
            );
        }
    }

    /// <summary>
    /// 403 con cuerpo estable y legible por el frontend. El codigo permite
    /// distinguir "identidad sin vincular" de un fallo generico de red o de
    /// servidor, sin filtrar detalles internos ni tokens.
    /// </summary>
    private ObjectResult PerfilNoDisponible(string codigo, string mensaje)
        => StatusCode(StatusCodes.Status403Forbidden, new { codigo, mensaje });
}
