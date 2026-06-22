using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/planes-pago")]
[Authorize]
public sealed class PlanesPagoController : ControllerBase
{
    private const string TableName = "planes_pago";
    private readonly SupabaseTableService _tableService;

    public PlanesPagoController(SupabaseTableService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOOperador")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var planes = await _tableService.GetListAsync<PlanPagoDto>(
                TableName,
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["activo"] = "eq.true",
                    ["order"] = "nombre.asc"
                },
                cancellationToken);

            return Ok(planes);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> Create([FromBody] PlanPagoCreateDto dto, CancellationToken cancellationToken)
    {
        var validation = Validate(dto);
        if (validation.Count > 0)
        {
            return BadRequest(new { errors = validation });
        }

        try
        {
            var plan = await _tableService.InsertAsync<PlanPagoDto>(
                TableName,
                ToPayload(dto),
                cancellationToken);

            return CreatedAtAction(nameof(GetAll), new { id = plan.Id }, plan);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PlanPagoCreateDto dto, CancellationToken cancellationToken)
    {
        var validation = Validate(dto);
        if (validation.Count > 0)
        {
            return BadRequest(new { errors = validation });
        }

        try
        {
            var plan = await _tableService.UpdateAsync<PlanPagoDto>(
                TableName,
                id,
                ToPayload(dto),
                cancellationToken);

            return plan is null ? NotFound(new { error = "Plan de pago no encontrado." }) : Ok(plan);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    private static List<string> Validate(PlanPagoCreateDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            errors.Add("El nombre del plan es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(dto.Tipo))
        {
            errors.Add("El tipo de plan de pago es obligatorio.");
        }

        if (dto.MontoMatricula < 0 || dto.MontoTotalAnual < 0)
        {
            errors.Add("Los montos no pueden ser negativos.");
        }

        if (dto.CantidadCuotas <= 0)
        {
            errors.Add("La cantidad de cuotas debe ser mayor a cero.");
        }

        if (dto.MesInicio is < 1 or > 12)
        {
            errors.Add("El mes de inicio debe estar entre 1 y 12.");
        }

        if (dto.DiaVencimiento is < 1 or > 28)
        {
            errors.Add("El dia de vencimiento debe estar entre 1 y 28.");
        }

        return errors;
    }

    private static object ToPayload(PlanPagoCreateDto dto)
    {
        return new
        {
            nombre = dto.Nombre.Trim(),
            tipo = dto.Tipo.Trim().ToLowerInvariant(),
            tipo_plan_pago_id = dto.TipoPlanPagoId,
            descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            monto_matricula = dto.MontoMatricula,
            monto_total_anual = dto.MontoTotalAnual,
            cantidad_cuotas = dto.CantidadCuotas,
            mes_inicio = dto.MesInicio,
            dia_vencimiento = dto.DiaVencimiento,
            descuento_porcentaje = dto.DescuentoPorcentaje,
            activo = dto.Activo,
            updated_at = DateTimeOffset.UtcNow
        };
    }
}
