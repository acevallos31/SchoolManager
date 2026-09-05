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
public class ResponsablesController(NpgsqlDataSource dataSource) : ControllerBase
{
    // Lectura de un responsable con su Persona (identidad global) resuelta.
    private const string LecturaBase = """
        select
          r.id,
          r.persona_id,
          r.institucion_id,
          r.estado,
          p.nombres,
          p.apellidos,
          p.tipo_identificacion,
          p.numero_identificacion,
          p.telefono,
          p.correo,
          r.created_at,
          r.fecha_desactivacion,
          r.motivo_desactivacion
        from public.responsables r
        join public.personas p on p.id = r.persona_id
    """;

    // Filtra cada fila contra el ámbito institucional real del usuario (igual
    // que MatriculasController). Reutiliza la SECURITY DEFINER existente.
    private const string FiltroContextoInstitucional =
        " and public.usuario_tiene_permiso_actual('academico.responsables.ver', r.institucion_id)";

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

    private static ResponsableDto Leer(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        PersonaId = r.GetGuid(1),
        InstitucionId = r.GetGuid(2),
        Estado = r.GetString(3),
        Nombres = r.GetString(4),
        Apellidos = r.GetString(5),
        TipoIdentificacion = r.IsDBNull(6) ? null : r.GetString(6),
        NumeroIdentificacion = r.IsDBNull(7) ? null : r.GetString(7),
        Telefono = r.IsDBNull(8) ? null : r.GetString(8),
        Correo = r.IsDBNull(9) ? null : r.GetString(9),
        CreatedAt = r.GetFieldValue<DateTimeOffset>(10),
        FechaDesactivacion = r.IsDBNull(11) ? null : r.GetFieldValue<DateTimeOffset>(11),
        MotivoDesactivacion = r.IsDBNull(12) ? null : r.GetString(12),
    };

