using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlumnosController : ControllerBase
{
    // GET api/alumnos
    // Admin: lista todos los alumnos. Padre: lista solo a sus hijos (filtrar por padre_id = sub del token).
    [HttpGet]
    public IActionResult GetAll()
    {
        // TODO: consultar Supabase/Postgres y devolver List<AlumnoDto>
        return Ok(new List<AlumnoDto>());
    }

    // GET api/alumnos/{id}
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        // TODO: buscar alumno por id, validar que el padre solo pueda ver a sus hijos
        return Ok(new AlumnoDto { Id = id });
    }

    // POST api/alumnos
    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] AlumnoCreateDto dto)
    {
        // TODO: insertar alumno en la base de datos
        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    // PUT api/alumnos/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Update(Guid id, [FromBody] AlumnoCreateDto dto)
    {
        // TODO: actualizar alumno
        return NoContent();
    }

    // DELETE api/alumnos/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Delete(Guid id)
    {
        // TODO: marcar alumno como inactivo (soft delete) o eliminar
        return NoContent();
    }
}
