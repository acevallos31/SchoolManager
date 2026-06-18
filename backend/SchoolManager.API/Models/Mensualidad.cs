namespace SchoolManager.API.Models;

public class Mensualidad
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }
    public Guid CicloId { get; set; }
    public int Mes { get; set; }
    public decimal MontoOriginal { get; set; }
    public decimal Descuento { get; set; } = 0;
    public decimal MontoFinal { get; set; }
    public string Estado { get; set; } = "pendiente";
    public DateOnly FechaLimite { get; set; }
    public DateTime CreatedAt { get; set; }
}
