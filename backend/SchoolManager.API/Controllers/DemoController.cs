using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/demo")]
[Authorize]
[EnableRateLimiting("demo-session")]
public sealed class DemoController(
    IDemoSandboxService sandboxService,
    IOptions<DemoOptions> optionsAccessor,
    ILogger<DemoController> logger) : ControllerBase
{
    private readonly DemoOptions _options = optionsAccessor.Value;

    [HttpPost("session")]
    public Task<IActionResult> CrearOReutilizar(CancellationToken cancellationToken)
        => EjecutarAsync(
            (authUserId, templateId, ct) =>
                sandboxService.CrearOReutilizarAsync(authUserId, templateId, ct),
            "crear_o_reutilizar",
            cancellationToken
        );

    [HttpPost("session/reset")]
    public Task<IActionResult> Reiniciar(CancellationToken cancellationToken)
        => EjecutarAsync(
            (authUserId, templateId, ct) =>
                sandboxService.ReiniciarAsync(authUserId, templateId, ct),
            "reiniciar",
            cancellationToken
        );

    private async Task<IActionResult> EjecutarAsync(
        Func<Guid, Guid, CancellationToken, Task<DemoSessionResult>> operacion,
        string accion,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return NotFound();

        if (_options.TemplateInstitutionId is not Guid templateId
            || templateId == Guid.Empty)
        {
            logger.LogError(
                "Demo habilitada sin Demo:TemplateInstitutionId válido. Acción: {Accion}",
                accion
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { codigo = "DEMO_CONFIG_INVALIDA" }
            );
        }

        var sub = User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var authUserId))
            return Unauthorized(new { codigo = "SESION_INVALIDA" });

        var isAnonymous = User.FindFirstValue("is_anonymous");
        if (!string.Equals(isAnonymous, "true", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { codigo = "DEMO_REQUIERE_SESION_ANONIMA" }
            );
        }

        try
        {
            var result = await operacion(
                authUserId,
                templateId,
                cancellationToken
            );

            logger.LogInformation(
                "Demo {Accion} completada. SessionId={SessionId} InstitucionId={InstitucionId} Reused={Reused}",
                accion,
                result.SessionId,
                result.InstitucionId,
                result.Reused
            );

            return Ok(result);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0002")
        {
            logger.LogWarning(
                "Plantilla Demo no disponible. Acción={Accion} SqlState={SqlState}",
                accion,
                ex.SqlState
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { codigo = "DEMO_TEMPLATE_NO_DISPONIBLE" }
            );
        }
        catch (PostgresException ex) when (
            ex.SqlState is "22023" or "23514" or "42501" or "SM001")
        {
            logger.LogWarning(
                "Operación Demo rechazada. Acción={Accion} SqlState={SqlState}",
                accion,
                ex.SqlState
            );
            return StatusCode(
                StatusCodes.Status409Conflict,
                new { codigo = "DEMO_NO_DISPONIBLE" }
            );
        }
    }
}
