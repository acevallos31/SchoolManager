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
public class AlumnosController(NpgsqlDataSource dataSource) : ApiControllerBase(dataSource)
{
    private const string LecturaBase = """
        select
          a.id,
          a.persona_id,
          a.institucion_id,
          p.nombres,
          p.apellidos,
          p.numero_identificacion,
          a.rne,
          a.codigo_interno,
          a.estado,
          ma.id as matricula_actual_id,
          ma.seccion_nombre,
          ma.grado_nombre,
          ma.ciclo_nombre
        from public.alumnos a
        join public.personas p on p.id = a.persona_id
        left join lateral (
          select m.id, s.nombre as seccion_nombre,
                 g.nombre as grado_nombre,
                 c.nombre as ciclo_nombre
          from public.matriculas m
          join public.secciones s on s.id = m.seccion_id
          left join public.grados g on g.id = s.grado_id
          left join public.ciclos_escolares c on c.id = s.ciclo_id
          where m.alumno_id = a.id and m.estado = 'activa'
          order by m.created_at desc
          limit 1
        ) ma on true
    """;

    private const string FiltroContextoInstitucional =
        " and public.usuario_tiene_permiso_actual('academico.alumnos.ver', a.institucion_id)";

    private static AlumnoDto Leer(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        PersonaId = r.GetGuid(1),
        InstitucionId = r.GetGuid(2),
        NombreCompleto = $"{((r.IsDBNull(3) ? "" : r.GetString(3)) + " " + (r.IsDBNull(4) ? "" : r.GetString(4))).Trim()}",
        Identidad = r.IsDBNull(5) ? null : r.GetString(5),
        Rne = r.IsDBNull(6) ? null : r.GetString(6),
        CodigoInterno = r.IsDBNull(7) ? null : r.GetString(7),
        Estado = r.GetString(8),
        MatriculaActual = r.IsDBNull(9) ? null : new MatriculaActualAlumnoDto
        {
            Id = r.GetGuid(9).ToString(),
            Seccion = r.IsDBNull(10) ? string.Empty : r.GetString(10),
            Grado = r.IsDBNull(11) ? string.Empty : r.GetString(11),
            Ciclo = r.IsDBNull(12) ? string.Empty : r.GetString(12),
        },
    };

