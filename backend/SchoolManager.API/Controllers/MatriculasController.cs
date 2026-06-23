using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
    [Authorize(Policy = "AdminOOperador")]
    public async Task<IActionResult> Create([FromBody] MatriculaCreateDto dto, CancellationToken cancellationToken)
    {
        if (dto.AlumnoId == Guid.Empty || dto.CicloId == Guid.Empty || dto.GradoId == Guid.Empty || dto.SeccionId == Guid.Empty || dto.PlanPagoId == Guid.Empty)
        {
            return BadRequest(new { error = "Alumno, ciclo, grado, seccion y plan de pago son obligatorios." });
        }

        try
        {
            var resultado = await _tableService.RpcAsync<RegistrarMatriculaResponse>(
                "registrar_matricula_transaccional",
                new
                {
                    p_alumno_id = dto.AlumnoId,
                    p_ciclo_id = dto.CicloId,
                    p_grado_id = dto.GradoId,
                    p_seccion_id = dto.SeccionId,
                    p_plan_pago_id = dto.PlanPagoId,
                    p_monto_matricula = dto.Monto > 0 ? dto.Monto : (decimal?)null,
                    p_registrado_por = TryGetUserGuid()
                },
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = resultado.Matricula.Id }, new
            {
                matricula = resultado.Matricula,
                facturasGeneradas = resultado.Facturas.Count,
                facturas = resultado.Facturas,
                mensaje = resultado.Mensaje
            });
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOOperador")]
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
        if (dto.PlanPagoId != Guid.Empty) payload["plan_pago_id"] = dto.PlanPagoId;
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

    private Guid? TryGetUserGuid()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }

    private sealed class RegistrarMatriculaResponse
    {
        public MatriculaDto Matricula { get; set; } = new();
        public List<CargoDto> Facturas { get; set; } = [];
        public string Mensaje { get; set; } = string.Empty;
    }
}
