namespace SchoolManager.API.DTOs;

// Configuración financiera: concepto de cobro (catálogo de cargos por
// institución). Solo lectura del catálogo; la edición se delega a las RPCs
// SECURITY DEFINER del bloque 019.
public class ConceptoFinancieroDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Monto { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset? FechaDesactivacion { get; set; }
    public string? MotivoDesactivacion { get; set; }
}

// Entrada para crear/editar un concepto financiero.
public class ConceptoFinancieroUpsertDto
{
    public string Nombre { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
}

// Entrada para desactivar un concepto (el motivo es obligatorio).
public class DesactivarConceptoDto
{
    public string Motivo { get; set; } = string.Empty;
}