using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Identity;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[Route("api/invitaciones")]
[Authorize]
public sealed class InvitacionesController(
    NpgsqlDataSource dataSource,
    InvitationDeliveryService deliveryService,
    InvitationAcceptanceService acceptanceService) : ApiControllerBase(dataSource)
{
    [HttpPost("aceptar")]
    public async Task<IActionResult> Aceptar(
        [FromBody] AceptarInvitacionAccesoDto dto,
        CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            return Unauthorized();

        try
        {
            return Ok(await acceptanceService.AcceptAsync(dto.Token, sub, ct));
        }
        catch (ArgumentException)
        {
            return BadRequest(new { error = "El token de invitacion no es valido." });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (PostgresException ex)
        {
            return ToError(ex);
        }
    }

    [HttpPost("{invitacionId:guid}/enviar")]
    public async Task<IActionResult> Enviar(Guid invitacionId, CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            return Unauthorized();

        try
        {
            var result = await deliveryService.SendAsync(invitacionId, sub, ct);
            return Ok(result);
        }
        catch (InvitationEmailNotConfiguredException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "El servicio de correo de invitaciones todavía no está configurado."
            });
        }
        catch (InvitationEmailDeliveryException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "No se pudo entregar el correo de invitación. Puedes reintentar sin crear otro usuario."
            });
        }
        catch (PostgresException ex)
        {
            return ToError(ex);
        }
    }
}
