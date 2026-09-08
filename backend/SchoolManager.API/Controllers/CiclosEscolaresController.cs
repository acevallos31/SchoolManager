using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/ciclos-escolares")]
[Authorize]
// Controlador del modulo Ciclos/Periodos (Bloque 030D). Sigue el patron de
// AlumnosController/MatriculasController: la autorizacion .NET (policy
// academico.ciclos.*) se valida ANTES de invocar la DB; la RPC/RLS existente
// (configuracion.ciclos.* / configuracion.periodos_matricula.*, migracion 014)
// queda como segunda capa de invariantes. NO se reimplementa logica SQL/RPC en
// C#: cada endpoint delega en la RPC 014 correspondiente.
public class CiclosEscolaresController(NpgsqlDataSource dataSource) : ApiControllerBase(dataSource)
{
    // ----- Ciclos escolares -----

    [HttpGet]
    [Authorize(Policy = Permisos.CiclosEscolares.Ver)]
    public async Task<IActionResult> ListarCiclos(
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC de listado 014; la RPC resuelve el ambito
            // institucional y aplica su propia autorizacion (configuracion.ciclos.ver).
            cmd.CommandText = "select * from public.rpc_listar_ciclos_escolares(@institucionId)";
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<CicloEscolarDto>();
            while (await r.ReadAsync(ct))
            {
                lista.Add(new CicloEscolarDto
                {
                    Id = r.GetGuid(0),
                    InstitucionId = r.GetGuid(1),
                    Nombre = r.GetString(2),
                    FechaInicio = r.IsDBNull(3) ? default : r.GetFieldValue<DateOnly>(3),
                    FechaFin = r.IsDBNull(4) ? default : r.GetFieldValue<DateOnly>(4),
                    Activo = r.GetBoolean(5),
                    MotivoDesactivacion = r.IsDBNull(7) ? null : r.GetString(7),
                });
            }
            await r.DisposeAsync();
            return Ok(lista);
        }, ct);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.CiclosEscolares.Crear)]
    public async Task<IActionResult> CrearCiclo(
        [FromBody] CicloEscolarInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)
            || dto.FechaInicio == default || dto.FechaFin == default)
            return BadRequest(new { error = "Nombre y rango de fechas son obligatorios." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 014 (valida rangos, duplicados y permiso interno).
            cmd.CommandText = "select public.rpc_crear_ciclo_escolar(@nombre, @inicio, @fin, @institucionId)";
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("inicio", dto.FechaInicio);
            cmd.Parameters.AddWithValue("fin", dto.FechaFin);
            cmd.Parameters.AddWithValue("institucionId", (object?)dto.InstitucionId ?? DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return CreatedAtAction(nameof(ListarCiclos), new { id }, new { id });
        }, ct);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.CiclosEscolares.Editar)]
    public async Task<IActionResult> ActualizarCiclo(
        Guid id,
        [FromBody] CicloEscolarInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)
            || dto.FechaInicio == default || dto.FechaFin == default)
            return BadRequest(new { error = "Nombre y rango de fechas son obligatorios." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            // Conserva el estado de activacion actual del ciclo. La actualizacion
            // de metadatos no cambia el estado; reactivar/desactivar tienen su
            // propio endpoint. Se lee el activo con scope institucional real.
            var activo = await LeerActivoCicloAsync(c, tx, id, dto.InstitucionId, ct);
            if (activo is null)
                return NotFound();

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 014 (revalida rangos, periodo cubiertos y permiso).
            cmd.CommandText = "select public.rpc_actualizar_ciclo_escolar(@id, @nombre, @inicio, @fin, @activo, null)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("inicio", dto.FechaInicio);
            cmd.Parameters.AddWithValue("fin", dto.FechaFin);
            cmd.Parameters.AddWithValue("activo", activo.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.CiclosEscolares.Desactivar)]
    public async Task<IActionResult> DesactivarCiclo(
        Guid id,
        [FromBody] DesactivarDto dto,
        CancellationToken ct)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) return BadRequest(new { error = "El motivo es obligatorio." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_ciclo_escolar(@id, @motivo)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("motivo", motivo);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Policy = Permisos.CiclosEscolares.Editar)]
    public async Task<IActionResult> ReactivarCiclo(Guid id, CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_ciclo_escolar(@id)";
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    // ----- Periodos de matricula -----

    [HttpGet("{id:guid}/periodos")]
    [Authorize(Policy = Permisos.CiclosEscolares.Ver)]
    public async Task<IActionResult> ListarPeriodos(Guid id, CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select * from public.rpc_listar_periodos_matricula(@cicloId)";
            cmd.Parameters.AddWithValue("cicloId", id);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<PeriodoMatriculaDto>();
            while (await r.ReadAsync(ct))
            {
                lista.Add(new PeriodoMatriculaDto
                {
                    Id = r.GetGuid(0),
                    CicloId = r.GetGuid(1),
                    Nombre = r.GetString(2),
                    Tipo = r.IsDBNull(3) ? null : r.GetString(3),
                    FechaInicio = r.IsDBNull(4) ? default : r.GetFieldValue<DateOnly>(4),
                    FechaFin = r.IsDBNull(5) ? default : r.GetFieldValue<DateOnly>(5),
                    Activo = r.GetBoolean(6),
                });
            }
            await r.DisposeAsync();
            return Ok(lista);
        }, ct);
    }

    [HttpPost("{id:guid}/periodos")]
    [Authorize(Policy = Permisos.CiclosEscolares.Editar)]
    public async Task<IActionResult> CrearPeriodo(
        Guid id,
        [FromBody] PeriodoMatriculaInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)
            || dto.FechaInicio == default || dto.FechaFin == default)
            return BadRequest(new { error = "Nombre y rango de fechas son obligatorios." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_crear_periodo_matricula(@cicloId, @nombre, @tipo, @inicio, @fin)";
            cmd.Parameters.AddWithValue("cicloId", id);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("tipo", (object?)(string.IsNullOrWhiteSpace(dto.Tipo) ? null : dto.Tipo.Trim()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("inicio", dto.FechaInicio);
            cmd.Parameters.AddWithValue("fin", dto.FechaFin);
            var nuevoId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return CreatedAtAction(nameof(ListarPeriodos), new { id }, new { id = nuevoId });
        }, ct);
    }

    [HttpPut("{id:guid}/periodos/{periodoId:guid}")]
    [Authorize(Policy = Permisos.CiclosEscolares.Editar)]
    public async Task<IActionResult> ActualizarPeriodo(
        Guid id,
        Guid periodoId,
        [FromBody] PeriodoMatriculaInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)
            || dto.FechaInicio == default || dto.FechaFin == default)
            return BadRequest(new { error = "Nombre y rango de fechas son obligatorios." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            // Conserva el estado de activacion del periodo (misma logica que el ciclo).
            var activo = await LeerActivoPeriodoAsync(c, tx, periodoId, id, ct);
            if (activo is null)
                return NotFound();

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_actualizar_periodo_matricula(@id, @nombre, @tipo, @inicio, @fin, @activo)";
            cmd.Parameters.AddWithValue("id", periodoId);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("tipo", (object?)(string.IsNullOrWhiteSpace(dto.Tipo) ? null : dto.Tipo.Trim()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("inicio", dto.FechaInicio);
            cmd.Parameters.AddWithValue("fin", dto.FechaFin);
            cmd.Parameters.AddWithValue("activo", activo.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("{id:guid}/periodos/{periodoId:guid}/desactivar")]
    [Authorize(Policy = Permisos.CiclosEscolares.Desactivar)]
    public async Task<IActionResult> DesactivarPeriodo(Guid id, Guid periodoId, CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_periodo_matricula(@id)";
            cmd.Parameters.AddWithValue("id", periodoId);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("{id:guid}/periodos/{periodoId:guid}/reactivar")]
    [Authorize(Policy = Permisos.CiclosEscolares.Editar)]
    public async Task<IActionResult> ReactivarPeriodo(Guid id, Guid periodoId, CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_periodo_matricula(@id)";
            cmd.Parameters.AddWithValue("id", periodoId);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    // ----- Helpers (solo lectura de estado; no reimplementa reglas de negocio) -----

    private static async Task<bool?> LeerActivoCicloAsync(
        NpgsqlConnection c,
        NpgsqlTransaction tx,
        Guid cicloId,
        Guid? institucionId,
        CancellationToken ct)
    {
        // Lee el estado de activacion del ciclo acotando contra el ambito
        // institucional real de la FILA (no un resolver global). Si el usuario
        // no tiene academico.ciclos.ver en la institucion del ciclo o el ciclo
        // no existe, devuelve null (404); la RPC 014 revalida luego el permiso
        // segun la operacion (editar/desactivar).
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            select ce.activo
            from public.ciclos_escolares ce
            where ce.id = @id
              and public.usuario_tiene_permiso_actual('academico.ciclos.ver', ce.institucion_id)
            """; // NOSONAR:csharpsquid:S2077 (fragmento fijo; valores por NpgsqlParameter)
        cmd.Parameters.AddWithValue("id", cicloId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : (bool)result;
    }

    private static async Task<bool?> LeerActivoPeriodoAsync(
        NpgsqlConnection c,
        NpgsqlTransaction tx,
        Guid periodoId,
        Guid cicloId,
        CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            select pm.activo
            from public.periodos_matricula pm
            join public.ciclos_escolares ce on ce.id = pm.ciclo_id
            where pm.id = @id
              and pm.ciclo_id = @cicloId
              and public.usuario_tiene_permiso_actual('academico.ciclos.ver', ce.institucion_id)
            """; // NOSONAR:csharpsquid:S2077 (fragmento fijo; valores por NpgsqlParameter)
        cmd.Parameters.AddWithValue("id", periodoId);
        cmd.Parameters.AddWithValue("cicloId", cicloId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : (bool)result;
    }
}
