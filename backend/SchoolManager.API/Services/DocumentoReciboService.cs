using Npgsql;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Services;

/// <summary>
/// Implementación de la capa de aplicación para el recibo de pago.
///
/// Decisión arquitectónica (047A): el recibo se compone reutilizando las RPC
/// autoritativas ya existentes —<c>rpc_obtener_pago</c> (cabecera del pago) y
/// <c>rpc_obtener_aplicaciones_pago</c> (detalle aplicado a cargos)— más dos
/// lecturas de identidad (alumno/persona e institución emisora) que esas RPC no
/// devuelven. No se crea una migración nueva porque las RPC cubren el núcleo
/// financiero autoritativo y el resto son datos de presentación acotados por el
/// contexto institucional resuelto por la propia RPC. Todas las lecturas corren
/// dentro de la MISMA transacción, por lo que comparten un único snapshot
/// (consistencia ACID) y el pago se lee una sola vez de forma autoritativa.
/// </summary>
public sealed class DocumentoReciboService(NpgsqlDataSource dataSource) : IDocumentoReciboService
{
    private const string SqlFijarClaim =
        "select set_config('request.jwt.claim.sub', @sub, true)";

    private const string SqlPago =
        "select * from public.rpc_obtener_pago(@pagoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    private const string SqlAplicaciones =
        "select * from public.rpc_obtener_aplicaciones_pago(@pagoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

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

    public async Task<ReciboPagoDto?> ObtenerReciboPagoAsync(
        Guid pagoId, Guid? institucionId, string sub, CancellationToken ct)
    {
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conexion.BeginTransactionAsync(ct);
        try
        {
            await FijarClaimAsync(conexion, tx, sub, ct);

            PagoDto? pago = await LeerPagoAsync(conexion, tx, pagoId, institucionId, ct);
            if (pago is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            var detalles = await LeerDetallesAsync(conexion, tx, pagoId, pago.InstitucionId, ct);
            var alumno = await LeerAlumnoAsync(conexion, tx, pago.AlumnoId, pago.InstitucionId, ct);
            var institucion = await LeerInstitucionAsync(conexion, tx, pago.InstitucionId, ct);

            await tx.CommitAsync(ct);

            return new ReciboPagoDto
            {
                PagoId = pago.Id,
                NumeroRecibo = pago.NumeroRecibo,
                FechaPago = pago.FechaPago,
                MontoTotal = pago.MontoTotal,
                MetodoPago = pago.MetodoPago,
                ReferenciaExterna = pago.ReferenciaExterna,
                Estado = pago.Estado,
                FechaAnulacion = pago.FechaAnulacion,
                MotivoAnulacion = pago.MotivoAnulacion,
                Institucion = institucion,
                Alumno = alumno,
                Detalles = detalles
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

    private static async Task<PagoDto?> LeerPagoAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid pagoId, Guid? institucionId, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlPago;
        cmd.Parameters.AddWithValue("pagoId", pagoId);
        cmd.Parameters.AddWithValue("institucionId", (object?)institucionId ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            return null;
        }

        // Columnas de rpc_obtener_pago (15), mismo orden que PagosController.
        return new PagoDto
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
            MotivoAnulacion = r.IsDBNull(13) ? null : r.GetString(13)
        };
    }

    private static async Task<IReadOnlyList<ReciboPagoDetalleDto>> LeerDetallesAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid pagoId, Guid institucionId, CancellationToken ct)
    {
        var detalles = new List<ReciboPagoDetalleDto>();
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlAplicaciones;
        cmd.Parameters.AddWithValue("pagoId", pagoId);
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            // Columnas de rpc_obtener_aplicaciones_pago (10): CargoId=2, MontoAplicado=4,
            // Estado=5, CargoEstado=7, ConceptoNombre=8.
            detalles.Add(new ReciboPagoDetalleDto
            {
                CargoId = r.GetGuid(2),
                MontoAplicado = r.GetDecimal(4),
                Estado = r.IsDBNull(7) ? r.GetString(5) : r.GetString(7),
                Concepto = r.IsDBNull(8) ? string.Empty : r.GetString(8)
            });
        }

        return detalles;
    }

    private static async Task<AlumnoReciboDto> LeerAlumnoAsync(
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
            return new AlumnoReciboDto { Id = alumnoId };
        }

        return new AlumnoReciboDto
        {
            Id = r.GetGuid(0),
            Rne = r.IsDBNull(1) ? null : r.GetString(1),
            CodigoInterno = r.IsDBNull(2) ? null : r.GetString(2),
            NombreCompleto = r.IsDBNull(3) ? string.Empty : r.GetString(3)
        };
    }

    private static async Task<InstitucionReciboDto> LeerInstitucionAsync(
        NpgsqlConnection conexion, NpgsqlTransaction tx, Guid institucionId, CancellationToken ct)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = SqlInstitucionEmisora; // NOSONAR:csharpsquid:S2077
        cmd.Parameters.AddWithValue("institucionId", institucionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            return new InstitucionReciboDto { Id = institucionId };
        }

        return new InstitucionReciboDto
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