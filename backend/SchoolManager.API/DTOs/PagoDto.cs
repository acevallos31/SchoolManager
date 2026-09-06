namespace SchoolManager.API.DTOs;

// Cabecera de un pago (rpc_listar_pagos_alumno / rpc_obtener_pago).
// Bloque 021 (en main): pago como hecho transaccional independiente de los
// cargos a los que se aplica. El saldo del cargo es derivado; aqui se refleja
// el monto del pago y su estado (registrado | anulado). Anular = trazabilidad.
public class PagoDto
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public Guid AlumnoId { get; set; }
    public Guid? ResponsableId { get; set; }
    public long NumeroRecibo { get; set; }
    public decimal MontoTotal { get; set; }
    public DateTimeOffset FechaPago { get; set; }
    public string? MetodoPago { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string Estado { get; set; } = string.Empty;
    public Guid? RegistradoPor { get; set; }
    public DateTimeOffset? FechaAnulacion { get; set; }
    public Guid? AnuladoPor { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// Aplicacion de un pago a un cargo (rpc_obtener_aplicaciones_pago).
// Suma de aplicaciones 'vigente' de un pago = su monto_total. Al anular, las
// aplicaciones pasan a 'reversada' (no se eliminan) para conservar trazabilidad.
public class AplicacionPagoDto
{
    public Guid AplicacionId { get; set; }
    public Guid PagoId { get; set; }
    public Guid CargoId { get; set; }
    public Guid InstitucionId { get; set; }
    public decimal MontoAplicado { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset? FechaReversion { get; set; }
    public string CargoEstado { get; set; } = string.Empty;
    public string? ConceptoNombre { get; set; }
    public decimal MontoOriginal { get; set; }
}

// Cargo al que se aplica una parte de un pago.
public class AplicacionCargoInputDto
{
    public Guid CargoId { get; set; }
    public decimal Monto { get; set; }
}

// Entrada para registrar un pago sobre uno o varios cargos del alumno.
public class RegistrarPagoDto
{
    public decimal MontoTotal { get; set; }
    public List<AplicacionCargoInputDto> Aplicaciones { get; set; } = [];
    public Guid? ResponsableId { get; set; }
    public string? MetodoPago { get; set; }
    public string? ReferenciaExterna { get; set; }
    public DateTimeOffset? FechaPago { get; set; }
}

// Entrada para anular un pago (motivo obligatorio; revierte aplicaciones).
public class AnularPagoDto
{
    public string Motivo { get; set; } = string.Empty;
}
