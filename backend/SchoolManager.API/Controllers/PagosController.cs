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
    public async Task<IActionResult> GetAll([FromQuery] Guid? cargoId, CancellationToken cancellationToken)
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
        if (dto.CargoId == Guid.Empty)
        {
            return BadRequest(new { error = "Debes seleccionar el cargo a pagar." });
        }

        if (dto.MontoPagado <= 0)
        {
            return BadRequest(new { error = "El monto pagado debe ser mayor a cero." });
        }

        try
        {
            var cargo = await _tableService.GetSingleAsync<CargoDto>(
                "cargos",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{dto.CargoId}"
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
                    cargo_id = dto.CargoId,
                    fecha_pago = DateOnly.FromDateTime(DateTime.UtcNow),
                    monto_pagado = dto.MontoPagado,
                    metodo_pago = dto.MetodoPago.Trim().ToLowerInvariant(),
                    numero_recibo = recibo,
                    registrado_por = TryGetUserGuid(),
                    created_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            await _tableService.UpdateAsync<CargoDto>(
                "cargos",
                dto.CargoId,
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
    public IActionResult GetPagosPorMensualidad(Guid mensualidadId)
    {
        return Ok(new { mensaje = "Pagos de mensualidad", mensualidadId });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> AnularPago(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var pago = await _tableService.UpdateAsync<Dictionary<string, object?>>(
                "pagos",
                id,
                new
                {
                    anulado = true,
                    updated_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

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
}
