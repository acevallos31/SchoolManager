using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
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

            var pagos = await GetPagosDeCargosAsync(cargos, cancellationToken);

            return Ok(new
            {
                alumnoId,
                cargos = cargos.Select(ToLegacyMensualidad),
                pagos,
                pagados = cargos.Where(cargo => cargo.Estado == "pagado").Select(ToLegacyMensualidad),
                pendientes = cargos.Where(cargo => cargo.Estado is "pendiente" or "vencido").Select(ToLegacyMensualidad),
                resumen = BuildResumen(cargos, pagos)
            });
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("estado-cuenta/mis-alumnos")]
    [Authorize(Policy = "AdminOPadre")]
    public async Task<IActionResult> GetMiEstadoCuenta(CancellationToken cancellationToken)
    {
        var possibleTutorIds = GetPossibleTutorIds();

        if (possibleTutorIds.Count == 0)
        {
            return Unauthorized(new { error = "No se pudo identificar el usuario actual." });
        }

        try
        {
            var tutorFilter = string.Join(",", possibleTutorIds.Select(id => $"tutor_id.eq.{id}"));
            var alumnos = await _tableService.GetListAsync<AlumnoDto>(
                "alumnos",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["or"] = $"({tutorFilter})",
                    ["estado"] = "eq.activo",
                    ["order"] = "nombres.asc"
                },
                cancellationToken);

            var detalle = new List<object>();
            decimal totalPendiente = 0;
            decimal totalVencido = 0;
            decimal totalPagado = 0;

            foreach (var alumno in alumnos)
            {
                var cargos = await _tableService.GetListAsync<CargoDto>(
                    "cargos",
                    new Dictionary<string, string?>
                    {
                        ["select"] = "*",
                        ["alumno_id"] = $"eq.{alumno.Id}",
                        ["order"] = "fecha_vencimiento.asc"
                    },
                    cancellationToken);

                var pagos = await GetPagosDeCargosAsync(cargos, cancellationToken);
                var resumen = BuildResumen(cargos, pagos);

                totalPendiente += resumen.TotalPendiente;
                totalVencido += resumen.TotalVencido;
                totalPagado += resumen.TotalPagado;

                detalle.Add(new
                {
                    alumno,
                    cargos = cargos.Select(ToLegacyMensualidad),
                    pagos,
                    resumen
                });
            }

            return Ok(new
            {
                alumnos = detalle,
                resumen = new
                {
                    totalAlumnos = alumnos.Count,
                    totalPendiente,
                    totalVencido,
                    totalPagado,
                    totalSaldo = totalPendiente + totalVencido
                }
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

    private async Task<IReadOnlyList<Dictionary<string, object?>>> GetPagosDeCargosAsync(
        IReadOnlyList<CargoDto> cargos,
        CancellationToken cancellationToken)
    {
        if (cargos.Count == 0)
        {
            return [];
        }

        return await _tableService.GetListAsync<Dictionary<string, object?>>(
            "pagos",
            new Dictionary<string, string?>
            {
                ["select"] = "*",
                ["cargo_id"] = $"in.({string.Join(",", cargos.Select(cargo => cargo.Id))})",
                ["order"] = "fecha_pago.desc"
            },
            cancellationToken);
    }

    private static EstadoCuentaResumen BuildResumen(
        IReadOnlyList<CargoDto> cargos,
        IReadOnlyList<Dictionary<string, object?>> pagos)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var totalPendiente = cargos
            .Where(cargo => cargo.Estado == "pendiente" && cargo.FechaVencimiento >= today)
            .Sum(cargo => cargo.Monto);
        var totalVencido = cargos
            .Where(cargo => cargo.Estado == "vencido" || (cargo.Estado == "pendiente" && cargo.FechaVencimiento < today))
            .Sum(cargo => cargo.Monto);
        var totalPagado = pagos
            .Where(pago => !TryReadBool(pago, "anulado"))
            .Sum(pago => TryReadDecimal(pago, "monto_pagado"));

        return new EstadoCuentaResumen(totalPendiente, totalVencido, totalPagado, totalPendiente + totalVencido);
    }

    private List<Guid> GetPossibleTutorIds()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var supabaseUid = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return new[]
            {
                Guid.TryParse(usuarioId, out var profileId) ? profileId : (Guid?)null,
                Guid.TryParse(supabaseUid, out var authId) ? authId : (Guid?)null
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private static decimal TryReadDecimal(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue),
            int intValue => intValue,
            long longValue => longValue,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var parsed) => parsed,
            _ => decimal.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }

    private static bool TryReadBool(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool boolValue => boolValue,
            JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            _ => bool.TryParse(value.ToString(), out var parsed) && parsed
        };
    }

    private sealed record EstadoCuentaResumen(
        decimal TotalPendiente,
        decimal TotalVencido,
        decimal TotalPagado,
        decimal TotalSaldo);
}
