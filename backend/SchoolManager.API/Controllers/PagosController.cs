using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll([FromQuery] Guid? alumnoId)
    {
        return Ok(new { mensaje = "Listado de pagos", alumnoId });
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return Ok(new { mensaje = "Detalle del pago", id });
    }

    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult RegistrarPago([FromBody] PagoCreateDto dto)
    {
        if (dto.MontoPagado <= 0)
            return BadRequest(new { error = "El monto pagado debe ser mayor a cero" });

        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    [HttpGet("mensualidad/{mensualidadId:guid}")]
    public IActionResult GetPagosPorMensualidad(Guid mensualidadId)
    {
        return Ok(new { mensaje = "Pagos de mensualidad", mensualidadId });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult AnularPago(Guid id)
    {
        return Ok(new { mensaje = "Pago anulado", id });
    }
}
