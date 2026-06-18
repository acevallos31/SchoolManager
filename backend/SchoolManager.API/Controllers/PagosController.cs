using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    // POST /api/pagos
    [HttpPost]
    public IActionResult RegistrarPago([FromBody] PagoCreateDto dto)
    {
        if (dto.MontoPagado <= 0)
            return BadRequest(new { error = "El monto del pago debe ser mayor a cero" });
        // TODO: Insertar pago y actualizar mensualidad a 'pagada'
        return Created("/api/pagos", new { mensaje = "Pago registrado", pago = dto });
    }

    // GET /api/pagos/{mensualidadId}
    [HttpGet("{mensualidadId:guid}")]
    public IActionResult GetPago(Guid mensualidadId)
    {
        // TODO: Obtener pago asociado a una mensualidad
        return Ok(new { mensaje = "Detalle del pago", mensualidadId });
    }
}
