using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

// Gestion de pagos/cobranza (Bloque 021). Cada pago es un hecho transaccional
// que se aplica a uno o varios cargos del alumno (pagos_aplicaciones). El saldo
// de los cargos es derivado (monto_original - aplicaciones vigentes), nunca
// almacenado; anular un pago revierte sus aplicaciones y recalcula los estados
// dentro de la misma transaccion, dejando trazabilidad (sin DELETE).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController(NpgsqlDataSource dataSource) : ApiControllerBase(dataSource)
{
    // Columnas de rpc_listar_pagos_alumno / rpc_obtener_pago (15).
    private static PagoDto LeerPago(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        InstitucionId = r.GetGuid(1),
        AlumnoId = r.GetGuid(2),
        ResponsableId = r.IsDBNull(3) ? null : r.GetGuid(3),
        NumeroRecibo = r.GetInt64(4),
        MontoTotal = r.GetDecimal(5),
        FechaPago = r.GetFieldValue<DateTimeOffset>(6),
        MetodoPago = r.IsDBNull(7) ? null : r.GetString(7),
        ReferenciaExterna = r.IsDBNull(8) ? null : r.GetString(8),
        Estado = r.GetString(9),
        RegistradoPor = r.IsDBNull(10) ? null : r.GetGuid(10),
        FechaAnulacion = r.IsDBNull(11) ? null : r.GetFieldValue<DateTimeOffset>(11),
        AnuladoPor = r.IsDBNull(12) ? null : r.GetGuid(12),
        MotivoAnulacion = r.IsDBNull(13) ? null : r.GetString(13),
        CreatedAt = r.GetFieldValue<DateTimeOffset>(14),
    };

    // Columnas de rpc_obtener_aplicaciones_pago (10).
    private static AplicacionPagoDto LeerAplicacion(NpgsqlDataReader r) => new()
    {
        AplicacionId = r.GetGuid(0),
        PagoId = r.GetGuid(1),
        CargoId = r.GetGuid(2),
        InstitucionId = r.GetGuid(3),
        MontoAplicado = r.GetDecimal(4),
        Estado = r.GetString(5),
        FechaReversion = r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
        CargoEstado = r.GetString(7),
        ConceptoNombre = r.IsDBNull(8) ? null : r.GetString(8),
        MontoOriginal = r.GetDecimal(9),
    };

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
            while (await r.ReadAsync(ct)) lista.Add(LeerPago(r));
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
            var dto = LeerPago(r);
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
            while (await r.ReadAsync(ct)) lista.Add(LeerAplicacion(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Pagos.Registrar)]
    public async Task<IActionResult> Registrar(Guid alumnoId, [FromBody] RegistrarPagoDto dto, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        if (dto.MontoTotal <= 0)
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
            cmd.Parameters.AddWithValue("montoTotal", dto.MontoTotal);
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