    [HttpGet]
    [Authorize(Policy = Permisos.Alumnos.Ver)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? institucionId,
        [FromQuery] string? termino,
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

            bool paginar = page.HasValue && pageSize.HasValue;

            var where = " where a.institucion_id = public.resolver_institucion_operacion(@institucionId)"
                + FiltroContextoInstitucional;
            if (!string.IsNullOrWhiteSpace(termino))
            {
                where += " and (p.nombres ilike @termino or p.apellidos ilike @termino"
                       + " or a.rne ilike @termino or a.codigo_interno ilike @termino)";
            }
            if (!string.IsNullOrWhiteSpace(estado)) where += " and a.estado = @estado";

            int limit = paginar ? Math.Clamp(pageSize!.Value, 1, 100) : 0;
            int offset = paginar ? Math.Clamp(page!.Value, 1, int.MaxValue / 100) - 1 : 0;

            string sql = LecturaBase + where;
            if (paginar) sql += " order by p.apellidos, p.nombres limit @limit offset @offset";
            else sql += " order by p.apellidos, p.nombres";

            await using var cmd = c1.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            if (!string.IsNullOrWhiteSpace(termino)) cmd.Parameters.AddWithValue("termino", $"%{termino.Trim()}%");
            if (!string.IsNullOrWhiteSpace(estado)) cmd.Parameters.AddWithValue("estado", estado.Trim());
            if (paginar)
            {
                cmd.Parameters.AddWithValue("limit", limit);
                cmd.Parameters.AddWithValue("offset", offset * limit);
            }

            await using var r2 = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<AlumnoDto>();
            while (await r2.ReadAsync(ct)) lista.Add(Leer(r2));
            await r2.DisposeAsync();

            if (!paginar)
            {
                await tx.CommitAsync(ct);
                return Ok(lista);
            }

            await using var countCmd = c1.CreateCommand();
            countCmd.Transaction = tx;
            countCmd.CommandText = "select count(*) from public.alumnos a" // NOSONAR:csharpsquid:S2077
                + " join public.personas p on p.id = a.persona_id" + where;
            countCmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            if (!string.IsNullOrWhiteSpace(termino)) countCmd.Parameters.AddWithValue("termino", $"%{termino.Trim()}%");
            if (!string.IsNullOrWhiteSpace(estado)) countCmd.Parameters.AddWithValue("estado", estado.Trim());
            var total = (long)(await countCmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);

            return Ok(new PaginatedResult<AlumnoDto>
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
    [Authorize(Policy = Permisos.Alumnos.Ver)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            await using var c2 = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c2.BeginTransactionAsync(ct);
            await FijarClaimAsync(c2, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c2.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = LecturaBase + " where a.id = @id" + FiltroContextoInstitucional; // NOSONAR:csharpsquid:S2077
            cmd.Parameters.AddWithValue("id", id);
            await using var r3 = await cmd.ExecuteReaderAsync(ct);
            AlumnoDto? dto = await r3.ReadAsync(ct) ? Leer(r3) : null;
            await r3.DisposeAsync();
            await tx.CommitAsync(ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost]
    [Authorize(Policy = Permisos.Alumnos.Crear)]
    public async Task<IActionResult> Create([FromBody] CrearAlumnoDto dto, CancellationToken ct)
    {
        if (dto.InstitucionId.GetValueOrDefault() == Guid.Empty
            || string.IsNullOrWhiteSpace(dto.Nombres)
            || string.IsNullOrWhiteSpace(dto.Apellidos)
            || string.IsNullOrWhiteSpace(dto.TipoIdentificacion)
            || string.IsNullOrWhiteSpace(dto.NumeroIdentificacion))
            return BadRequest(new { error = "Faltan datos obligatorios del alumno." });
        try
        {
            await using var c4 = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c4.BeginTransactionAsync(ct);
            await FijarClaimAsync(c4, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c4.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_crear_alumno_nueva_persona_con_documento("
                + "@institucionId, @nombres, @apellidos, @tipo, @numero, @fechaNac, @rne, @codigoInterno)";
            cmd.Parameters.AddWithValue("institucionId", dto.InstitucionId.GetValueOrDefault());
            cmd.Parameters.AddWithValue("nombres", dto.Nombres.Trim());
            cmd.Parameters.AddWithValue("apellidos", dto.Apellidos.Trim());
            cmd.Parameters.AddWithValue("tipo", dto.TipoIdentificacion.Trim());
            cmd.Parameters.AddWithValue("numero", dto.NumeroIdentificacion.Trim());
            cmd.Parameters.AddWithValue("fechaNac", (object?)(dto.FechaNacimiento) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rne", (object?)(string.IsNullOrWhiteSpace(dto.Rne) ? null : dto.Rne.Trim()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("codigoInterno", (object?)(string.IsNullOrWhiteSpace(dto.CodigoInterno) ? null : dto.CodigoInterno.Trim()) ?? DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.Alumnos.Desactivar)]
    public async Task<IActionResult> Desactivar(Guid id, [FromBody] DesactivarDto dto, CancellationToken ct)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) return BadRequest(new { error = "El motivo es obligatorio." });
        try
        {
            await using var c5 = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c5.BeginTransactionAsync(ct);
            await FijarClaimAsync(c5, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c5.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_alumno(@id, @motivo)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("motivo", motivo);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Policy = Permisos.Alumnos.Editar)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        try
        {
            await using var c6 = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c6.BeginTransactionAsync(ct);
            await FijarClaimAsync(c6, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c6.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_alumno(@id)";
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}
