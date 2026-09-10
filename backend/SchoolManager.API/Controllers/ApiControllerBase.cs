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
    private static readonly IReadOnlyDictionary<string, string> MensajesRestricciones =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["uq_matriculas_alumno_ciclo"] = "El alumno ya tiene una matrícula vigente en este ciclo escolar.",
            ["ux_personas_documento_normalizado"] = "Ya existe una persona con esa identificación.",
            ["ux_alumnos_rne_global"] = "Ya existe un alumno con ese RNE.",
            ["ux_alumnos_codigo_interno_por_institucion"] = "Ya existe un alumno con ese código interno en la institución.",
            ["uq_responsables_persona_institucion"] = "Esta persona ya está registrada como responsable en la institución.",
            ["uq_alumno_responsable"] = "Este responsable ya está vinculado con el alumno.",
            ["ux_alumno_responsable_principal_activo"] = "El alumno ya tiene un responsable principal activo.",
            ["uq_periodos_matricula_ciclo_nombre"] = "Ya existe un período de matrícula con ese nombre en el ciclo escolar.",
            ["ux_grados_institucion_nombre"] = "Ya existe un grado con ese nombre en la institución.",
            ["ux_jornadas_institucion_nombre"] = "Ya existe una jornada con ese nombre en la institución.",
            ["ux_secciones_contexto_jornada_nombre"] = "Ya existe una sección con ese nombre en el mismo contexto académico.",
            ["ux_secciones_contexto_sin_jornada_nombre"] = "Ya existe una sección con ese nombre en el mismo contexto académico.",
            ["ux_secciones_contexto_nombre"] = "Ya existe una sección con ese nombre en el mismo contexto académico.",
            ["ux_conceptos_financieros_nombre_normalizado"] = "Ya existe un concepto financiero con ese nombre.",
            ["ux_planes_pago_nombre_normalizado"] = "Ya existe un plan de pago con ese nombre.",
            ["ux_plan_cuotas_orden"] = "El orden de las cuotas no puede repetirse dentro del plan de pago.",
            ["uq_pagos_numero_recibo"] = "Ya existe un pago con ese número de recibo.",
            ["ux_pagos_referencia_externa_institucion"] = "Ya existe un pago con esa referencia externa en la institución."
        };

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

    private static string MensajeError(PostgresException ex)
    {
        if (!string.IsNullOrWhiteSpace(ex.ConstraintName) &&
            MensajesRestricciones.TryGetValue(ex.ConstraintName, out var mensaje))
        {
            return mensaje;
        }

        // Los RAISE de las RPC ya usan mensajes de negocio y normalmente no
        // incluyen ConstraintName. Se conservan tal cual; solo se oculta el
        // detalle tecnico de restricciones conocidas que PostgreSQL genera.
        return ex.MessageText ?? "Error en base de datos";
    }
}
