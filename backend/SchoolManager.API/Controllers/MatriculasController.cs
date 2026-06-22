using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


using SchoolManager.API.DTOs;


namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatriculasController : ControllerBase
{

    // GET api/matriculas?alumnoId={id}
    [HttpGet]
    public IActionResult GetAll([FromQuery] Guid? alumnoId)
    {
        // TODO: listar matrículas (admin: todas, padre: solo de sus hijos)
        return Ok(new List<object>());
    }

    // GET api/matriculas/{id}
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        // TODO: obtener matrícula por id
        return Ok();
    }

    // POST api/matriculas
    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] object dto)
    {
        // TODO: registrar nueva matrícula para un alumno y año escolar
        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    // PUT api/matriculas/{id}/estado
    [HttpPut("{id:guid}/estado")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult ActualizarEstado(Guid id, [FromBody] string nuevoEstado)
    {
        // TODO: actualizar estado de la matrícula (activa | retirada | finalizada)
        return NoContent();

    // POST /api/matriculas
    [HttpPost]
    public IActionResult RegistrarMatricula([FromBody] MatriculaCreateDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { error = "El monto debe ser mayor a cero" });
        // TODO: Verificar que no exista matrícula para el mismo alumno y ciclo
        return Created("/api/matriculas", new { mensaje = "Matrícula registrada" });
    }

    // GET /api/matriculas/alumno/{alumnoId}
    [HttpGet("alumno/{alumnoId:guid}")]
    public IActionResult GetMatriculasAlumno(Guid alumnoId)
    {
        // TODO: Obtener historial de matrículas del alumno
        return Ok(new { mensaje = "Historial de matrículas", alumnoId });

    }
}
