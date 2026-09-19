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
    private const string SqlResumen =
        "select * from public.rpc_resumen_financiero_alumno(@alumnoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    private const string SqlCargos =
        "select * from public.rpc_listar_cargos_alumno(@alumnoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    private const string SqlPagos =
        "select * from public.rpc_listar_pagos_alumno(@alumnoId, @institucionId)"; // NOSONAR:csharpsquid:S2077

    public async Task<EstadoCuentaDto?> ObtenerEstadoCuentaAsync(
        Guid alumnoId, Guid? institucionId, string sub, CancellationToken ct)
    {
        await using var conexion = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conexion.BeginTransactionAsync(ct);
        try
        {
            await DocumentoFinancieroData.FijarClaimAsync(conexion, tx, sub, ct);

            var institucionResuelta = await DocumentoFinancieroData.ResolverInstitucionAlumnoAsync(
                conexion, tx, alumnoId, institucionId, ct);
            if (!institucionResuelta.HasValue)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            ResumenFinancieroDto? resumen = await LeerResumenAsync(
                conexion, tx, alumnoId, institucionResuelta.Value, ct);
            if (resumen is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            var cargos = await LeerCargosAsync(conexion, tx, alumnoId, resumen.InstitucionId, ct);
            var pagos = await LeerPagosAsync(conexion, tx, alumnoId, resumen.InstitucionId, ct);
            var alumnoData = await DocumentoFinancieroData.LeerAlumnoAsync(
                conexion, tx, alumnoId, resumen.InstitucionId, ct);
            var institucionData = await DocumentoFinancieroData.LeerInstitucionAsync(
                conexion, tx, resumen.InstitucionId, ct);
            var alumno = alumnoData is null
                ? new AlumnoEstadoCuentaDto { Id = alumnoId }
                : new AlumnoEstadoCuentaDto
                {
                    Id = alumnoData.Id,
                    Rne = alumnoData.Rne,
                    CodigoInterno = alumnoData.CodigoInterno,
                    NombreCompleto = alumnoData.NombreCompleto
                };
            var institucion = institucionData is null
                ? new InstitucionEstadoCuentaDto { Id = resumen.InstitucionId }
                : new InstitucionEstadoCuentaDto
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

        return FinanzasDataReader.LeerResumen(r);
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
            cargos.Add(FinanzasDataReader.LeerCargo(r));
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
            pagos.Add(FinanzasDataReader.LeerPago(r));
        }

        return pagos;
    }

}
