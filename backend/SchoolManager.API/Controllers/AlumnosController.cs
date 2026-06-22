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

    private readonly IConfiguration _config;
    public AlumnosController(IConfiguration config) => _config = config;

    // GET /api/alumnos
    [HttpGet]
    public IActionResult GetAlumnos([FromQuery] string? grado, [FromQuery] string? buscar)
    {
        // TODO: Consultar Supabase con filtros opcionales
        return Ok(new { mensaje = "Listado de alumnos", grado, buscar });
    }

    // GET /api/alumnos/{id}
    [HttpGet("{id:guid}")]
    public IActionResult GetAlumno(Guid id)
    {
        // TODO: Obtener alumno por ID desde Supabase
        return Ok(new { mensaje = "Detalle del alumno", id });
    }

    // POST /api/alumnos
    [HttpPost]
    public IActionResult CrearAlumno([FromBody] AlumnoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Identidad))
            return BadRequest(new { error = "Nombre e identidad son obligatorios" });
        // TODO: Insertar alumno en Supabase
        return Created("/api/alumnos", new { mensaje = "Alumno creado", alumno = dto });
    }

    // PUT /api/alumnos/{id}
    [HttpPut("{id:guid}")]
    public IActionResult ActualizarAlumno(Guid id, [FromBody] AlumnoCreateDto dto)
    {
        // TODO: Actualizar alumno en Supabase
        return Ok(new { mensaje = "Alumno actualizado", id });
    }

    // DELETE /api/alumnos/{id}
    [HttpDelete("{id:guid}")]
    public IActionResult DesactivarAlumno(Guid id)
    {
        // TODO: Cambiar estado a 'inactivo' (nunca eliminar físicamente)
        return Ok(new { mensaje = "Alumno desactivado", id });

    }
}
