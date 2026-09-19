using Npgsql;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Services;

/// <summary>
/// Implementación de la capa de aplicación para el estado de cuenta del alumno.
///
/// Decisión arquitectónica (047B): el estado de cuenta se compone reutilizando
/// las RPC autoritativas ya existentes de 021 —<c>rpc_resumen_financiero_alumno</c>
/// (totales), <c>rpc_listar_cargos_alumno</c> (detalle de obligaciones) y
/// <c>rpc_listar_pagos_alumno</c> (histórico de pagos)— más dos lecturas de
/// identidad (alumno/persona e institución emisora) que esas RPC no devuelven.
/// No se crea una migración nueva porque las RPC cubren el núcleo financiero
/// autoritativo y el resto son datos de presentación acotados por el contexto
/// institucional resuelto por la propia RPC. Todas las lecturas corren dentro
/// de la MISMA transacción, por lo que comparten un único snapshot
/// (consistencia ACID) y los totales/cargos/pagos se leen una sola vez de forma
/// autoritativa. El frontend solo presenta/imprime/descarga el DTO.
/// </summary>
public sealed class EstadoCuentaService(NpgsqlDataSource dataSource) : IEstadoCuentaService
{
    private const string SqlFijarClaim =
        "select set_config('request.jwt.claim.sub', @sub, true)";

