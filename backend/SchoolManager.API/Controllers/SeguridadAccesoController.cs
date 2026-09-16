using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

/// <summary>
/// Superficie administrativa de Configuración > Seguridad y acceso.
/// La autorización institucional efectiva permanece en PostgreSQL mediante
/// las RPC RBAC y la autoridad estricta introducida en las migraciones 036+.
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

    [HttpPost("roles")]
    public Task<IActionResult> CrearRol(
        [FromBody] CrearRolInstitucionalDto dto,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText = """
                select public.rpc_crear_rol_institucional(
                  @institucion_id, @codigo, @nombre, @descripcion
                )
                """;
            comando.Parameters.AddWithValue("institucion_id", dto.InstitucionId);
            comando.Parameters.AddWithValue("codigo", dto.Codigo);
            comando.Parameters.AddWithValue("nombre", dto.Nombre);
            comando.Parameters.AddWithValue("descripcion", (object?)dto.Descripcion ?? DBNull.Value);
            var id = (Guid)(await comando.ExecuteScalarAsync(ct))!;
            return Ok(new { id });
        }, ct);

    [HttpPost("roles/clonar")]
    public Task<IActionResult> ClonarPlantilla(
        [FromBody] ClonarPlantillaRolDto dto,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText = """
                select public.rpc_clonar_plantilla_rol(
                  @institucion_id, @plantilla_codigo, @codigo, @nombre, @descripcion
                )
                """;
            comando.Parameters.AddWithValue("institucion_id", dto.InstitucionId);
            comando.Parameters.AddWithValue("plantilla_codigo", dto.PlantillaCodigo);
            comando.Parameters.AddWithValue("codigo", dto.Codigo);
            comando.Parameters.AddWithValue("nombre", dto.Nombre);
            comando.Parameters.AddWithValue("descripcion", (object?)dto.Descripcion ?? DBNull.Value);
            var id = (Guid)(await comando.ExecuteScalarAsync(ct))!;
            return Ok(new { id });
        }, ct);

    [HttpPut("roles/{rolId:guid}")]
    public Task<IActionResult> EditarRol(
        Guid rolId,
        [FromBody] EditarRolInstitucionalDto dto,
        CancellationToken ct) =>
        EjecutarSinResultadoAsync(
            "select public.rpc_editar_rol_institucional(@rol_id, @nombre, @descripcion)",
            comando =>
            {
                comando.Parameters.AddWithValue("rol_id", rolId);
                comando.Parameters.AddWithValue("nombre", dto.Nombre);
                comando.Parameters.AddWithValue("descripcion", (object?)dto.Descripcion ?? DBNull.Value);
            }, ct);

    [HttpPut("roles/{rolId:guid}/permisos")]
    public Task<IActionResult> ReemplazarPermisos(
        Guid rolId,
        [FromBody] ReemplazarPermisosRolDto dto,
        CancellationToken ct) =>
        EjecutarSinResultadoAsync(
            "select public.rpc_reemplazar_permisos_rol_institucional(@rol_id, @permisos)",
            comando =>
            {
                comando.Parameters.AddWithValue("rol_id", rolId);
                comando.Parameters.AddWithValue("permisos", dto.Permisos);
            }, ct);

    [HttpPost("roles/{rolId:guid}/asignaciones")]
    public Task<IActionResult> AsignarRol(
        Guid rolId,
        [FromBody] AsignarRolInstitucionalDto dto,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText =
                "select public.rpc_asignar_rol_institucional(@usuario_id, @rol_id)";
            comando.Parameters.AddWithValue("usuario_id", dto.UsuarioId);
            comando.Parameters.AddWithValue("rol_id", rolId);
            var id = (Guid)(await comando.ExecuteScalarAsync(ct))!;
            return Ok(new { id });
        }, ct);

    [HttpPost("roles/{rolId:guid}/desactivar")]
    public Task<IActionResult> DesactivarRol(
        Guid rolId,
        [FromBody] DesactivarRbacDto dto,
        CancellationToken ct) =>
        EjecutarSinResultadoAsync(
            "select public.rpc_desactivar_rol_institucional(@rol_id, @motivo)",
            comando =>
            {
                comando.Parameters.AddWithValue("rol_id", rolId);
                comando.Parameters.AddWithValue("motivo", dto.Motivo);
            }, ct);

    [HttpPost("asignaciones/{usuarioRolId:guid}/desactivar")]
    public Task<IActionResult> DesactivarAsignacion(
        Guid usuarioRolId,
        [FromBody] DesactivarRbacDto dto,
        CancellationToken ct) =>
        EjecutarSinResultadoAsync(
            "select public.rpc_desactivar_rol_usuario(@usuario_rol_id, @motivo)",
            comando =>
            {
                comando.Parameters.AddWithValue("usuario_rol_id", usuarioRolId);
                comando.Parameters.AddWithValue("motivo", dto.Motivo);
            }, ct);

    private Task<IActionResult> EjecutarSinResultadoAsync(
        string sql,
        Action<NpgsqlCommand> configurar,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText = sql;
            configurar(comando);
            await comando.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
}
