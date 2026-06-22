using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    }
}
