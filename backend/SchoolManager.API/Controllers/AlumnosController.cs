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
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? grado, [FromQuery] string? buscar)
    {
        return Ok(new
        {
            mensaje = "Listado de alumnos",
            grado,
            buscar
        });
    }

    // GET api/alumnos/{id}
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return Ok(new
        {
            mensaje = "Detalle del alumno",
            id
        });
    }

    // POST api/alumnos
    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] AlumnoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Identidad))
            return BadRequest(new { error = "Nombre e identidad son obligatorios" });

        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }

    // PUT api/alumnos/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Update(Guid id, [FromBody] AlumnoCreateDto dto)
    {
        return Ok(new
        {
            mensaje = "Alumno actualizado",
            id
        });
    }

    // DELETE api/alumnos/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Delete(Guid id)
    {
        return Ok(new
        {
            mensaje = "Alumno desactivado",
            id
        });
    }
}
