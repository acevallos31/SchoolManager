namespace SchoolManager.API.DTOs;

// Usado para listar / mostrar una mensualidad
public class MensualidadDto
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public decimal Monto { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
}

// Usado para respuestas detalladas
public class MensualidadResponseDto
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public string NombreMes { get; set; } = string.Empty;
    public decimal MontoOriginal { get; set; }
    public decimal Descuento { get; set; }
    public decimal MontoFinal { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateOnly FechaLimite { get; set; }
}

// Usado al generar mensualidades nuevas
public class MensualidadCreateDto
{
    public Guid AlumnoId { get; set; }
    public Guid? MatriculaId { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public decimal Monto { get; set; }
    public DateOnly FechaVencimiento { get; set; }
}

public class DescuentoDto
{
    public decimal Descuento { get; set; }
}

public class PagoCreateDto
{
    public Guid MensualidadId { get; set; }
    public decimal MontoPagado { get; set; }
    public string MetodoPago { get; set; } = "efectivo";
}

public class MatriculaCreateDto
{
    public Guid AlumnoId { get; set; }
    public Guid CicloId { get; set; }
    public decimal Monto { get; set; }
}
