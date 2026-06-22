namespace SchoolManager.API.Models;

public class Matricula
{
    public Guid Id { get; set; }

    public Guid AlumnoId { get; set; }
    public Guid CicloId { get; set; }

    public int AnioEscolar { get; set; }

    public DateOnly FechaMatricula { get; set; }

    public decimal Monto { get; set; }

    public string Estado { get; set; } = "activa";

    public Guid? RegistradoPor { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTime CreatedAt { get; set; }
}