    private ObjectResult ToError(PostgresException ex) =>
        new(new { error = ex.MessageText ?? "Error en base de datos" })
        {
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
    [Authorize(Policy = Permisos.Responsables.Ver)]
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

            var where = " where r.institucion_id = public.resolver_institucion_operacion(@institucionId)"
                + FiltroContextoInstitucional;
            if (!string.IsNullOrWhiteSpace(termino))
            {
                where += " and (p.nombres ilike @termino or p.apellidos ilike @termino"
                       + " or p.numero_identificacion ilike @termino)";
            }
            if (!string.IsNullOrWhiteSpace(estado))
            {
                where += " and r.estado = @estado";
            }

            bool paginar = page.HasValue && pageSize.HasValue;
            int limit = paginar ? Math.Clamp(pageSize!.Value, 1, 100) : 0;
            int offset = paginar ? Math.Clamp(page!.Value, 1, int.MaxValue / 100) - 1 : 0;

            string sql = LecturaBase + where;
            if (paginar) sql += " order by p.apellidos, p.nombres limit @limit offset @offset";

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
            var lista = new List<ResponsableDto>();
            while (await r2.ReadAsync(ct)) lista.Add(Leer(r2));
            await r2.DisposeAsync();

            if (!paginar)
            {
                await tx.CommitAsync(ct);
                return Ok(lista);
            }

            // Recuento con los mismos filtros para totalPages. Solo fragmentos
            // WHERE fijos se concatenan; los valores van por NpgsqlParameter.
            await using var countCmd = c1.CreateCommand();
            countCmd.Transaction = tx;
            // Only fixed WHERE fragments are concatenated; all values go via
            // NpgsqlParameter below — no user input is interpolated into SQL.
            countCmd.CommandText = "select count(*) from public.responsables r" // NOSONAR:csharpsquid:S2077 (solo fragmentos WHERE fijos; valores por NpgsqlParameter)
                + " join public.personas p on p.id = r.persona_id" + where;
            countCmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
            if (!string.IsNullOrWhiteSpace(termino)) countCmd.Parameters.AddWithValue("termino", $"%{termino.Trim()}%");
            if (!string.IsNullOrWhiteSpace(estado)) countCmd.Parameters.AddWithValue("estado", estado.Trim());
            var total = (long)(await countCmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);

            return Ok(new PaginatedResult<ResponsableDto>
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
    [Authorize(Policy = Permisos.Responsables.Ver)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        await using var c2 = await AbrirComoUsuarioAsync(ct);
        await using var tx = await c2.BeginTransactionAsync(ct);
        await FijarClaimAsync(c2, tx, User.FindFirstValue("sub")!, ct);
        await using var cmd = c2.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = LecturaBase + " where r.id = @id" + FiltroContextoInstitucional; // NOSONAR:csharpsquid:S2077 (query parameterizada, concatenación de constantes readonly)
        cmd.Parameters.AddWithValue("id", id);
        await using var r3 = await cmd.ExecuteReaderAsync(ct);
        ResponsableDto? dto = await r3.ReadAsync(ct) ? Leer(r3) : null;
        await r3.DisposeAsync();
        await tx.CommitAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // Responsables de un alumno (vínculos activos/inactivos con parentesco).
    [HttpGet("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Responsables.Ver)]
    public async Task<IActionResult> GetResponsablesDeAlumno(Guid alumnoId, CancellationToken ct)
    {
        await using var c4 = await AbrirComoUsuarioAsync(ct);
        await using var tx = await c4.BeginTransactionAsync(ct);
        await FijarClaimAsync(c4, tx, User.FindFirstValue("sub")!, ct);
        await using var cmd = c4.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            select
              ar.id,
              r.id,
              ar.parentesco,
              ar.es_principal,
              ar.acceso_financiero,
              ar.estado,
              p.nombres,
              p.apellidos,
              p.telefono,
              p.correo
            from public.alumno_responsable ar
            join public.responsables r on r.id = ar.responsable_id
            join public.personas p on p.id = r.persona_id
            where ar.alumno_id = @alumnoId
              and public.usuario_tiene_permiso_actual('academico.responsables.ver', r.institucion_id)
            order by ar.es_principal desc, p.apellidos, p.nombres
        """;
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        await using var r4 = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<ResponsableVinculoDto>();
        while (await r4.ReadAsync(ct))
        {
            lista.Add(new ResponsableVinculoDto
            {
                Id = r4.GetGuid(0),
                ResponsableId = r4.GetGuid(1),
                Parentesco = r4.IsDBNull(2) ? null : r4.GetString(2),
                EsPrincipal = r4.GetBoolean(3),
                AccesoFinanciero = r4.GetBoolean(4),
                Estado = r4.GetString(5),
                Nombres = r4.GetString(6),
                Apellidos = r4.GetString(7),
                Telefono = r4.IsDBNull(8) ? null : r4.GetString(8),
                Correo = r4.IsDBNull(9) ? null : r4.GetString(9),
            });
        }
        await r4.DisposeAsync();
        await tx.CommitAsync(ct);
        return Ok(lista);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.Responsables.Crear)]
    public async Task<IActionResult> Create([FromBody] CrearResponsableDto dto, CancellationToken ct)
    {
        if (dto.InstitucionId == Guid.Empty
            || string.IsNullOrWhiteSpace(dto.Nombres)
            || string.IsNullOrWhiteSpace(dto.Apellidos)
            || string.IsNullOrWhiteSpace(dto.TipoIdentificacion)
            || string.IsNullOrWhiteSpace(dto.NumeroIdentificacion))
            return BadRequest(new { error = "Faltan datos obligatorios del responsable." });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_crear_responsable_con_documento("
                + "@institucionId, @nombres, @apellidos, @tipo, @numero, @telefono, @correo)";
            cmd.Parameters.AddWithValue("institucionId", dto.InstitucionId);
            cmd.Parameters.AddWithValue("nombres", dto.Nombres.Trim());
            cmd.Parameters.AddWithValue("apellidos", dto.Apellidos.Trim());
            cmd.Parameters.AddWithValue("tipo", dto.TipoIdentificacion.Trim());
            cmd.Parameters.AddWithValue("numero", dto.NumeroIdentificacion.Trim());
            cmd.Parameters.AddWithValue("telefono", (object?)(string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("correo", (object?)(string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim()) ?? DBNull.Value);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    // Crear responsable para una persona ya existente (ideal para re-vincular).
    [HttpPost("para-persona")]
    [Authorize(Policy = Permisos.Responsables.Crear)]
    public async Task<IActionResult> CreateParaPersona([FromBody] CrearResponsableParaPersonaDto dto, CancellationToken ct)
    {
        if (dto.PersonaId == Guid.Empty || dto.InstitucionId == Guid.Empty)
            return BadRequest(new { error = "Faltan datos obligatorios del responsable." });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_crear_responsable_para_persona(@personaId, @institucionId)";
            cmd.Parameters.AddWithValue("personaId", dto.PersonaId);
            cmd.Parameters.AddWithValue("institucionId", dto.InstitucionId);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.Responsables.Editar)]
    public async Task<IActionResult> Update(Guid id, [FromBody] EditarResponsableDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombres) || string.IsNullOrWhiteSpace(dto.Apellidos))
            return BadRequest(new { error = "Nombres y apellidos son obligatorios." });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_editar_responsable(@id, @nombres, @apellidos, @telefono, @correo)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("nombres", dto.Nombres.Trim());
            cmd.Parameters.AddWithValue("apellidos", dto.Apellidos.Trim());
            cmd.Parameters.AddWithValue("telefono", (object?)(string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("correo", (object?)(string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim()) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPut("{id:guid}/estado")]
    [Authorize(Policy = Permisos.Responsables.Editar)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] DesactivarDto dto, CancellationToken ct)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) return BadRequest(new { error = "El motivo es obligatorio." });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_inactivar_responsable(@id, @motivo)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("motivo", motivo);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Policy = Permisos.Responsables.Editar)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_responsable(@id)";
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    // Vincular un responsable a un alumno.
    [HttpPost("alumno/{alumnoId:guid}")]
    [Authorize(Policy = Permisos.Responsables.Editar)]
    public async Task<IActionResult> Vincular(Guid alumnoId, [FromBody] VincularResponsableDto dto, CancellationToken ct)
    {
        if (dto.ResponsableId == Guid.Empty)
            return BadRequest(new { error = "El responsable es obligatorio." });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_vincular_alumno_responsable("
                + "@alumnoId, @responsableId, @parentesco, @esPrincipal, @accesoFinanciero)";
            cmd.Parameters.AddWithValue("alumnoId", alumnoId);
            cmd.Parameters.AddWithValue("responsableId", dto.ResponsableId);
            cmd.Parameters.AddWithValue("parentesco", (object?)(string.IsNullOrWhiteSpace(dto.Parentesco) ? null : dto.Parentesco.Trim()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("esPrincipal", dto.EsPrincipal);
            cmd.Parameters.AddWithValue("accesoFinanciero", dto.AccesoFinanciero);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return CreatedAtAction(nameof(GetResponsablesDeAlumno), new { alumnoId }, new { id });
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPut("vinculo/{vinculoId:guid}")]
    [Authorize(Policy = Permisos.Responsables.Editar)]
    public async Task<IActionResult> EditarVinculo(Guid vinculoId, [FromBody] EditarVinculoDto dto, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_editar_vinculo_responsable("
                + "@vinculoId, @parentesco, @esPrincipal, @accesoFinanciero)";
            cmd.Parameters.AddWithValue("vinculoId", vinculoId);
            cmd.Parameters.AddWithValue("parentesco", (object?)dto.Parentesco ?? DBNull.Value);
            cmd.Parameters.AddWithValue("esPrincipal", (object?)dto.EsPrincipal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("accesoFinanciero", (object?)dto.AccesoFinanciero ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPut("vinculo/{vinculoId:guid}/desactivar")]
    [Authorize(Policy = Permisos.Responsables.Editar)]
    public async Task<IActionResult> DesactivarVinculo(Guid vinculoId, [FromBody] DesactivarDto dto, CancellationToken ct)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) return BadRequest(new { error = "El motivo es obligatorio." });
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_desactivar_vinculo_responsable(@vinculoId, @motivo)";
            cmd.Parameters.AddWithValue("vinculoId", vinculoId);
            cmd.Parameters.AddWithValue("motivo", motivo);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }

    [HttpPost("vinculo/{vinculoId:guid}/reactivar")]
    [Authorize(Policy = Permisos.Responsables.Editar)]
    public async Task<IActionResult> ReactivarVinculo(Guid vinculoId, CancellationToken ct)
    {
        try
        {
            await using var c = await AbrirComoUsuarioAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await FijarClaimAsync(c, tx, User.FindFirstValue("sub")!, ct);
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select public.rpc_reactivar_vinculo_responsable(@vinculoId)";
            cmd.Parameters.AddWithValue("vinculoId", vinculoId);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        catch (PostgresException ex) { return ToError(ex); }
    }
}