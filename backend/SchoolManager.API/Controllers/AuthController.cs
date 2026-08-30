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
            return Forbid();
        }
    }
}
