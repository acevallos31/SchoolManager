using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

// Portal del responsable (Bloque 022): superficie de SOLO LECTURA para el
// usuario autenticado cuyo persona_id es responsable financiero activo de un
// alumno. Reutiliza el modelo financiero real de 021 (cargos, saldo derivado,
// resumen, pagos, aplicaciones) via RPC dedicadas rpc_*_responsable.
//
// Seguridad:
//   * La identidad se resuelve SIEMPRE en la DB desde el claim sub (auth.uid());
//     este controller NO acepta un responsable/institucion arbitrario del cliente.
//   * Las RPC lanzan 'Acceso denegado.' (42501 -> 403) de forma uniforme tambien
//     para alumnos inexistentes/ajenos, sin revelar existencia, y aislan por
//     institucion (multi-tenant).
//   * Solo lectura: no hay endpoints de escritura aqui (el portal NO paga).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortalResponsableController(NpgsqlDataSource dataSource)
    : ApiControllerBase(dataSource)
{
    // Columnas de rpc_mis_alumnos_responsable() (6).
    private static MisAlumnoDto LeerMisAlumno(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        InstitucionId = r.GetGuid(1),
        Nombres = r.IsDBNull(2) ? null : r.GetString(2),
        Apellidos = r.IsDBNull(3) ? null : r.GetString(3),
        Parentesco = r.IsDBNull(4) ? null : r.GetString(4),
        EsPrincipal = r.GetBoolean(5),
    };

    // Columnas de rpc_cargos_responsable (17, igual layout que listar_cargos).
    private static CargoDto LeerCargo(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        MatriculaId = r.GetGuid(1),
        AlumnoId = r.GetGuid(2),
        PlanPagoId = r.IsDBNull(3) ? null : r.GetGuid(3),
        Orden = r.GetInt32(4),
        ConceptoId = r.IsDBNull(5) ? null : r.GetGuid(5),
        ConceptoNombre = r.IsDBNull(6) ? null : r.GetString(6),
        Descripcion = r.IsDBNull(7) ? null : r.GetString(7),
        MontoOriginal = r.GetDecimal(8),
        FechaVencimiento = r.GetFieldValue<DateOnly>(9),
        Estado = r.GetString(10),
        FechaGeneracion = r.GetFieldValue<DateTimeOffset>(11),
        FechaAnulacion = r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12),
        MotivoAnulacion = r.IsDBNull(13) ? null : r.GetString(13),
        EsVencido = r.GetBoolean(14),
        Saldo = r.GetDecimal(15),
        Aplicado = r.GetDecimal(16),
    };

    // Columnas de rpc_pagos_responsable (15, igual layout que listar_pagos).
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

    // Columnas de rpc_pago_aplicaciones_responsable (10, igual layout que
    // rpc_obtener_aplicaciones_pago).
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

    /// <summary>Hijos del usuario autenticado como responsable financiero activo.</summary>
    [HttpGet("mis-alumnos")]
    public async Task<IActionResult> MisAlumnos(CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_mis_alumnos_responsable()";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<MisAlumnoDto>();
            while (await r.ReadAsync(ct)) lista.Add(LeerMisAlumno(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    /// <summary>Resumen financiero real del hijo (saldo derivado, vencidos, aplicado).</summary>
    [HttpGet("alumnos/{alumnoId:guid}/resumen")]
    public async Task<IActionResult> Resumen(Guid alumnoId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_resumen_financiero_responsable(@alumnoId, null)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
            {
                await r.DisposeAsync();
                await tx.CommitAsync(ct);
                return NotFound(new { error = "No hay informacion financiera disponible." });
            }
            var dto = new ResumenFinancieroDto
            {
                AlumnoId = r.GetGuid(0),
                InstitucionId = r.GetGuid(1),
                TotalObligaciones = r.GetInt64(2),
                TotalMontoOriginal = r.GetDecimal(3),
                TotalPendiente = r.GetDecimal(4),
                TotalVencido = r.GetDecimal(5),
                TotalAnulado = r.GetDecimal(6),
                TotalAplicado = r.GetDecimal(7),
            };
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(dto);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    /// <summary>Cargos del hijo (pendientes/parciales/pagados/anulados, saldo derivado).</summary>
    [HttpGet("alumnos/{alumnoId:guid}/cargos")]
    public async Task<IActionResult> Cargos(Guid alumnoId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_cargos_responsable(@alumnoId, null)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<CargoDto>();
            while (await r.ReadAsync(ct)) lista.Add(LeerCargo(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    /// <summary>Historial real de pagos del hijo.</summary>
    [HttpGet("alumnos/{alumnoId:guid}/pagos")]
    public async Task<IActionResult> Pagos(Guid alumnoId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_pagos_responsable(@alumnoId, null)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<PagoDto>();
            while (await r.ReadAsync(ct)) lista.Add(LeerPago(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    /// <summary>Aplicaciones de un pago del hijo (detalle: a que cargo se aplico).</summary>
    [HttpGet("pagos/{pagoId:guid}/aplicaciones")]
    public async Task<IActionResult> Aplicaciones(Guid pagoId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_pago_aplicaciones_responsable(@pagoId, null)";
            cmd.Parameters.AddWithValue("pagoId", pagoId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<AplicacionPagoDto>();
            while (await r.ReadAsync(ct)) lista.Add(LeerAplicacion(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}
