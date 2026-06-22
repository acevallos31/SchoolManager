namespace SchoolManager.API.DTOs;


// Usado para listar / mostrar un alumno
public class AlumnoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public DateOnly FechaNacimiento { get; set; }
    public string Grado { get; set; } = string.Empty;
    public string? Seccion { get; set; }
    public bool Activo { get; set; }
}

// Usado al crear o actualizar un alumno
public class AlumnoCreateDto
{
    public Guid? PadreId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public DateOnly FechaNacimiento { get; set; }
    public string Grado { get; set; } = string.Empty;
    public string? Seccion { get; set; }

public class AlumnoCreateDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Identidad { get; set; } = string.Empty;
    public DateOnly? FechaNacimiento { get; set; }
    public string Grado { get; set; } = string.Empty;
    public string? Seccion { get; set; }
    public Guid? TutorId { get; set; }
}

public class AlumnoResponseDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Identidad { get; set; } = string.Empty;
    public string Grado { get; set; } = string.Empty;
    public string? Seccion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

}