    private const string SqlResumen =
        "select * from public.rpc_resumen_financiero_alumno(@alumnoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    private const string SqlCargos =
        "select * from public.rpc_listar_cargos_alumno(@alumnoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    private const string SqlPagos =
        "select * from public.rpc_listar_pagos_alumno(@alumnoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    private const string SqlAlumnoIdentidad = """
        select a.id, a.rne, a.codigo_interno,
               trim(coalesce(p.nombres, '') || ' ' || coalesce(p.apellidos, '')) as nombre_completo
        from public.alumnos a
        join public.personas p on p.id = a.persona_id
        where a.id = @alumnoId and a.institucion_id = @institucionId
        """;

    private const string SqlInstitucionEmisora = """
        select id, nombre, nombre_corto, direccion, telefono, correo, logo_url
        from public.instituciones
        where id = @institucionId
        """;

    public async Task<EstadoCuentaDto?> ObtenerEstadoCuentaAsync(
        Guid alumnoId, Guid? institucionId, string sub, CancellationToken ct)
    {
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conexion.BeginTransactionAsync(ct);
        try
        {
            await FijarClaimAsync(conexion, tx, sub, ct);

            ResumenFinancieroDto? resumen = await LeerResumenAsync(conexion, tx, alumnoId, institucionId, ct);
            if (resumen is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            var cargos = await LeerCargosAsync(conexion, tx, alumnoId, resumen.InstitucionId, ct);
            var pagos = await LeerPagosAsync(conexion, tx, alumnoId, resumen.InstitucionId, ct);
            var alumno = await LeerAlumnoAsync(conexion, tx, alumnoId, resumen.InstitucionId, ct);
            var institucion = await LeerInstitucionAsync(conexion, tx, resumen.InstitucionId, ct);

            await tx.CommitAsync(ct);

            return new EstadoCuentaDto
            {
                Institucion = institucion,
                Alumno = alumno,
                Resumen = resumen,
                Cargos = cargos,
                Pagos = pagos
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task FijarClaimAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, string sub, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlFijarClaim;
        cmd.Parameters.AddWithValue("sub", sub);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<ResumenFinancieroDto?> LeerResumenAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid alumnoId, Guid? institucionId, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlResumen;
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            return null;
        }

        // Columnas de rpc_resumen_financiero_alumno (8), mismo orden que CargosController.
        return new ResumenFinancieroDto
        {
            AlumnoId = r.GetGuid(0),
            InstitucionId = r.GetGuid(1),
            TotalObligaciones = r.GetInt64(2),
            TotalMontoOriginal = r.GetDecimal(3),
            TotalPendiente = r.GetDecimal(4),
            TotalVencido = r.GetDecimal(5),
            TotalAnulado = r.GetDecimal(6),
            TotalAplicado = r.GetDecimal(7)
        };
    }

    private static async Task<IReadOnlyList<CargoDto>> LeerCargosAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid alumnoId, Guid institucionId, CancellationToken ct)
    {
        var cargos = new List<CargoDto>();
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlCargos;
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            // Columnas de rpc_listar_cargos_alumno (17), mismo orden que CargosController.
            cargos.Add(new CargoDto
            {
                Id = r.GetGuid(0),
                MatriculaId = r.GetGuid(1),
                AlumnoId = r.GetGuid(2),
                PlanPagoId = r.IsDBNull(3) ? null : r.GetGuid(3),
                Orden = r.GetInt32(4),
                ConceptoId = r.IsDBNull(5) ? null : r.GetGuid(5),
                ConceptoNombre = r.IsDBNull(6) ? null : r.GetString(6),
                Descripcion = r.IsDBNull(7) ? null : r.GetString(7),
                MontoOriginal = r.GetDecimal(8),
                FechaVencimiento = r.GetFieldValue<DateOnly>(9),
                Estado = r.GetString(10),
                FechaGeneracion = r.GetFieldValue<DateTimeOffset>(11),
                FechaAnulacion = r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12),
                MotivoAnulacion = r.IsDBNull(13) ? null : r.GetString(13),
                EsVencido = r.GetBoolean(14),
                Saldo = r.GetDecimal(15),
                Aplicado = r.GetDecimal(16)
            });
        }

        return cargos;
    }

    private static async Task<IReadOnlyList<PagoDto>> LeerPagosAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid alumnoId, Guid institucionId, CancellationToken ct)
    {
        var pagos = new List<PagoDto>();
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlPagos;
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            // Columnas de rpc_listar_pagos_alumno (15), mismo orden que PagosController.
            pagos.Add(new PagoDto
            {
                Id = r.GetGuid(0),
                InstitucionId = r.GetGuid(1),
                AlumnoId = r.GetGuid(2),
                ResponsableId = r.IsDBNull(3) ? null : r.GetGuid(3),
                NumeroRecibo = r.GetInt64(4),
                MontoTotal = r.GetDecimal(5),
                FechaPago = r.GetFieldValue<DateTimeOffset>(6),
                MetodoPago = r.IsDBNull(7) ? null : r.GetString(7),
                ReferenciaExterna = r.IsDBNull(8) ? null : r.GetString(8),
                Estado = r.GetString(9),
                RegistradoPor = r.IsDBNull(10) ? null : r.GetGuid(10),
                FechaAnulacion = r.IsDBNull(11) ? null : r.GetFieldValue<DateTimeOffset>(11),
                AnuladoPor = r.IsDBNull(12) ? null : r.GetGuid(12),
                MotivoAnulacion = r.IsDBNull(13) ? null : r.GetString(13),
                CreatedAt = r.GetFieldValue<DateTimeOffset>(14)
            });
        }

        return pagos;
    }

    private static async Task<AlumnoEstadoCuentaDto> LeerAlumnoAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid alumnoId, Guid institucionId, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlAlumnoIdentidad; // NOSONAR:csharpsquid:S2077
        cmd.Parameters.AddWithValue("alumnoId", alumnoId);
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            return new AlumnoEstadoCuentaDto { Id = alumnoId };
        }

        return new AlumnoEstadoCuentaDto
        {
            Id = r.GetGuid(0),
            Rne = r.IsDBNull(1) ? null : r.GetString(1),
            CodigoInterno = r.IsDBNull(2) ? null : r.GetString(2),
            NombreCompleto = r.IsDBNull(3) ? string.Empty : r.GetString(3)
        };
    }

    private static async Task<InstitucionEstadoCuentaDto> LeerInstitucionAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid institucionId, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlInstitucionEmisora; // NOSONAR:csharpsquid:S2077
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            return new InstitucionEstadoCuentaDto { Id = institucionId };
        }

        return new InstitucionEstadoCuentaDto
        {
            Id = r.GetGuid(0),
            Nombre = r.GetString(1),
            NombreCorto = r.IsDBNull(2) ? null : r.GetString(2),
            Direccion = r.IsDBNull(3) ? null : r.GetString(3),
            Telefono = r.IsDBNull(4) ? null : r.GetString(4),
            Correo = r.IsDBNull(5) ? null : r.GetString(5),
            LogoUrl = r.IsDBNull(6) ? null : r.GetString(6)
        };
    }
}
