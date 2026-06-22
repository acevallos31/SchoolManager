using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatriculasController : ControllerBase
{
    private const string TableName = "matriculas";
    private readonly SupabaseTableService _tableService;

    public MatriculasController(SupabaseTableService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? alumnoId, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["select"] = "*",
            ["order"] = "created_at.desc"
        };

        if (alumnoId.HasValue)
        {
            query["alumno_id"] = $"eq.{alumnoId.Value}";
        }

        try
        {
            var matriculas = await _tableService.GetListAsync<MatriculaDto>(TableName, query, cancellationToken);
            return Ok(matriculas);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var matricula = await _tableService.GetSingleAsync<MatriculaDto>(
                TableName,
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{id}"
                },
                cancellationToken);

            return matricula is null ? NotFound(new { error = "Matricula no encontrada." }) : Ok(matricula);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> Create([FromBody] MatriculaCreateDto dto, CancellationToken cancellationToken)
    {
        if (dto.AlumnoId == Guid.Empty || dto.CicloId == Guid.Empty || dto.GradoId == Guid.Empty || dto.SeccionId == Guid.Empty)
        {
            return BadRequest(new { error = "Alumno, ciclo, grado y seccion son obligatorios." });
        }

        if (dto.Monto <= 0)
        {
            return BadRequest(new { error = "El monto debe ser mayor a cero." });
        }

        var payload = new
        {
            alumno_id = dto.AlumnoId,
            ciclo_id = dto.CicloId,
            grado_id = dto.GradoId,
            seccion_id = dto.SeccionId,
            fecha_matricula = DateOnly.FromDateTime(DateTime.UtcNow),
            monto = dto.Monto,
            estado = string.IsNullOrWhiteSpace(dto.Estado) ? "pendiente" : dto.Estado.Trim().ToLowerInvariant(),
            created_at = DateTimeOffset.UtcNow
        };

        try
        {
            var matricula = await _tableService.InsertAsync<MatriculaDto>(TableName, payload, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = matricula.Id }, matricula);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MatriculaCreateDto dto, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["updated_at"] = DateTimeOffset.UtcNow
        };

        if (dto.AlumnoId != Guid.Empty) payload["alumno_id"] = dto.AlumnoId;
        if (dto.CicloId != Guid.Empty) payload["ciclo_id"] = dto.CicloId;
        if (dto.GradoId != Guid.Empty) payload["grado_id"] = dto.GradoId;
        if (dto.SeccionId != Guid.Empty) payload["seccion_id"] = dto.SeccionId;
        if (dto.Monto > 0) payload["monto"] = dto.Monto;
        if (!string.IsNullOrWhiteSpace(dto.Estado)) payload["estado"] = dto.Estado.Trim().ToLowerInvariant();

        try
        {
            var matricula = await _tableService.UpdateAsync<MatriculaDto>(TableName, id, payload, cancellationToken);
            return matricula is null ? NotFound(new { error = "Matricula no encontrada." }) : Ok(matricula);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}
