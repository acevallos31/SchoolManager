namespace SchoolManager.API.DTOs;

/// <summary>
/// Modelo autoritativo de negocio para representar un recibo de pago.
/// No contiene decisiones de formato (PDF/HTML/impresora): esas pertenecen
/// a la capa de presentación. El backend compone y valida todos los datos.
/// </summary>
public sealed class ReciboPagoDto
{
    public Guid PagoId { get; init; }
    public long NumeroRecibo { get; init; }
    public DateTimeOffset FechaPago { get; init; }
    public decimal MontoTotal { get; init; }
    public string? MetodoPago { get; init; }
    public string? ReferenciaExterna { get; init; }
    public string Estado { get; init; } = string.Empty;
    public DateTimeOffset? FechaAnulacion { get; init; }
    public string? MotivoAnulacion { get; init; }
    public InstitucionReciboDto Institucion { get; init; } = new();
    public AlumnoReciboDto Alumno { get; init; } = new();
    public IReadOnlyList<ReciboPagoDetalleDto> Detalles { get; init; } = [];
}

public sealed class InstitucionReciboDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? NombreCorto { get; init; }
    public string? Direccion { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
    public string? LogoUrl { get; init; }
}

public sealed class AlumnoReciboDto
{
    public Guid Id { get; init; }
    public string NombreCompleto { get; init; } = string.Empty;
    public string? Rne { get; init; }
    public string? CodigoInterno { get; init; }
}

public sealed class ReciboPagoDetalleDto
{
    public Guid CargoId { get; init; }
    public string Concepto { get; init; } = string.Empty;
    public decimal MontoAplicado { get; init; }
    public string Estado { get; init; } = string.Empty;
}
