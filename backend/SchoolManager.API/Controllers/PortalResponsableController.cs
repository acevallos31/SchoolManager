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
            var dto = FinanzasDataReader.LeerResumen(r);
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
            while (await r.ReadAsync(ct)) lista.Add(FinanzasDataReader.LeerCargo(r));
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
            while (await r.ReadAsync(ct)) lista.Add(FinanzasDataReader.LeerPago(r));
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
            while (await r.ReadAsync(ct)) lista.Add(FinanzasDataReader.LeerAplicacion(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}
