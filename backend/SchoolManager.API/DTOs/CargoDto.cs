namespace SchoolManager.API.DTOs;

// Obligacion/cargo generado a partir de una cuota de un plan de pago.
// Estados (021 en main): 'pendiente' | 'anulado' | 'pagado' | 'parcial'.
// 'pendiente'/'anulado' se persisten; 'pagado'/'parcial' derivan del saldo.
// El vencido es derivado por fecha (es_vencido), no persistido.
public class CargoDto
{
    public Guid Id { get; set; }
    public Guid MatriculaId { get; set; }
    public Guid AlumnoId { get; set; }
    public Guid? PlanPagoId { get; set; }
    public int Orden { get; set; }
    public Guid? ConceptoId { get; set; }
    public string? ConceptoNombre { get; set; }
    public string? Descripcion { get; set; }
    public decimal MontoOriginal { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset FechaGeneracion { get; set; }
    public DateTimeOffset? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public bool EsVencido { get; set; }
    // Bloque 021: saldo derivado (monto_original - aplicaciones vigentes) y
    // total aplicado, anexados por la migracion. El saldo NO se almacena.
    public decimal Saldo { get; set; }
    public decimal Aplicado { get; set; }
}

// Resumen financiero de un alumno (rpc_resumen_financiero_alumno).
public class ResumenFinancieroDto
{
    public Guid AlumnoId { get; set; }
    public Guid InstitucionId { get; set; }
    public long TotalObligaciones { get; set; }
    public decimal TotalMontoOriginal { get; set; }
    public decimal TotalPendiente { get; set; }
    public decimal TotalVencido { get; set; }
    public decimal TotalAnulado { get; set; }
    // Bloque 021: total aplicado (vigente) anexado por la migracion.
    public decimal TotalAplicado { get; set; }
}

// Entrada para asignar un plan de pago a una matricula.
public class AsignarPlanPagoDto
{
    public Guid PlanPagoId { get; set; }
}

// Entrada para anular un cargo (motivo obligatorio, soft state).
public class AnularCargoDto
{
    public string Motivo { get; set; } = string.Empty;
}
