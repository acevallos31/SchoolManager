using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatriculasController : ControllerBase
{
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
