using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConceptosFinancierosController(NpgsqlDataSource dataSource) : ControllerBase
{
    private const string LecturaConcepto = "select * from public.rpc_listar_conceptos_financieros(@institucionId, @activo)";

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

    private static ConceptoFinancieroDto Leer(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Nombre = r.GetString(1),
        Descripcion = r.IsDBNull(2) ? null : r.GetString(2),
        Monto = r.GetDecimal(3),
        Activo = r.GetBoolean(4),
        FechaDesactivacion = r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5),
        MotivoDesactivacion = r.IsDBNull(6) ? null : r.GetString(6)
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

    [HttpGet]
    [Authorize(Policy = Permisos.ConceptosFinancieros.Ver)]
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
            cmd.CommandText = LecturaConcepto;
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("activo", (object?)activo ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<ConceptoFinancieroDto>();
            while (await r.ReadAsync(ct)) lista.Add(Leer(r));
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return Ok(lista);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost]
    [Authorize(Policy = Permisos.ConceptosFinancieros.Crear)]
    public async Task<IActionResult> Create([FromBody] ConceptoFinancieroUpsertDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre del concepto es obligatorio" });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_crear_concepto_financiero(@nombre, @monto, @descripcion, @institucionId)";
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("monto", dto.Monto);
            cmd.Parameters.AddWithValue("descripcion", (object?)dto.Descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permisos.ConceptosFinancieros.Ver)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_listar_conceptos_financieros(null, null)";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var dto = Leer(r);
                if (dto.Id == id)
                {
                    await r.DisposeAsync();
                    await tx.CommitAsync(ct);
                    return Ok(dto);
                }
            }
            await r.DisposeAsync();
            await tx.CommitAsync(ct);
            return NotFound();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.ConceptosFinancieros.Editar)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ConceptoFinancieroUpsertDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre del concepto es obligatorio" });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_actualizar_concepto_financiero(@id, @nombre, @monto, @descripcion, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("monto", dto.Monto);
            cmd.Parameters.AddWithValue("descripcion", (object?)dto.Descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permisos.ConceptosFinancieros.Desactivar)]
    public async Task<IActionResult> Desactivar(Guid id, [FromBody] DesactivarConceptoDto dto, CancellationToken ct)
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
            cmd.CommandText = "select public.rpc_desactivar_concepto_financiero(@id, @motivo, @institucionId)";
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
    [Authorize(Policy = Permisos.ConceptosFinancieros.Editar)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_concepto_financiero(@id, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}