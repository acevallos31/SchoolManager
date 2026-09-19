using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

// Estado de cuenta del alumno (Bloque 047B). El controller es delgado: delega
// toda la composicion autoritativa en la capa de aplicacion (IEstadoCuentaService),
// que reutiliza las RPC de 021 (resumen, cargos, pagos) dentro de una unica
// transaccion. El frontend solo presenta/imprime/descarga el DTO.
[ApiController]
// Ruta explícita: el frontend consume /api/estado-cuenta; [controller] produciría /api/EstadoCuenta.
[Route("api/estado-cuenta")]
[Authorize]
public class EstadoCuentaController(NpgsqlDataSource dataSource, IEstadoCuentaService estadoCuenta)
    : ApiControllerBase(dataSource)
{
    [HttpGet("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Cargos.Ver)]
    public async Task<IActionResult> Obtener(Guid alumnoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            var dto = await estadoCuenta.ObtenerEstadoCuentaAsync(
                alumnoId, institucionId, User.FindFirstValue("sub")!, ct);
            return dto is null ? NotFound(new { error = "El alumno no existe." }) : Ok(dto);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}
