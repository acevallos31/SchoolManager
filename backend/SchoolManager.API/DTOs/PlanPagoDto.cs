namespace SchoolManager.API.DTOs;

// Cuota de un plan de pago (importe + opcionalmente ligada a un concepto).
public class PlanCuotaDto
{
    public Guid? Id { get; set; }
    public int Orden { get; set; }
    public Guid? ConceptoId { get; set; }
    public string? ConceptoNombre { get; set; }
    public string? Descripcion { get; set; }
    public decimal Monto { get; set; }
    public int VencimientoDias { get; set; }
}

// Fila del listado de planes: resumen sin cuotas.
public class PlanPagoListaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset? FechaDesactivacion { get; set; }
    public string? MotivoDesactivacion { get; set; }
    public long TotalCuotas { get; set; }
    public decimal MontoTotal { get; set; }
}

// Detalle de un plan con sus cuotas (rpc_obtener_plan_pago).
public class PlanPagoDetalleDto : PlanPagoListaDto
{
    public List<PlanCuotaDto> Cuotas { get; set; } = [];
}

// Entrada para crear/editar un plan con sus cuotas (se envían juntas).
public class PlanPagoUpsertDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public List<PlanCuotaInputDto> Cuotas { get; set; } = [];
}

public class PlanCuotaInputDto
{
    public int Orden { get; set; }
    public Guid? ConceptoId { get; set; }
    public string? Descripcion { get; set; }
    public decimal Monto { get; set; }
    public int VencimientoDias { get; set; }
}

// Entrada para desactivar un plan (motivo obligatorio).
public class DesactivarPlanDto
{
    public string Motivo { get; set; } = string.Empty;
}