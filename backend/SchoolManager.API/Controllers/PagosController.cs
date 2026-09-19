using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

// Gestion de pagos/cobranza (Bloque 021). Cada pago es un hecho transaccional
// que se aplica a uno o varios cargos del alumno (pagos_aplicaciones). El saldo
// de los cargos es derivado (monto_original - aplicaciones vigentes), nunca
// almacenado; anular un pago revierte sus aplicaciones y recalcula los estados
// dentro de la misma transaccion, dejando trazabilidad (sin DELETE).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController(NpgsqlDataSource dataSource, IDocumentoReciboService documentoRecibo)
    : ApiControllerBase(dataSource)
{
    [HttpGet("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Pagos.Ver)]
    public async Task<IActionResult> ListarPorAlumno(Guid alumnoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_listar_pagos_alumno(@alumnoId, @institucionId)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<PagoDto>();
            while (await r.ReadAsync(ct)) lista.Add(FinanzasDataReader.LeerPago(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpGet("{pagoId:guid}")]
    [Authorize(Policy = Permisos.Pagos.Ver)]
    public async Task<IActionResult> Obtener(Guid pagoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_obtener_pago(@pagoId, @institucionId)";
            cmd.Parameters.AddWithValue("pagoId", pagoId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
            {
                await r.DisposeAsync();
                await tx.CommitAsync(ct);
                return NotFound(new { error = "El pago no existe." });
            }
            var dto = FinanzasDataReader.LeerPago(r);
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(dto);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpGet("{pagoId:guid}/aplicaciones")]
    [Authorize(Policy = Permisos.Pagos.Ver)]
    public async Task<IActionResult> ObtenerAplicaciones(Guid pagoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_obtener_aplicaciones_pago(@pagoId, @institucionId)";
            cmd.Parameters.AddWithValue("pagoId", pagoId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<AplicacionPagoDto>();
            while (await r.ReadAsync(ct)) lista.Add(FinanzasDataReader.LeerAplicacion(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    // Recibo de pago (Bloque 047A). El controller es delgado: delega toda la
    // composicion autoritativa en la capa de aplicacion (IDocumentoReciboService),
    // que reutiliza rpc_obtener_pago + rpc_obtener_aplicaciones_pago dentro de una
    // unica transaccion. El frontend solo presenta/imprime/descarga el DTO.
    [HttpGet("{pagoId:guid}/recibo")]
    [Authorize(Policy = Permisos.Pagos.Ver)]
    public async Task<IActionResult> ObtenerRecibo(Guid pagoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            var dto = await documentoRecibo.ObtenerReciboPagoAsync(pagoId, institucionId, User.FindFirstValue("sub")!, ct);
            return dto is null ? NotFound(new { error = "El pago no existe." }) : Ok(dto);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Pagos.Registrar)]
    public async Task<IActionResult> Registrar(Guid alumnoId, [FromBody] RegistrarPagoDto dto, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        if (!dto.MontoTotal.HasValue || dto.MontoTotal.Value <= 0)
            return BadRequest(new { error = "El monto total del pago debe ser mayor a cero" });
        if (dto.Aplicaciones is null || dto.Aplicaciones.Count == 0)
            return BadRequest(new { error = "El pago debe aplicarse a al menos un cargo" });
        if (dto.Aplicaciones.Any(a => a.CargoId == Guid.Empty || a.Monto <= 0))
            return BadRequest(new { error = "Las aplicaciones del pago son invalidas" });
        try
        {
            // La DB valida atómicamente contexto (institucion/alumno), sobrepago,
            // suma == monto_total, responsable valido y referencia unica.
            var aplicacionesJson = "[" + string.Join(",",
                dto.Aplicaciones.Select(a =>
                    $"{{\"cargo_id\":\"{a.CargoId:N}\",\"monto\":{a.Monto.ToString(CultureInfo.InvariantCulture)}}}")) + "]";

            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_registrar_pago(@alumnoId, @aplicaciones::jsonb, @montoTotal, @institucionId, @responsableId, @metodoPago, @referenciaExterna, @fechaPago)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            cmd.Parameters.AddWithValue("aplicaciones", aplicacionesJson);
            cmd.Parameters.AddWithValue("montoTotal", dto.MontoTotal.Value);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("responsableId", (object?)dto.ResponsableId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("metodoPago", (object?)dto.MetodoPago ?? DBNull.Value);
            cmd.Parameters.AddWithValue("referenciaExterna", (object?)dto.ReferenciaExterna ?? DBNull.Value);
            cmd.Parameters.AddWithValue("fechaPago", (object?)dto.FechaPago ?? DBNull.Value);
            var pagoId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(Obtener), new { pagoId }, new { id = pagoId });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("{pagoId:guid}/anular")]
    [Authorize(Policy = Permisos.Pagos.Anular)]
    public async Task<IActionResult> Anular(Guid pagoId, [FromBody] AnularPagoDto dto, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
            return BadRequest(new { error = "El motivo de anulacion es obligatorio" });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_anular_pago(@pagoId, @motivo, @institucionId)";
            cmd.Parameters.AddWithValue("pagoId", pagoId);
            cmd.Parameters.AddWithValue("motivo", motivo);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}
