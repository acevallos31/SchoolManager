namespace SchoolManager.API.DTOs;

/// <summary>
/// Modelo autoritativo de negocio para el estado de cuenta de un alumno (047B).
/// Compone, dentro de una única transacción, el resumen financiero, el detalle
/// de cargos y el histórico de pagos (todos derivados de las RPC de 021) más
/// las lecturas de identidad (alumno e institución) que esas RPC no devuelven.
/// No contiene decisiones de formato (PDF/HTML/impresora): esas pertenecen a
/// la capa de presentación. El backend compone y valida todos los datos.
/// </summary>
public sealed class EstadoCuentaDto
{
    public InstitucionEstadoCuentaDto Institucion { get; init; } = new();
    public AlumnoEstadoCuentaDto Alumno { get; init; } = new();
    public ResumenFinancieroDto Resumen { get; init; } = new();
    public IReadOnlyList<CargoDto> Cargos { get; init; } = [];
    public IReadOnlyList<PagoDto> Pagos { get; init; } = [];
}

public sealed class InstitucionEstadoCuentaDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? NombreCorto { get; init; }
    public string? Direccion { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
    public string? LogoUrl { get; init; }
}

public sealed class AlumnoEstadoCuentaDto
{
    public Guid Id { get; init; }
    public string NombreCompleto { get; init; } = string.Empty;
    public string? Rne { get; init; }
    public string? CodigoInterno { get; init; }
}
