using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    private readonly SupabaseTableService _tableService;

    public PagosController(SupabaseTableService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOperadorOPadre")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? cargoId, [FromQuery] Guid? alumnoId, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["select"] = "*",
            ["order"] = "created_at.desc"
        };

        if (cargoId.HasValue)
        {
            query["cargo_id"] = $"eq.{cargoId.Value}";
        }

        if (alumnoId.HasValue)
        {
            var cargos = await _tableService.GetListAsync<CargoDto>(
                "cargos",
                new Dictionary<string, string?>
                {
                    ["select"] = "id",
                    ["alumno_id"] = $"eq.{alumnoId.Value}"
                },
                cancellationToken);

            if (cargos.Count == 0)
            {
                return Ok(Array.Empty<object>());
            }

            query["cargo_id"] = $"in.({string.Join(",", cargos.Select(cargo => cargo.Id))})";
        }

        try
        {
            var pagos = await _tableService.GetListAsync<Dictionary<string, object?>>("pagos", query, cancellationToken);
            return Ok(pagos);
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
            var pago = await _tableService.GetSingleAsync<Dictionary<string, object?>>(
                "pagos",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{id}"
                },
                cancellationToken);

            return pago is null ? NotFound(new { error = "Pago no encontrado." }) : Ok(pago);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOOperador")]
    public async Task<IActionResult> RegistrarPago([FromBody] RegistrarPagoCargoDto dto, CancellationToken cancellationToken)
    {
        var cargoId = dto.CargoIdEfectivo;
        var montoPagado = dto.MontoPagadoEfectivo;
        var metodoPago = dto.MetodoPagoEfectivo;

        if (cargoId == Guid.Empty)
        {
            return BadRequest(new { error = "Debes seleccionar el cargo a pagar." });
        }

        if (montoPagado <= 0)
        {
            return BadRequest(new { error = "El monto pagado debe ser mayor a cero." });
        }

        if (string.IsNullOrWhiteSpace(metodoPago))
        {
            return BadRequest(new { error = "El metodo de pago es obligatorio." });
        }

        try
        {
            var cargo = await _tableService.GetSingleAsync<CargoDto>(
                "cargos",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{cargoId}"
                },
                cancellationToken);

            if (cargo is null)
            {
                return NotFound(new { error = "Cargo no encontrado." });
            }

            if (cargo.Estado == "pagado")
            {
                return BadRequest(new { error = "Este cargo ya fue pagado." });
            }

            var recibo = $"REC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
            var pago = await _tableService.InsertAsync<Dictionary<string, object?>>(
                "pagos",
                new
                {
                    cargo_id = cargoId,
                    fecha_pago = DateOnly.FromDateTime(DateTime.UtcNow),
                    monto_pagado = montoPagado,
                    metodo_pago = metodoPago.Trim().ToLowerInvariant(),
                    numero_recibo = recibo,
                    registrado_por = TryGetUserGuid(),
                    created_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            await _tableService.UpdateAsync<CargoDto>(
                "cargos",
                cargoId,
                new
                {
                    estado = "pagado",
                    updated_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = pago["id"] }, new
            {
                pago,
                numeroRecibo = recibo,
                mensaje = "Pago registrado correctamente."
            });
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("mensualidad/{mensualidadId:guid}")]
    [Authorize(Policy = "AdminOperadorOPadre")]
    public async Task<IActionResult> GetPagosPorMensualidad(Guid mensualidadId, CancellationToken cancellationToken)
    {
        return await GetAll(mensualidadId, null, cancellationToken);
    }

    [HttpGet("{id:guid}/comprobante")]
    [Authorize(Policy = "AdminOperadorOPadre")]
    public async Task<IActionResult> GetComprobante(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var pago = await _tableService.GetSingleAsync<Dictionary<string, object?>>(
                "pagos",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{id}"
                },
                cancellationToken);

            if (pago is null)
            {
                return NotFound(new { error = "Pago no encontrado." });
            }

            var cargoId = TryReadGuid(pago, "cargo_id");
            var cargo = cargoId.HasValue
                ? await _tableService.GetSingleAsync<CargoDto>(
                    "cargos",
                    new Dictionary<string, string?> { ["select"] = "*", ["id"] = $"eq.{cargoId.Value}" },
                    cancellationToken)
                : null;

            var alumno = cargo is not null
                ? await _tableService.GetSingleAsync<AlumnoDto>(
                    "alumnos",
                    new Dictionary<string, string?> { ["select"] = "*", ["id"] = $"eq.{cargo.AlumnoId}" },
                    cancellationToken)
                : null;

            return Ok(new
            {
                pago,
                cargo,
                alumno,
                numeroRecibo = TryReadString(pago, "numero_recibo"),
                generadoEn = DateTimeOffset.UtcNow
            });
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> AnularPago(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var pagoActual = await _tableService.GetSingleAsync<Dictionary<string, object?>>(
                "pagos",
                new Dictionary<string, string?> { ["select"] = "*", ["id"] = $"eq.{id}" },
                cancellationToken);

            if (pagoActual is null)
            {
                return NotFound(new { error = "Pago no encontrado." });
            }

            var pago = await _tableService.UpdateAsync<Dictionary<string, object?>>(
                "pagos",
                id,
                new
                {
                    anulado = true,
                    updated_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            var cargoId = TryReadGuid(pagoActual, "cargo_id");
            if (cargoId.HasValue)
            {
                await _tableService.UpdateAsync<CargoDto>(
                    "cargos",
                    cargoId.Value,
                    new
                    {
                        estado = "pendiente",
                        updated_at = DateTimeOffset.UtcNow
                    },
                    cancellationToken);
            }

            return pago is null ? NotFound(new { error = "Pago no encontrado." }) : Ok(pago);
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

    private static Guid? TryReadGuid(Dictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : null;
    }

    private static string? TryReadString(Dictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
