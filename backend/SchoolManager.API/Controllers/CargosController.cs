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
// No hay pagos reales: pagado/parcial son bloque 021.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CargosController(NpgsqlDataSource dataSource) : ControllerBase
{
    private async Task<NpgsqlConnection> AbrirComoUsuarioAsync(CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            throw new UnauthorizedAccessException("El claim sub del JWT es obligatorio.");
        return await dataSource.OpenConnectionAsync(ct);
    }

    private static async Task FijarClaimAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sub, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "select set_config('request.jwt.claim.sub', @sub, true)";
        cmd.Parameters.AddWithValue("sub", sub);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private ObjectResult ToError(PostgresException ex) =>
        new ObjectResult(new { error = ex.MessageText ?? "Error en base de datos" })
        {
            StatusCode = ex.SqlState switch
            {
                "42501" => StatusCodes.Status403Forbidden,
                "P0002" => StatusCodes.Status404NotFound,
                "23505" or "23514" => StatusCodes.Status409Conflict,
                "22023" or "23503" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            }
        };

    // Columnas de rpc_listar_cargos_matricula / rpc_listar_cargos_alumno (15).
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
    };

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
            while (await r.ReadAsync(ct)) lista.Add(LeerCargo(r));
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
            while (await r.ReadAsync(ct)) lista.Add(LeerCargo(r));
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
            var dto = new ResumenFinancieroDto
            {
                AlumnoId = r.GetGuid(0),
                InstitucionId = r.GetGuid(1),
                TotalObligaciones = r.GetInt64(2),
                TotalMontoOriginal = r.GetDecimal(3),
                TotalPendiente = r.GetDecimal(4),
                TotalVencido = r.GetDecimal(5),
                TotalAnulado = r.GetDecimal(6),
            };
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
        if (dto.PlanPagoId == Guid.Empty)
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
            cmd.Parameters.AddWithValue("planPagoId", dto.PlanPagoId);
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
