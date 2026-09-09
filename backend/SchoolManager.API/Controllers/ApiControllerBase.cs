using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Npgsql;

namespace SchoolManager.API.Controllers;

/// <summary>
/// Base compartida para los controllers que acceden a Postgres como el
/// usuario autenticado. Centraliza el boilerplate de conexion y la
/// traduccion de errores SQL a respuestas HTTP, evitando la duplicacion
/// y la deriva entre controllers (los codigos de contexto SM001/SM003
/// deben mapearse igual en todas partes).
/// </summary>
[ApiController]
public abstract class ApiControllerBase(NpgsqlDataSource dataSource) : ControllerBase
{
    // Centraliza el ciclo de vida repetido en las operaciones académicas.
    // Cada callback conserva su SQL/RPC; errores y respuestas de rechazo
    // disponen la transacción sin commit y el claim nunca sale de su ámbito.
    protected async Task<IActionResult> EnTransaccionComoUsuarioAsync(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<IActionResult>> operacion,
        CancellationToken ct)
    {
        try
        {
            await using var conexion = await AbrirComoUsuarioAsync(ct);
            await using var tx = await conexion.BeginTransactionAsync(ct);
            await FijarClaimAsync(conexion, tx, User.FindFirstValue("sub")!, ct);
            var resultado = await operacion(conexion, tx);
            if (resultado is not IStatusCodeActionResult { StatusCode: >= 400 })
                await tx.CommitAsync(ct);
            return resultado;
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    protected async Task<NpgsqlConnection> AbrirComoUsuarioAsync(CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            throw new UnauthorizedAccessException("El claim sub del JWT es obligatorio.");
        return await dataSource.OpenConnectionAsync(ct);
    }

    protected static async Task FijarClaimAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sub, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "select set_config('request.jwt.claim.sub', @sub, true)";
        cmd.Parameters.AddWithValue("sub", sub);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    protected static ObjectResult ToError(PostgresException ex) =>
        new ObjectResult(new { error = MensajeError(ex) })
        {
            // P0001 (raise_exception generico) no se trata como 403: cae en el default.
            // "SM001"/"SM003" son codigos de contexto de la implementacion (validacion
            // de negocio) y se mapean a 400, igual que los codigos 22023/23503.
            StatusCode = ex.SqlState switch
            {
                "42501" => StatusCodes.Status403Forbidden,
                "P0002" => StatusCodes.Status404NotFound,
                "23505" or "23514" => StatusCodes.Status409Conflict,
                "22023" or "23503" or "SM001" or "SM003" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            }
        };

    private static string MensajeError(PostgresException ex) =>
        ex.SqlState == PostgresErrorCodes.UniqueViolation &&
        ex.ConstraintName == "uq_matriculas_alumno_ciclo"
            ? "El alumno ya tiene una matrícula vigente en este ciclo escolar."
            : ex.MessageText ?? "Error en base de datos";
}
