using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/estructura-academica")]
[Authorize]
// Controlador de la estructura academica (grados/jornadas/secciones), bloque
// 030E. Sigue el patron de CiclosEscolaresController: la autorizacion .NET
// (policy academico.estructura.*) se valida ANTES de invocar la DB; la RPC/RLS
// existente (configuracion.grados/jornadas/secciones.*, migracion 016/020)
// queda como segunda capa de invariantes. NO se reimplementa logica SQL/RPC en
// C#: cada endpoint delega en la RPC 016 correspondiente.
public class EstructuraAcademicaController(NpgsqlDataSource dataSource) : ApiControllerBase(dataSource)
{
    // ----- Grados academicos -----

    [HttpGet("grados")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Ver)]
    public async Task<IActionResult> ListarGrados(
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC de listado 016; la RPC resuelve el ambito
            // institucional y aplica su propia autorizacion (configuracion.grados.ver).
            cmd.CommandText = "select * from public.rpc_listar_grados(@institucionId)";
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<GradoDto>();
            while (await r.ReadAsync(ct))
            {
                lista.Add(new GradoDto
                {
                    Id = r.GetGuid(0),
                    Nombre = r.GetString(1),
                    Orden = r.GetInt32(2),
                    Activo = r.GetBoolean(3),
                });
            }
            await r.DisposeAsync();
            return Ok(lista);
        }, ct);
    }

    [HttpPost("grados")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> CrearGrado(
        [FromBody] GradoInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre es obligatorio." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 016 (valida duplicados, rango y permiso interno).
            cmd.CommandText = "select public.rpc_crear_grado(@nombre, @orden, @institucionId)";
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("orden", dto.Orden);
            cmd.Parameters.AddWithValue("institucionId", (object?)dto.InstitucionId ?? DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return CreatedAtAction(nameof(ListarGrados), new { id }, new { id });
        }, ct);
    }

    [HttpPut("grados/{id:guid}")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> ActualizarGrado(
        Guid id,
        [FromQuery] Guid? institucionId,
        [FromBody] GradoInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre es obligatorio." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 016 (revalida duplicados, rango y pertenencia).
            cmd.CommandText = "select public.rpc_actualizar_grado(@id, @nombre, @orden, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("orden", dto.Orden);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("grados/{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Desactivar)]
    public async Task<IActionResult> DesactivarGrado(
        Guid id,
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        // El grado no exige motivo; la desactivacion soft se delega en la RPC.
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_grado(@id, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("grados/{id:guid}/reactivar")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> ReactivarGrado(
        Guid id,
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_grado(@id, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    // ----- Jornadas academicas -----

    [HttpGet("jornadas")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Ver)]
    public async Task<IActionResult> ListarJornadas(
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC de listado 016 (configuracion.jornadas.ver interna).
            cmd.CommandText = "select * from public.rpc_listar_jornadas(@institucionId)";
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<JornadaDto>();
            while (await r.ReadAsync(ct))
            {
                lista.Add(new JornadaDto
                {
                    Id = r.GetGuid(0),
                    Nombre = r.GetString(1),
                    Activo = r.GetBoolean(2),
                });
            }
            await r.DisposeAsync();
            return Ok(lista);
        }, ct);
    }

    [HttpPost("jornadas")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> CrearJornada(
        [FromBody] JornadaInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre es obligatorio." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 016 (valida duplicados y permiso interno).
            cmd.CommandText = "select public.rpc_crear_jornada(@nombre, @institucionId)";
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("institucionId", (object?)dto.InstitucionId ?? DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return CreatedAtAction(nameof(ListarJornadas), new { id }, new { id });
        }, ct);
    }

    [HttpPut("jornadas/{id:guid}")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> ActualizarJornada(
        Guid id,
        [FromQuery] Guid? institucionId,
        [FromBody] JornadaInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre es obligatorio." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 016 (revalida duplicados y pertenencia).
            cmd.CommandText = "select public.rpc_actualizar_jornada(@id, @nombre, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("jornadas/{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Desactivar)]
    public async Task<IActionResult> DesactivarJornada(
        Guid id,
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        // La jornada no exige motivo; la desactivacion soft se delega en la RPC.
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_jornada(@id, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("jornadas/{id:guid}/reactivar")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> ReactivarJornada(
        Guid id,
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_jornada(@id, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    // ----- Secciones -----

    [HttpGet("secciones")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Ver)]
    public async Task<IActionResult> ListarSecciones(
        [FromQuery] Guid cicloId,
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        // cicloId es obligatorio para acotar el listado a un ciclo escolar.
        if (cicloId == Guid.Empty)
            return BadRequest(new { error = "cicloId es obligatorio." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC de listado 016 (configuracion.secciones.ver interna).
            cmd.CommandText = "select * from public.rpc_listar_secciones(@cicloId, @institucionId)";
            cmd.Parameters.AddWithValue("cicloId", cicloId);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<SeccionDto>();
            while (await r.ReadAsync(ct))
            {
                lista.Add(new SeccionDto
                {
                    Id = r.GetGuid(0),
                    InstitucionId = r.GetGuid(1),
                    CicloId = r.GetGuid(2),
                    GradoId = r.GetGuid(3),
                    GradoNombre = r.GetString(4),
                    JornadaId = r.IsDBNull(5) ? null : r.GetGuid(5),
                    JornadaNombre = r.IsDBNull(6) ? null : r.GetString(6),
                    Nombre = r.GetString(7),
                    Cupo = r.IsDBNull(8) ? null : r.GetInt32(8),
                    Activo = r.GetBoolean(9),
                    FechaDesactivacion = r.IsDBNull(10) ? null : r.GetFieldValue<DateTimeOffset>(10),
                    MotivoDesactivacion = r.IsDBNull(11) ? null : r.GetString(11),
                });
            }
            await r.DisposeAsync();
            return Ok(lista);
        }, ct);
    }

    [HttpPost("secciones")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> CrearSeccion(
        [FromBody] SeccionInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre es obligatorio." });
        if (dto.CicloId == Guid.Empty || dto.GradoId == Guid.Empty)
            return BadRequest(new { error = "cicloId y gradoId son obligatorios." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 016 (valida duplicados, cupo y permiso interno).
            cmd.CommandText = "select public.rpc_crear_seccion(@institucionId, @cicloId, @gradoId, @jornadaId, @nombre, @cupo)";
            cmd.Parameters.AddWithValue("institucionId", (object?)dto.InstitucionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cicloId", dto.CicloId);
            cmd.Parameters.AddWithValue("gradoId", dto.GradoId);
            cmd.Parameters.AddWithValue("jornadaId", (object?)dto.JornadaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("cupo", (object?)dto.Cupo ?? DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return CreatedAtAction(nameof(ListarSecciones), new { id }, new { id });
        }, ct);
    }

    [HttpPut("secciones/{id:guid}")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> ActualizarSeccion(
        Guid id,
        [FromQuery] Guid? institucionId,
        [FromBody] SeccionInputDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { error = "El nombre es obligatorio." });
        if (dto.CicloId == Guid.Empty || dto.GradoId == Guid.Empty)
            return BadRequest(new { error = "cicloId y gradoId son obligatorios." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            // Delega en la RPC 016 (revalida duplicados, cupo y pertenencia).
            cmd.CommandText = "select public.rpc_actualizar_seccion(@id, @cicloId, @gradoId, @jornadaId, @nombre, @cupo, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("cicloId", dto.CicloId);
            cmd.Parameters.AddWithValue("gradoId", dto.GradoId);
            cmd.Parameters.AddWithValue("jornadaId", (object?)dto.JornadaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("nombre", dto.Nombre.Trim());
            cmd.Parameters.AddWithValue("cupo", (object?)dto.Cupo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("secciones/{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Desactivar)]
    public async Task<IActionResult> DesactivarSeccion(
        Guid id,
        [FromQuery] Guid? institucionId,
        [FromBody] DesactivarDto dto,
        CancellationToken ct)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) return BadRequest(new { error = "El motivo es obligatorio." });
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_seccion(@id, @motivo, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("motivo", motivo);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }

    [HttpPost("secciones/{id:guid}/reactivar")]
    [Authorize(Policy = Permisos.EstructuraAcademica.Editar)]
    public async Task<IActionResult> ReactivarSeccion(
        Guid id,
        [FromQuery] Guid? institucionId,
        CancellationToken ct)
    {
        return await EnTransaccionComoUsuarioAsync(async (c, tx) =>
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_seccion(@id, @institucionId)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return NoContent();
        }, ct);
    }
}
