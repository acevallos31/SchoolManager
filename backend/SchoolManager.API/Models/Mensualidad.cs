namespace SchoolManager.API.Models;

public class Mensualidad
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }
    public Guid? MatriculaId { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public decimal Monto { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string Estado { get; set; } = "pendiente"; // pendiente | pagada | vencida
    public DateTimeOffset CreadoEn { get; set; }
}
