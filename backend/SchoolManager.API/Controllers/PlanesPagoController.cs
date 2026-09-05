using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlanesPagoController(NpgsqlDataSource dataSource) : ControllerBase
{
    private const string LecturaLista = "select * from public.rpc_listar_planes_pago(@institucionId, @activo)";
    private const string LecturaDetalle = "select * from public.rpc_obtener_plan_pago(@planId, @institucionId)";

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

    private static PlanPagoListaDto LeerLista(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Nombre = r.GetString(1),
        Descripcion = r.IsDBNull(2) ? null : r.GetString(2),
        Activo = r.GetBoolean(3),
        FechaDesactivacion = r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4),
        MotivoDesactivacion = r.IsDBNull(5) ? null : r.GetString(5),
        TotalCuotas = r.GetInt64(6),
        MontoTotal = r.GetDecimal(7)
    };

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

    private static string JsonbCuotas(IEnumerable<PlanCuotaInputDto> cuotas) =>
        JsonSerializer.Serialize(cuotas.Select(c => new
        {
            orden = c.Orden,
            concepto_id = c.ConceptoId?.ToString(),
            descripcion = c.Descripcion,
            monto = c.Monto,
            vencimiento_dias = c.VencimientoDias
        }));

    [HttpGet]
    [Authorize(Policy = Permisos.PlanesPago.Ver)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? institucionId,
        [FromQuery] bool? activo,
        CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = LecturaLista;
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("activo", (object?)activo ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<PlanPagoListaDto>();
            while (await r.ReadAsync(ct)) lista.Add(LeerLista(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permisos.PlanesPago.Ver)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = LecturaDetalle;
            cmd.Parameters.AddWithValue("planId", id);
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            PlanPagoDetalleDto? detalle = null;
            while (await r.ReadAsync(ct))
            {
                detalle ??= new PlanPagoDetalleDto
                {
                    Id = r.GetGuid(0),
                    Nombre = r.GetString(1),
                    Descripcion = r.IsDBNull(2) ? null : r.GetString(2),
                    Activo = r.GetBoolean(3)
                };
                if (!r.IsDBNull(4))
                    detalle.Cuotas.Add(new PlanCuotaDto
                    {
                        Id = r.GetGuid(4),
                        Orden = r.GetInt32(5),
                        ConceptoId = r.IsDBNull(6) ? null : r.GetGuid(6),
                        ConceptoNombre = r.IsDBNull(7) ? null : r.GetString(7),
                        Descripcion = r.IsDBNull(8) ? null : r.GetString(8),
                        Monto = r.GetDecimal(9),
                        VencimientoDias = r.GetInt32(10)
                    });
            }
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return detalle == null ? NotFound() : Ok(detalle);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost]
    [Authorize(Policy = Permisos.PlanesPago.Crear)]
    public async Task<IActionResult> Create([FromBody] PlanPagoUpsertDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre del plan es obligatorio" });
        if (dto.Cuotas.Count == 0)
            return BadRequest(new { error = "El plan debe incluir al menos una cuota" });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_crear_plan_pago(@nombre, @descripcion, @cuotas::jsonb, @institucionId)";
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("descripcion", (object?)dto.Descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cuotas", JsonbCuotas(dto.Cuotas));
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.PlanesPago.Editar)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PlanPagoUpsertDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre del plan es obligatorio" });
        if (dto.Cuotas.Count == 0)
            return BadRequest(new { error = "El plan debe incluir al menos una cuota" });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_actualizar_plan_pago(@id, @nombre, @descripcion, @cuotas::jsonb, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("descripcion", (object?)dto.Descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cuotas", JsonbCuotas(dto.Cuotas));
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permisos.PlanesPago.Desactivar)]
    public async Task<IActionResult> Desactivar(Guid id, [FromBody] DesactivarPlanDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return BadRequest(new { error = "El motivo es obligatorio" });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_plan_pago(@id, @motivo, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("motivo", dto.Motivo.Trim());
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Policy = Permisos.PlanesPago.Editar)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_plan_pago(@id, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}