using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MensualidadesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll([FromQuery] Guid? alumnoId, [FromQuery] string? estado)
    {
        return Ok(new List<MensualidadDto>());
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return Ok(new MensualidadDto { Id = id });
    }

    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] MensualidadCreateDto dto)
    {
        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    [HttpPost("generar-anio")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult GenerarAnioEscolar([FromBody] object dto)
    {
        return Ok(new { mensaje = "Mensualidades del año escolar generadas" });
    }

    [HttpGet("estado-cuenta/{alumnoId:guid}")]
    public IActionResult GetEstadoCuenta(Guid alumnoId)
    {
        return Ok(new { mensaje = "Estado de cuenta", alumnoId });
    }

    [HttpPost("generar/{cicloId:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult GenerarMensualidades(Guid cicloId, [FromQuery] decimal monto)
    {
        if (monto <= 0)
            return BadRequest(new { error = "El monto debe ser mayor a cero" });

        return Ok(new { mensaje = "Mensualidades generadas", cicloId, monto });
    }

    [HttpPut("{id:guid}/descuento")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult AplicarDescuento(Guid id, [FromBody] DescuentoDto dto)
    {
        if (dto.Descuento < 0)
            return BadRequest(new { error = "El descuento no puede ser negativo" });

        return Ok(new { mensaje = "Descuento aplicado", id, descuento = dto.Descuento });
    }
}
