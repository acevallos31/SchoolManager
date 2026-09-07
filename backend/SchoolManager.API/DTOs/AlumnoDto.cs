namespace SchoolManager.API.DTOs;

// Alumno tal como se expone al API. La identidad global se resuelve por JOIN a
// persona; la matrícula activa actual (con grado/jornada/sección) es un vínculo
// opcional que solo se carga cuando la lectura lo pide (listado completo), y no
// forma parte de las lecturas paginadas — igual que el contrato del frontend.
public class AlumnoDto
{
    public Guid Id { get; set; }
    public Guid PersonaId { get; set; }
    public Guid InstitucionId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Identidad { get; set; }
    public string? Rne { get; set; }
    public string? CodigoInterno { get; set; }
    public string Estado { get; set; } = "activo";
    public MatriculaActualAlumnoDto? MatriculaActual { get; set; }
}

// Matrícula activa vigente del alumno, en la forma que consume la UI de alumno.
public class MatriculaActualAlumnoDto
{
    public string Id { get; set; } = string.Empty;
    public string Ciclo { get; set; } = string.Empty;
    public string Grado { get; set; } = string.Empty;
    public string Seccion { get; set; } = string.Empty;
}

// Crear un alumno con una persona nueva (documento). Atómico en la RPC.
public class CrearAlumnoDto
{
    public Guid InstitucionId { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string TipoIdentificacion { get; set; } = string.Empty;
    public string NumeroIdentificacion { get; set; } = string.Empty;
    public DateOnly? FechaNacimiento { get; set; }
    public string? Rne { get; set; }
    public string? CodigoInterno { get; set; }
}