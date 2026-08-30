using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatriculasController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll([FromQuery] Guid? alumnoId)
    {
        return Ok(new List<object>());
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return Ok();
    }

    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] object dto)
    {
        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    [HttpPut("{id:guid}/estado")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult ActualizarEstado(Guid id, [FromBody] string nuevoEstado)
    {
        return NoContent();
    }

    [HttpPost("registrar")]
    public IActionResult RegistrarMatricula([FromBody] MatriculaCreateDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { error = "El monto debe ser mayor a cero" });

        return Created("/api/matriculas", new { mensaje = "Matrícula registrada" });
    }

    [HttpGet("alumno/{alumnoId:guid}")]
    public IActionResult GetMatriculasAlumno(Guid alumnoId)
    {
        return Ok(new { mensaje = "Historial de matrículas", alumnoId });
    }
}
