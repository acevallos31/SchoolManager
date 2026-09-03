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
public class MatriculasController(NpgsqlDataSource dataSource) : ControllerBase
{
    private static readonly string[] EstadosTerminales =
        { "retirada", "anulada", "trasladada" };

    private const string LecturaBase = """
        select
          m.id,
          m.alumno_id,
          m.institucion_id,
          m.ciclo_id,
          m.seccion_id,
          m.periodo_matricula_id,
          m.registrado_por,
          m.fecha_matricula,
          m.estado,
          m.fecha_anulacion,
          m.motivo_anulacion,
          m.created_at,
          (trim(coalesce(p.nombres, '')) || ' '|| trim(coalesce(p.apellidos, ''))) as nombre_alumno,
          s.nombre as nombre_seccion,
          g.nombre as nombre_grado,
          j.nombre as nombre_jornada,
          pm.nombre as nombre_periodo,
          c.nombre as ciclo_nombre
        from public.matriculas m
        join public.alumnos a on a.id = m.alumno_id
        join public.personas p on p.id = a.persona_id
        join public.secciones s on s.id = m.seccion_id
        left join public.grados g on g.id = s.grado_id
        left join public.jornadas j on j.id = s.jornada_id
        join public.periodos_matricula pm on pm.id = m.periodo_matricula_id
        join public.ciclos_escolares c on c.id = m.ciclo_id
        where m.institucion_id = public.resolver_institucion_operacion(@institucionId)
    """;

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

    private MatriculaDto? Leer(NpgsqlDataReader r)
    {
        return new MatriculaDto
        {
            Id = r.GetGuid(0),
            AlumnoId = r.GetGuid(1),
            InstitucionId = r.GetGuid(2),
            CicloId = r.GetGuid(3),
            SeccionId = r.GetGuid(4),
            PeriodoMatriculaId = r.GetGuid(5),
            RegistradoPor = r.IsDBNull(6) ? null : r.GetGuid(6),
            FechaMatricula = r.GetFieldValue<DateOnly>(7),
            Estado = r.GetString(8),
            FechaAnulacion = r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9),
            MotivoAnulacion = r.IsDBNull(10) ? null : r.GetString(10),
            CreatedAt = r.GetFieldValue<DateTimeOffset>(11),
            NombreAlumno = r.GetString(12),
            NombreSeccion = r.GetString(13),
            NombreGrado = r.IsDBNull(14) ? string.Empty : r.GetString(14),
            NombreJornada = r.IsDBNull(15) ? null : r.GetString(15),
            NombrePeriodo = r.GetString(16),
            CicloNombre = r.GetString(17),
        };
    }

    private ObjectResult ToError(PostgresException ex) =>
        new ObjectResult(new { error = ex.MessageText ?? "Error en base de datos" })
        {
            StatusCode = ex.SqlState switch
            {
                "23505" => StatusCodes.Status409Conflict,
                "P0001" or "P0002" or "42501" => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            }
        };

    [HttpGet]
    [Authorize(Policy = Permisos.Matriculas.Ver)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? alumnoId, [FromQuery] Guid? institucionId, CancellationToken ct)
    {
        await using var c1 = await AbrirComoUsuarioAsync(ct);
        await using var tx = await c1.BeginTransactionAsync(ct);
        await FijarClaimAsync(c1, tx, User.FindFirstValue("sub")!, ct);
        string sql = LecturaBase;
        if (alumnoId.HasValue) sql += " and m.alumno_id = @alumnoId";
        await using var cmd = c1.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
        if (alumnoId.HasValue) cmd.Parameters.AddWithValue("alumnoId", alumnoId.Value);
        await using var r2 = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<MatriculaDto>();
        while (await r2.ReadAsync(ct)) lista.Add(Leer(r2)!);
        await tx.CommitAsync(ct);
        return Ok(lista);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permisos.Matriculas.Ver)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        await using var c2 = await AbrirComoUsuarioAsync(ct);
        await using var tx = await c2.BeginTransactionAsync(ct);
        await FijarClaimAsync(c2, tx, User.FindFirstValue("sub")!, ct);
        await using var cmd = c2.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = LecturaBase + " and m.id = @id";
        cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
        cmd.Parameters.AddWithValue("id", id);
        await using var r3 = await cmd.ExecuteReaderAsync(ct);
        MatriculaDto? dto = await r3.ReadAsync(ct) ? Leer(r3) : null;
        await tx.CommitAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.Matriculas.Crear)]
    public async Task<IActionResult> Create([FromBody] MatriculaCreateDto dto, CancellationToken ct)
    {
        if (dto.AlumnoId == Guid.Empty || dto.SeccionId == Guid.Empty || dto.PeriodoMatriculaId == Guid.Empty)
            return BadRequest(new { error = "Faltan datos obligatorios" });
        try
        {
            await using var c4 = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c4.BeginTransactionAsync(ct);
            await FijarClaimAsync(c4, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c4.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_matricular_alumno(@a, @s, @pm)";
            cmd.Parameters.AddWithValue("a", dto.AlumnoId);
            cmd.Parameters.AddWithValue("s", dto.SeccionId);
            cmd.Parameters.AddWithValue("pm", dto.PeriodoMatriculaId);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPut("{id:guid}/estado")]
    [Authorize(Policy = Permisos.Matriculas.CambiarEstado)]
    public async Task<IActionResult> ActualizarEstado(Guid id, [FromBody] CambiarEstadoMatriculaDto dto, CancellationToken ct)
    {
        var estado = (dto.Estado ?? string.Empty).Trim();
        if (estado.Length == 0 || (EstadosTerminales.Contains(estado, StringComparer.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(dto.Motivo)))
            return BadRequest(new { error = "Estado invalido o falta motivo" });
        try
        {
            await using var c5 = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c5.BeginTransactionAsync(ct);
            await FijarClaimAsync(c5, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c5.CreateCommand();
            cmd.Transaction = tx;
            string? motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim();
            cmd.CommandText = "select public.rpc_cambiar_estado_matricula(@id, @estado, @motivo)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("estado", estado);
            cmd.Parameters.AddWithValue("motivo", (object?)motivo ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("registrar")]
    [Authorize(Policy = Permisos.Matriculas.Crear)]
    public Task<IActionResult> RegistrarMatricula([FromBody] MatriculaCreateDto dto, CancellationToken ct)
        => Create(dto, ct);

    [HttpGet("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Matriculas.Ver)]
    public async Task<IActionResult> GetMatriculasAlumno(Guid alumnoId, CancellationToken ct)
    {
        await using var c6 = await AbrirComoUsuarioAsync(ct);
        await using var tx = await c6.BeginTransactionAsync(ct);
        await FijarClaimAsync(c6, tx, User.FindFirstValue("sub")!, ct);
        await using var cmd = c6.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = LecturaBase + " and m.alumno_id = @alumnoId";
        cmd.Parameters.AddWithValue("institucionId", DBNull.Value);
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        await using var r8 = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<MatriculaDto>();
        while (await r8.ReadAsync(ct)) lista.Add(Leer(r8)!);
        await tx.CommitAsync(ct);
        return Ok(lista);
    }
}