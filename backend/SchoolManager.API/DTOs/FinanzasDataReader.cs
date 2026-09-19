using Npgsql;

namespace SchoolManager.API.DTOs;

/// <summary>
/// Mapeo canónico de filas devueltas por las RPC financieras de 021.
/// Centralizarlo evita que controllers y documentos repliquen el orden de
/// columnas, sin mover reglas de negocio fuera de PostgreSQL.
/// </summary>
internal static class FinanzasDataReader
{
    internal static CargoDto LeerCargo(NpgsqlDataReader r) => new()
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
    };

    internal static ResumenFinancieroDto LeerResumen(NpgsqlDataReader r) => new()
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

    internal static PagoDto LeerPago(NpgsqlDataReader r) => new()
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
    };

    internal static AplicacionPagoDto LeerAplicacion(NpgsqlDataReader r) => new()
    {
        AplicacionId = r.GetGuid(0),
        PagoId = r.GetGuid(1),
        CargoId = r.GetGuid(2),
        InstitucionId = r.GetGuid(3),
        MontoAplicado = r.GetDecimal(4),
        Estado = r.GetString(5),
        FechaReversion = r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
        CargoEstado = r.GetString(7),
        ConceptoNombre = r.IsDBNull(8) ? null : r.GetString(8),
        MontoOriginal = r.GetDecimal(9)
    };
}
