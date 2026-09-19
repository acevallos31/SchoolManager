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
    private const string SqlPago =
        "select * from public.rpc_obtener_pago(@pagoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    private const string SqlAplicaciones =
        "select * from public.rpc_obtener_aplicaciones_pago(@pagoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    public async Task<ReciboPagoDto?> ObtenerReciboPagoAsync(
        Guid pagoId, Guid? institucionId, string sub, CancellationToken ct)
    {
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conexion.BeginTransactionAsync(ct);
        try
        {
            await DocumentoFinancieroData.FijarClaimAsync(conexion, tx, sub, ct);

            PagoDto? pago = await LeerPagoAsync(conexion, tx, pagoId, institucionId, ct);
            if (pago is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            var detalles = await LeerDetallesAsync(conexion, tx, pagoId, pago.InstitucionId, ct);
            var alumnoData = await DocumentoFinancieroData.LeerAlumnoAsync(
                conexion, tx, pago.AlumnoId, pago.InstitucionId, ct);
            var institucionData = await DocumentoFinancieroData.LeerInstitucionAsync(
                conexion, tx, pago.InstitucionId, ct);
            var alumno = alumnoData is null
                ? new AlumnoReciboDto { Id = pago.AlumnoId }
                : new AlumnoReciboDto
                {
                    Id = alumnoData.Id,
                    Rne = alumnoData.Rne,
                    CodigoInterno = alumnoData.CodigoInterno,
                    NombreCompleto = alumnoData.NombreCompleto
                };
            var institucion = institucionData is null
                ? new InstitucionReciboDto { Id = pago.InstitucionId }
                : new InstitucionReciboDto
                {
                    Id = institucionData.Id,
                    Nombre = institucionData.Nombre,
                    NombreCorto = institucionData.NombreCorto,
                    Direccion = institucionData.Direccion,
                    Telefono = institucionData.Telefono,
                    Correo = institucionData.Correo,
                    LogoUrl = institucionData.LogoUrl
                };

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

        return FinanzasDataReader.LeerPago(r);
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

}
