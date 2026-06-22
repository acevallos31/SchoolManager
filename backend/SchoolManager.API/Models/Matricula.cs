namespace SchoolManager.API.Models;

public class Matricula
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }

    public int AnioEscolar { get; set; }
    public DateOnly FechaMatricula { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "activa"; // activa | retirada | finalizada
    public DateTimeOffset CreadoEn { get; set; }

    public Guid CicloId { get; set; }
    public DateOnly FechaMatricula { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "pendiente";
    public Guid? RegistradoPor { get; set; }
    public DateTime CreatedAt { get; set; }

}
