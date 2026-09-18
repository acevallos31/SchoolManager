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

    [HttpGet("usuarios")]
    public Task<IActionResult> ObtenerUsuarios(
        [FromQuery] Guid institucionId,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var permiso = conexion.CreateCommand();
            permiso.Transaction = tx;
            permiso.CommandText = """
                select public.usuario_tiene_permiso_institucional_estricto(
                  'identidad.usuarios.ver', @institucion_id
                )
                """;
            permiso.Parameters.AddWithValue("institucion_id", institucionId);
            var puedeVer = (bool?)await permiso.ExecuteScalarAsync(ct) ?? false;
            if (!puedeVer)
                return Forbid();

            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText = """
                with actor as (
                  select public.usuario_actual_id() as usuario_id
                ),
                alcance as (
                  select exists (
                    select 1
                    from actor a
                    join public.usuarios_roles ur
                      on ur.usuario_id = a.usuario_id
                     and ur.activo
                     and ur.institucion_id is null
                    join public.roles r
                      on r.id = ur.rol_id
                     and r.activo
                     and r.tipo = 'plataforma'
                     and r.codigo = 'platform_admin'
                  ) as es_platform_admin
                ),
                capacidad as (
                  select public.usuario_tiene_permiso_institucional_estricto(
                    'identidad.usuarios.editar', @institucion_id
                  ) as puede_editar
                )
                select coalesce(jsonb_agg(
                  jsonb_build_object(
                    'id', u.id,
                    'nombre', btrim(concat_ws(' ', pe.nombres, pe.apellidos)),
                    'nombres', pe.nombres,
                    'apellidos', pe.apellidos,
                    'correo', pe.correo,
                    'activo', u.activo,
                    'identidadVinculada', u.auth_user_id is not null,
                    'solicitudVinculacionId', (
                      select ia.id
                      from public.invitaciones_acceso ia
                      where ia.usuario_id = u.id
                        and ia.institucion_id = @institucion_id
                        and ia.estado = 'aceptada'
                        and ia.auth_user_id_solicitado is not null
                      order by ia.identidad_solicitada_at desc nulls last, ia.created_at desc
                      limit 1
                    ),
                    'identidadEstado', case
                      when u.auth_user_id is not null then 'vinculada'
                      when exists (
                        select 1
                        from public.invitaciones_acceso ia
                        where ia.usuario_id = u.id
                          and ia.institucion_id = @institucion_id
                          and ia.estado = 'aceptada'
                          and ia.auth_user_id_solicitado is not null
                      ) then 'pendiente_aprobacion'
                      else 'pendiente'
                    end,
                    'puedeEditar', c.puede_editar and pe.id is not null,
                    'roles', coalesce((
                      select jsonb_agg(
                        jsonb_build_object(
                          'asignacionId', ur2.id,
                          'rolId', r2.id,
                          'codigo', r2.codigo,
                          'nombre', r2.nombre
                        ) order by r2.nombre, r2.codigo
                      )
                      from public.usuarios_roles ur2
                      join public.roles r2
                        on r2.id = ur2.rol_id
                       and r2.activo
                       and r2.tipo = 'institucional'
                      where ur2.usuario_id = u.id
                        and ur2.institucion_id = @institucion_id
                        and ur2.activo
                    ), '[]'::jsonb)
                  ) order by pe.apellidos nulls last, pe.nombres nulls last, u.id
                ), '[]'::jsonb)::text
                from public.usuarios u
                left join public.personas pe on pe.id = u.persona_id
                cross join alcance a
                cross join capacidad c
                where a.es_platform_admin
                   or exists (
                     select 1
                     from public.usuarios_roles ur3
                     join public.roles r3
                       on r3.id = ur3.rol_id
                      and r3.activo
                      and r3.tipo = 'institucional'
                     where ur3.usuario_id = u.id
                       and ur3.institucion_id = @institucion_id
                       and ur3.activo
                   )
                """;
            comando.Parameters.AddWithValue("institucion_id", institucionId);

            var json = (string?)await comando.ExecuteScalarAsync(ct) ?? "[]";
            return Content(json, "application/json");
        }, ct);

    [HttpPut("usuarios/{usuarioId:guid}")]
    public Task<IActionResult> EditarUsuario(
        Guid usuarioId,
        [FromBody] EditarUsuarioPersonaDto dto,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            var nombres = dto.Nombres.Trim();
            var apellidos = dto.Apellidos.Trim();
            var correo = string.IsNullOrWhiteSpace(dto.Correo)
                ? null
                : dto.Correo.Trim().ToLowerInvariant();

            if (nombres.Length == 0 || apellidos.Length == 0)
                return BadRequest(new { error = "Nombres y apellidos son obligatorios." });

            await using var permiso = conexion.CreateCommand();
            permiso.Transaction = tx;
            permiso.CommandText = """
                select public.usuario_tiene_permiso_institucional_estricto(
                  'identidad.usuarios.editar', @institucion_id
                )
                """;
            permiso.Parameters.AddWithValue("institucion_id", dto.InstitucionId);
            var puedeEditar = (bool?)await permiso.ExecuteScalarAsync(ct) ?? false;
            if (!puedeEditar)
                return Forbid();

            await using var persona = conexion.CreateCommand();
            persona.Transaction = tx;
            persona.CommandText = """
                with actor as (
                  select public.usuario_actual_id() as usuario_id
                ),
                alcance as (
                  select exists (
                    select 1
                    from actor a
                    join public.usuarios_roles ur
                      on ur.usuario_id = a.usuario_id
                     and ur.activo
                     and ur.institucion_id is null
                    join public.roles r
                      on r.id = ur.rol_id
                     and r.activo
                     and r.tipo = 'plataforma'
                     and r.codigo = 'platform_admin'
                  ) as es_platform_admin
                )
                select u.persona_id
                from public.usuarios u
                cross join alcance a
                where u.id = @usuario_id
                  and (
                    a.es_platform_admin
                    or exists (
                      select 1
                      from public.usuarios_roles ur2
                      join public.roles r2
                        on r2.id = ur2.rol_id
                       and r2.activo
                       and r2.tipo = 'institucional'
                      where ur2.usuario_id = u.id
                        and ur2.institucion_id = @institucion_id
                        and ur2.activo
                    )
                  )
                """;
            persona.Parameters.AddWithValue("usuario_id", usuarioId);
            persona.Parameters.AddWithValue("institucion_id", dto.InstitucionId);
            var personaValor = await persona.ExecuteScalarAsync(ct);
            if (personaValor is not Guid personaId)
                return NotFound(new { error = "El usuario no existe en este contexto o no tiene una persona asociada." });

            await using var actualizar = conexion.CreateCommand();
            actualizar.Transaction = tx;
            actualizar.CommandText = """
                update public.personas
                set nombres = @nombres,
                    apellidos = @apellidos,
                    correo = @correo,
                    updated_at = clock_timestamp()
                where id = @persona_id
                """;
            actualizar.Parameters.AddWithValue("nombres", nombres);
            actualizar.Parameters.AddWithValue("apellidos", apellidos);
            actualizar.Parameters.AddWithValue("correo", (object?)correo ?? DBNull.Value);
            actualizar.Parameters.AddWithValue("persona_id", personaId);
            await actualizar.ExecuteNonQueryAsync(ct);

            await using var auditoria = conexion.CreateCommand();
            auditoria.Transaction = tx;
            auditoria.CommandText = """
                insert into public.seguridad_auditoria(
                  actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
                ) values (
                  public.usuario_actual_id(), @institucion_id,
                  'usuario.persona_editar', 'usuario', @usuario_id,
                  jsonb_build_object('persona_id', @persona_id, 'oauth_modificado', false)
                )
                """;
            auditoria.Parameters.AddWithValue("institucion_id", dto.InstitucionId);
            auditoria.Parameters.AddWithValue("usuario_id", usuarioId);
            auditoria.Parameters.AddWithValue("persona_id", personaId);
            await auditoria.ExecuteNonQueryAsync(ct);

            return NoContent();
        }, ct);

    [HttpPost("usuarios/invitaciones/preparar")]
    public Task<IActionResult> PrepararInvitacionUsuario(
        [FromBody] PrepararInvitacionUsuarioDto dto,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText = """
                select public.rpc_preparar_invitacion_usuario(
                  @institucion_id, @nombres, @apellidos, @correo, @rol_id, @origen
                )::text
                """;
            comando.Parameters.AddWithValue("institucion_id", dto.InstitucionId);
            comando.Parameters.AddWithValue("nombres", dto.Nombres);
            comando.Parameters.AddWithValue("apellidos", dto.Apellidos);
            comando.Parameters.AddWithValue("correo", dto.Correo);
            comando.Parameters.AddWithValue("rol_id", dto.RolId);
            comando.Parameters.AddWithValue("origen", dto.Origen);

            var json = (string?)await comando.ExecuteScalarAsync(ct) ?? "{}";
            return Content(json, "application/json");
        }, ct);

    [HttpPost("usuarios/{usuarioId:guid}/vinculacion")]
    public Task<IActionResult> OperarVinculacion(
        Guid usuarioId,
        [FromBody] OperarVinculacionIdentidadDto dto,
        CancellationToken ct) =>
        EnTransaccionComoUsuarioAsync(async (conexion, tx) =>
        {
            await using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText = """
                select public.rpc_operar_vinculacion_identidad(
                  @invitacion_id, @usuario_id, @institucion_id, @operacion, @motivo
                )::text
                """;
            comando.Parameters.AddWithValue("invitacion_id", dto.InvitacionId);
            comando.Parameters.AddWithValue("usuario_id", usuarioId);
            comando.Parameters.AddWithValue("institucion_id", dto.InstitucionId);
            comando.Parameters.AddWithValue("operacion", dto.Operacion);
            comando.Parameters.AddWithValue("motivo", (object?)dto.Motivo ?? DBNull.Value);

            var json = (string?)await comando.ExecuteScalarAsync(ct) ?? "{}";
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
