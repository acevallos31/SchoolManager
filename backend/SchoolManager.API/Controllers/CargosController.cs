using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

// Gestion de obligaciones financieras (cargos) por matricula/alumno.
// En 020: listar por matricula/alumno, resumen de saldo, asignar plan a
// matricula, generar cargos desde el plan (atomico) y anular cargo (soft).
// En 021 (en main): pagos/cobranza; el estado pagado/parcial deriva del
// saldo y se sincroniza por triggers.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CargosController(NpgsqlDataSource dataSource) : ApiControllerBase(dataSource)
{
    [HttpGet("matricula/{matriculaId:guid}")]
    [Authorize(Policy = Permisos.Cargos.Ver)]
    public async Task<IActionResult> GetPorMatricula(Guid matriculaId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_listar_cargos_matricula(@matriculaId, @institucionId)";
            cmd.Parameters.AddWithValue("matriculaId", matriculaId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<CargoDto>();
            while (await r.ReadAsync(ct)) lista.Add(FinanzasDataReader.LeerCargo(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpGet("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Cargos.Ver)]
    public async Task<IActionResult> GetPorAlumno(Guid alumnoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_listar_cargos_alumno(@alumnoId, @institucionId)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<CargoDto>();
            while (await r.ReadAsync(ct)) lista.Add(FinanzasDataReader.LeerCargo(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpGet("alumno/{alumnoId:guid}/resumen")]
    [Authorize(Policy = Permisos.Cargos.Ver)]
    public async Task<IActionResult> GetResumenAlumno(Guid alumnoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_resumen_financiero_alumno(@alumnoId, @institucionId)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
            {
                await r.DisposeAsync();
                await tx.CommitAsync(ct);
                return NotFound(new { error = "El alumno no existe." });
            }
            var dto = FinanzasDataReader.LeerResumen(r);
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(dto);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("matricula/{matriculaId:guid}/plan")]
    [Authorize(Policy = Permisos.Cargos.Generar)]
    public async Task<IActionResult> AsignarPlan(Guid matriculaId, [FromBody] AsignarPlanPagoDto dto, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        if (!dto.PlanPagoId.HasValue || dto.PlanPagoId == Guid.Empty)
            return BadRequest(new { error = "El plan de pago es obligatorio" });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_asignar_plan_pago_matricula(@matriculaId, @planPagoId, @institucionId)";
            cmd.Parameters.AddWithValue("matriculaId", matriculaId);
            cmd.Parameters.AddWithValue("planPagoId", dto.PlanPagoId.Value);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteScalarAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("matricula/{matriculaId:guid}/generar")]
    [Authorize(Policy = Permisos.Cargos.Generar)]
    public async Task<IActionResult> Generar(Guid matriculaId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_generar_cargos_matricula(@matriculaId, @institucionId)";
            cmd.Parameters.AddWithValue("matriculaId", matriculaId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            var total = (int)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return Ok(new { generados = total });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("{cargoId:guid}/anular")]
    [Authorize(Policy = Permisos.Cargos.Anular)]
    public async Task<IActionResult> Anular(Guid cargoId, [FromBody] AnularCargoDto dto, [FromQuery] Guid? institucionId, CancellationToken ct)
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
            cmd.CommandText = "select public.rpc_anular_cargo(@cargoId, @motivo, @institucionId)";
            cmd.Parameters.AddWithValue("cargoId", cargoId);
            cmd.Parameters.AddWithValue("motivo", motivo);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}
