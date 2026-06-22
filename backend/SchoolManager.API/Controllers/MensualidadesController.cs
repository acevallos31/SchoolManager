using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MensualidadesController : ControllerBase
{
    // GET api/mensualidades?alumnoId={id}&estado=pendiente
    [HttpGet]
    public IActionResult GetAll([FromQuery] Guid? alumnoId, [FromQuery] string? estado)
    {
        // TODO: listar mensualidades (admin: todas, padre: solo de sus hijos)
        return Ok(new List<MensualidadDto>());
    }

    // GET api/mensualidades/{id}
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        // TODO: obtener mensualidad por id
        return Ok(new MensualidadDto { Id = id });
    }

    // POST api/mensualidades
    // Genera una o varias mensualidades para un alumno (ej. las 10 del año escolar)
    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] MensualidadCreateDto dto)
    {
        // TODO: insertar mensualidad en la base de datos
        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    // POST api/mensualidades/generar-anio
    // Genera automáticamente las mensualidades de todo un año escolar para un alumno
    [HttpPost("generar-anio")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult GenerarAnioEscolar([FromBody] object dto)
    {
        // TODO: crear N mensualidades según el calendario escolar
        return Ok();
    }
}
