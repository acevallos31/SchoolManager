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
            var ciclo = await _tableService.GetSingleAsync<CicloEscolarDto>(
                "ciclos_escolares",
                new Dictionary<string, string?> { ["select"] = "*", ["id"] = $"eq.{dto.CicloId}" },
                cancellationToken);

            if (ciclo is null)
            {
                return BadRequest(new { error = "El ciclo escolar seleccionado no existe." });
            }

            var matriculaExistente = await _tableService.GetSingleAsync<MatriculaDto>(
                TableName,
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["alumno_id"] = $"eq.{dto.AlumnoId}",
                    ["ciclo_id"] = $"eq.{dto.CicloId}"
                },
                cancellationToken);

            if (matriculaExistente is not null)
            {
                return Conflict(new { error = "Este alumno ya tiene una matricula registrada para el ciclo seleccionado." });
            }

            var plan = await _tableService.GetSingleAsync<PlanPagoDto>(
                "planes_pago",
                new Dictionary<string, string?> { ["select"] = "*", ["id"] = $"eq.{dto.PlanPagoId}" },
                cancellationToken);

            if (plan is null || !plan.Activo)
            {
                return BadRequest(new { error = "El plan de pago seleccionado no existe o esta inactivo." });
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (ciclo.MatriculaInicio.HasValue
                && ciclo.MatriculaFin.HasValue
                && (today < ciclo.MatriculaInicio.Value || today > ciclo.MatriculaFin.Value))
            {
                return BadRequest(new
                {
                    error = $"El periodo de matricula para {ciclo.Nombre} esta cerrado. Vigente del {ciclo.MatriculaInicio:yyyy-MM-dd} al {ciclo.MatriculaFin:yyyy-MM-dd}."
                });
            }

            var montoMatricula = dto.Monto > 0 ? dto.Monto : plan.MontoMatricula;
            var payload = new
            {
                alumno_id = dto.AlumnoId,
                ciclo_id = dto.CicloId,
                grado_id = dto.GradoId,
                seccion_id = dto.SeccionId,
                plan_pago_id = dto.PlanPagoId,
                fecha_matricula = today,
                monto = montoMatricula,
                estado = "pendiente",
                registrado_por = TryGetUserGuid(),
                created_at = DateTimeOffset.UtcNow
            };

            var matricula = await _tableService.InsertAsync<MatriculaDto>(TableName, payload, cancellationToken);
            var cargos = await GenerarCargosAsync(matricula, ciclo, plan, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = matricula.Id }, new
            {
                matricula,
                facturasGeneradas = cargos.Count,
                facturas = cargos,
                mensaje = "Matricula registrada. La factura de matricula y las facturas del plan fueron generadas correctamente."
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

    private async Task<IReadOnlyList<CargoDto>> GenerarCargosAsync(
        MatriculaDto matricula,
        CicloEscolarDto ciclo,
        PlanPagoDto plan,
        CancellationToken cancellationToken)
    {
        var cargos = new List<CargoDto>();
        var now = DateTimeOffset.UtcNow;

        if (plan.MontoMatricula > 0)
        {
            cargos.Add(await _tableService.InsertAsync<CargoDto>(
                "cargos",
                new
                {
                    matricula_id = matricula.Id,
                    alumno_id = matricula.AlumnoId,
                    tipo = "matricula",
                    concepto = $"Factura de matricula - {ciclo.Nombre}",
                    numero_cuota = (int?)null,
                    monto = plan.MontoMatricula,
                    fecha_vencimiento = ciclo.MatriculaFin ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    estado = "pendiente",
                    created_at = now
                },
                cancellationToken));
        }

        var cantidadCuotas = Math.Max(1, plan.CantidadCuotas);

        if (plan.MontoTotalAnual <= 0 || cantidadCuotas <= 0)
        {
            return cargos;
        }

        var montoCuota = Math.Round(CalcularMontoFinanciado(plan) / cantidadCuotas, 2, MidpointRounding.AwayFromZero);
        var baseYear = ciclo.FechaInicio.Year;
        var baseDate = SafeDate(baseYear, Math.Clamp(plan.MesInicio, 1, 12), Math.Clamp(plan.DiaVencimiento, 1, 28));

        for (var cuota = 1; cuota <= cantidadCuotas; cuota++)
        {
            var vencimiento = DateOnly.FromDateTime(baseDate.AddMonths(cuota - 1));
            var estado = vencimiento < DateOnly.FromDateTime(DateTime.UtcNow) ? "vencido" : "pendiente";

            cargos.Add(await _tableService.InsertAsync<CargoDto>(
                "cargos",
                new
                {
                    matricula_id = matricula.Id,
                    alumno_id = matricula.AlumnoId,
                    tipo = cantidadCuotas == 1 ? "pago_anual" : "mensualidad",
                    concepto = BuildConcepto(plan, cuota, cantidadCuotas),
                    numero_cuota = cuota,
                    monto = montoCuota,
                    fecha_vencimiento = vencimiento,
                    estado,
                    created_at = now
                },
                cancellationToken));
        }

        return cargos;
    }

    private Guid? TryGetUserGuid()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }

    private static decimal CalcularMontoFinanciado(PlanPagoDto plan)
    {
        var descuento = plan.DescuentoPorcentaje <= 0
            ? 0
            : plan.MontoTotalAnual * (plan.DescuentoPorcentaje / 100);

        return Math.Max(0, plan.MontoTotalAnual - descuento);
    }

    private static DateTime SafeDate(int year, int month, int day)
    {
        var safeDay = Math.Min(day, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, safeDay, 0, 0, 0, DateTimeKind.Utc);
    }

    private static string BuildConcepto(PlanPagoDto plan, int cuota, int total)
    {
        return total == 1
            ? $"Factura anual - {plan.Nombre}"
            : $"Factura {plan.Nombre} - cuota {cuota} de {total}";
    }
}
