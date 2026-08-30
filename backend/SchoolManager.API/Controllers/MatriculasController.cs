using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatriculasController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.Matriculas.Ver)]
    public IActionResult GetAll([FromQuery] Guid? alumnoId)
    {
        return Ok(new List<object>());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permisos.Matriculas.Ver)]
    public IActionResult GetById(Guid id)
    {
        return Ok();
    }

    [HttpPost]
    [Authorize(Policy = Permisos.Matriculas.Crear)]
    public IActionResult Create([FromBody] object dto)
    {
        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    [HttpPut("{id:guid}/estado")]
    [Authorize(Policy = Permisos.Matriculas.CambiarEstado)]
    public IActionResult ActualizarEstado(Guid id, [FromBody] string nuevoEstado)
    {
        return NoContent();
    }

    [HttpPost("registrar")]
    [Authorize(Policy = Permisos.Matriculas.Crear)]
    public IActionResult RegistrarMatricula([FromBody] MatriculaCreateDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { error = "El monto debe ser mayor a cero" });

        return Created("/api/matriculas", new { mensaje = "Matrícula registrada" });
    }

    [HttpGet("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Matriculas.Ver)]
    public IActionResult GetMatriculasAlumno(Guid alumnoId)
    {
        return Ok(new { mensaje = "Historial de matrículas", alumnoId });
    }
}
