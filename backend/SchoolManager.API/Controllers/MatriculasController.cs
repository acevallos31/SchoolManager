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
    """;

    // Toda lectura filtra cada fila contra el ambito institucional real del
    // usuario que actua, en lugar de confiar solo en el codigo global del
    // AuthorizationHandler. Reutiliza la funcion SECURITY DEFINER existente.
    private const string FiltroContextoInstitucional =
        " and public.usuario_tiene_permiso_actual('academico.matriculas.ver', m.institucion_id)";

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
            // P0001 (raise_exception generico) no se trata como 403: cae en el default.
            // "SM001"/"SM003" son codigos de contexto de la implementacion.
            StatusCode = ex.SqlState switch
            {
                "42501" => StatusCodes.Status403Forbidden,
                "P0002" => StatusCodes.Status404NotFound,
                "23505" or "23514" => StatusCodes.Status409Conflict,
                "22023" or "23503" or "SM001" or "SM003" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            }
        };

    [HttpGet]
    [Authorize(Policy = Permisos.Matriculas.Ver)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? alumnoId,
        [FromQuery] Guid? institucionId,
        [FromQuery] Guid? cicloId,
        [FromQuery] string? estado,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        try
        {
            await using var c1 = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c1.BeginTransactionAsync(ct);
            await FijarClaimAsync(c1, tx, User.FindFirstValue("sub")!, ct);

            var where = " where m.institucion_id = public.resolver_institucion_operacion(@institucionId)"
                + FiltroContextoInstitucional;
            if (alumnoId.HasValue) where += " and m.alumno_id = @alumnoId";
            if (cicloId.HasValue) where += " and m.ciclo_id = @cicloId";
            if (!string.IsNullOrWhiteSpace(estado)) where += " and m.estado = @estado";

            bool paginar = page.HasValue && pageSize.HasValue;
            int limit = paginar ? Math.Clamp(pageSize!.Value, 1, 100) : 0;
            int offset = paginar ? Math.Clamp(page!.Value, 1, int.MaxValue / 100) - 1 : 0;

            string sql = LecturaBase + where;
            if (paginar)
            {
                sql += " order by m.created_at desc limit @limit offset @offset";
            }

            await using var cmd = c1.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            if (alumnoId.HasValue) cmd.Parameters.AddWithValue("alumnoId", alumnoId.Value);
            if (cicloId.HasValue) cmd.Parameters.AddWithValue("cicloId", cicloId.Value);
            if (!string.IsNullOrWhiteSpace(estado)) cmd.Parameters.AddWithValue("estado", estado.Trim());
            if (paginar)
            {
                cmd.Parameters.AddWithValue("limit", limit);
                cmd.Parameters.AddWithValue("offset", offset * limit);
            }

            await using var r2 = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<MatriculaDto>();
            while (await r2.ReadAsync(ct)) lista.Add(Leer(r2)!);
            await r2.DisposeAsync();

            if (!paginar)
            {
                await tx.CommitAsync(ct);
                return Ok(lista);
            }

            // Recuento total con los mismos filtros para calcular totalPages.
            await using var countCmd = c1.CreateCommand();
            countCmd.Transaction = tx;
            countCmd.CommandText = "select count(*) from public.matriculas m" + where;
            countCmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            if (alumnoId.HasValue) countCmd.Parameters.AddWithValue("alumnoId", alumnoId.Value);
            if (cicloId.HasValue) countCmd.Parameters.AddWithValue("cicloId", cicloId.Value);
            if (!string.IsNullOrWhiteSpace(estado)) countCmd.Parameters.AddWithValue("estado", estado.Trim());
            var total = (long)(await countCmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);

            return Ok(new PaginatedResult<MatriculaDto>
            {
                Items = lista,
                Page = page!.Value,
                PageSize = limit,
                TotalItems = total,
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / limit)
            });
        }
        catch (PostgresException ex) { return ToError(ex); }
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
        cmd.CommandText = LecturaBase + " where m.id = @id" + FiltroContextoInstitucional;
        cmd.Parameters.AddWithValue("id", id);
        await using var r3 = await cmd.ExecuteReaderAsync(ct);
        MatriculaDto? dto = await r3.ReadAsync(ct) ? Leer(r3) : null;
        await r3.DisposeAsync();
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
        cmd.CommandText = LecturaBase + " where m.alumno_id = @alumnoId" + FiltroContextoInstitucional;
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        await using var r8 = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<MatriculaDto>();
        while (await r8.ReadAsync(ct)) lista.Add(Leer(r8)!);
        await r8.DisposeAsync();
        await tx.CommitAsync(ct);
        return Ok(lista);
    }
}