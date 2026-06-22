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
