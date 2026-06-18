using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MensualidadesController : ControllerBase
{
    // GET /api/mensualidades/{alumnoId}
    [HttpGet("{alumnoId:guid}")]
    public IActionResult GetEstadoCuenta(Guid alumnoId)
    {
        // TODO: Obtener todas las mensualidades del alumno (Admin y Padre vía RLS)
        return Ok(new { mensaje = "Estado de cuenta", alumnoId });
    }

    // POST /api/mensualidades/generar/{cicloId}
    [HttpPost("generar/{cicloId:guid}")]
    public IActionResult GenerarMensualidades(Guid cicloId, [FromQuery] decimal monto)
    {
        if (monto <= 0)
            return BadRequest(new { error = "El monto debe ser mayor a cero" });
        // TODO: Generar mensualidad por cada alumno activo del ciclo
        return Ok(new { mensaje = "Mensualidades generadas", cicloId, monto });
    }

    // PUT /api/mensualidades/{id}/descuento
    [HttpPut("{id:guid}/descuento")]
    public IActionResult AplicarDescuento(Guid id, [FromBody] DescuentoDto dto)
    {
        if (dto.Descuento < 0)
            return BadRequest(new { error = "El descuento no puede ser negativo" });
        // TODO: Actualizar descuento y recalcular monto_final en Supabase
        return Ok(new { mensaje = "Descuento aplicado", id, descuento = dto.Descuento });
    }
}
