using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MensualidadesController : ControllerBase
{
    private readonly SupabaseTableService _tableService;

    public MensualidadesController(SupabaseTableService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOperadorOPadre")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? alumnoId, [FromQuery] string? estado, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["select"] = "*",
            ["or"] = "(tipo.eq.mensualidad,tipo.eq.pago_anual)",
            ["order"] = "fecha_vencimiento.asc"
        };

        if (alumnoId.HasValue)
        {
            query["alumno_id"] = $"eq.{alumnoId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query["estado"] = $"eq.{estado.Trim().ToLowerInvariant()}";
        }

        try
        {
            var cargos = await _tableService.GetListAsync<CargoDto>("cargos", query, cancellationToken);
            return Ok(cargos.Select(ToLegacyMensualidad));
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AdminOperadorOPadre")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var cargo = await _tableService.GetSingleAsync<CargoDto>(
                "cargos",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{id}"
                },
                cancellationToken);

            return cargo is null ? NotFound(new { error = "Mensualidad no encontrada." }) : Ok(ToLegacyMensualidad(cargo));
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] MensualidadCreateDto dto)
    {
        return BadRequest(new
        {
            error = "Las mensualidades ahora se generan automaticamente al finalizar una matricula."
        });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOOperador")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Dictionary<string, object?> dto, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["updated_at"] = DateTimeOffset.UtcNow
        };

        if (dto.TryGetValue("estado", out var estado) && estado is not null)
        {
            payload["estado"] = estado.ToString()?.Trim().ToLowerInvariant();
        }

        try
        {
            var cargo = await _tableService.UpdateAsync<CargoDto>("cargos", id, payload, cancellationToken);
            return cargo is null ? NotFound(new { error = "Mensualidad no encontrada." }) : Ok(ToLegacyMensualidad(cargo));
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("generar-anio")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult GenerarAnioEscolar([FromBody] object dto)
    {
        return BadRequest(new
        {
            error = "La generacion anual se realiza desde la matricula y el plan de pago."
        });
    }

    [HttpGet("estado-cuenta/{alumnoId:guid}")]
    [Authorize(Policy = "AdminOperadorOPadre")]
    public async Task<IActionResult> GetEstadoCuenta(Guid alumnoId, CancellationToken cancellationToken)
    {
        try
        {
            var cargos = await _tableService.GetListAsync<CargoDto>(
                "cargos",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["alumno_id"] = $"eq.{alumnoId}",
                    ["order"] = "fecha_vencimiento.asc"
                },
                cancellationToken);

            return Ok(new
            {
                alumnoId,
                pagados = cargos.Where(cargo => cargo.Estado == "pagado").Select(ToLegacyMensualidad),
                pendientes = cargos.Where(cargo => cargo.Estado is "pendiente" or "vencido").Select(ToLegacyMensualidad)
            });
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("generar/{cicloId:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult GenerarMensualidades(Guid cicloId, [FromQuery] decimal monto)
    {
        return BadRequest(new
        {
            error = "Usa el flujo de matricula con plan de pago para generar cargos."
        });
    }

    [HttpPut("{id:guid}/descuento")]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult AplicarDescuento(Guid id, [FromBody] DescuentoDto dto)
    {
        return BadRequest(new
        {
            error = "Los descuentos se aplicaran desde el modulo de becas y ajustes administrativos."
        });
    }

    private static object ToLegacyMensualidad(CargoDto cargo)
    {
        return new
        {
            id = cargo.Id,
            alumno_id = cargo.AlumnoId,
            matricula_id = cargo.MatriculaId,
            mes = cargo.FechaVencimiento.Month,
            anio = cargo.FechaVencimiento.Year,
            concepto = cargo.Concepto,
            monto_original = cargo.Monto,
            descuento = 0,
            monto_final = cargo.Monto,
            estado = cargo.Estado,
            fecha_limite = cargo.FechaVencimiento,
            fecha_vencimiento = cargo.FechaVencimiento
        };
    }
}
