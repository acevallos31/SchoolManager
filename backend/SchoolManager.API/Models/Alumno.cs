namespace SchoolManager.API.Models;

public class Alumno
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public int Edad { get; set; }
    public string Sexo { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string PadresEncargados { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? Grado { get; set; }
    public string? Seccion { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
