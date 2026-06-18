namespace SchoolManager.API.Models;

public class Alumno
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Identidad { get; set; } = string.Empty;
    public DateOnly? FechaNacimiento { get; set; }
    public string Grado { get; set; } = string.Empty;
    public string? Seccion { get; set; }
    public string Estado { get; set; } = "activo";
    public Guid? TutorId { get; set; }
    public DateTime CreatedAt { get; set; }
}
