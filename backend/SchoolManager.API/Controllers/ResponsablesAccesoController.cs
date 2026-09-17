using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace SchoolManager.API.Controllers;

/// <summary>
/// Operaciones de identidad para responsables existentes. La Persona objetivo
/// se resuelve exclusivamente desde responsable_id dentro de PostgreSQL.
/// </summary>
[ApiController]
[Route("api/responsables")]
[Authorize]
public sealed class ResponsablesAccesoController(NpgsqlDataSource dataSource)
    : ApiControllerBase(dataSource)
{
    [HttpPost("{responsableId:guid}/invitacion-acceso/preparar")]
    public Task<IActionResult> PrepararInvitacionAcceso(
        Guid responsableId,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText =
                "select public.rpc_preparar_invitacion_responsable(@responsable_id)::text";
            comando.Parameters.AddWithValue("responsable_id", responsableId);

            var json = (string?)await comando.ExecuteScalarAsync(ct) ?? "{}";
            return Content(json, "application/json");
        }, ct);
}
