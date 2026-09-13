using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace SchoolManager.API.Controllers;

/// <summary>
/// Superficie administrativa de Configuración > Seguridad y acceso.
/// La autorización institucional efectiva permanece en PostgreSQL mediante
/// rpc_obtener_seguridad_acceso y la autoridad estricta de RBAC.
/// </summary>
[Route("api/configuracion/seguridad")]
[Authorize]
public sealed class SeguridadAccesoController(NpgsqlDataSource dataSource)
    : ApiControllerBase(dataSource)
{
    [HttpGet]
    public Task<IActionResult> Obtener(
        [FromQuery] Guid institucionId,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText = "select public.rpc_obtener_seguridad_acceso(@institucion_id)";
            comando.Parameters.AddWithValue("institucion_id", institucionId);

            var valor = await comando.ExecuteScalarAsync(ct);
            var json = valor as string ?? "{}";
            return Content(json, "application/json");
        }, ct);
}
