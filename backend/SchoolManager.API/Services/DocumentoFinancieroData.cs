using Npgsql;

namespace SchoolManager.API.Services;

/// <summary>
/// Lecturas de identidad compartidas por documentos financieros.
/// El contexto se deriva del usuario autenticado y del alumno solicitado:
/// el cliente no necesita enviar institucionId para poder imprimir.
/// </summary>
internal static class DocumentoFinancieroData
{
    private const string SqlAlumno = """
        select a.id, a.rne, a.codigo_interno,
               trim(coalesce(p.nombres, '') || ' ' || coalesce(p.apellidos, '')) as nombre_completo
        from public.alumnos a
        join public.personas p on p.id = a.persona_id
        where a.id = @alumnoId and a.institucion_id = @institucionId
        """;

    private const string SqlInstitucion = """
        select id, nombre, nombre_corto, direccion, telefono, correo, logo_url
        from public.instituciones
        where id = @institucionId
        """;

    internal static async Task FijarClaimAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, string sub, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "select set_config('request.jwt.claim.sub', @sub, true)";
        cmd.Parameters.AddWithValue("sub", sub);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task<Guid?> ResolverInstitucionAlumnoAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction tx,
        Guid alumnoId,
        Guid? institucionSolicitada,
        CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            select a.institucion_id
            from public.alumnos a
            where a.id = @alumnoId
              and (@institucionId::uuid is null or a.institucion_id = @institucionId)
              and public.usuario_tiene_permiso_actual('academico.cargos.ver', a.institucion_id)
            limit 1
            """;
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        cmd.Parameters.AddWithValue("institucionId", (object?)institucionSolicitada ?? DBNull.Value);
        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is Guid institucionId ? institucionId : null;
    }

    internal static async Task<AlumnoDocumentoData?> LeerAlumnoAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid alumnoId, Guid institucionId, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlAlumno; // NOSONAR:csharpsquid:S2077
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;

        return new AlumnoDocumentoData(
            r.GetGuid(0),
            r.IsDBNull(1) ? null : r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.IsDBNull(3) ? string.Empty : r.GetString(3));
    }

    internal static async Task<InstitucionDocumentoData?> LeerInstitucionAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid institucionId, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlInstitucion; // NOSONAR:csharpsquid:S2077
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;

        return new InstitucionDocumentoData(
            r.GetGuid(0),
            r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3),
            r.IsDBNull(4) ? null : r.GetString(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetString(6));
    }
}

internal sealed record AlumnoDocumentoData(
    Guid Id, string? Rne, string? CodigoInterno, string NombreCompleto);

internal sealed record InstitucionDocumentoData(
    Guid Id,
    string Nombre,
    string? NombreCorto,
    string? Direccion,
    string? Telefono,
    string? Correo,
    string? LogoUrl);
