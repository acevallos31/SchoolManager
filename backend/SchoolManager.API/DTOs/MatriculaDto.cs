namespace SchoolManager.API.DTOs;

// Respuesta de solo lectura para una matrícula, alineada con el modelo
// académico vigente (mocke -> alumno + sección + periodo). Los nombres y el
// contexto se resuelven por JOIN para presentación sin duplicar la entidad.
public class MatriculaDto
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }
    public Guid InstitucionId { get; set; }
    public Guid CicloId { get; set; }
    public Guid SeccionId { get; set; }
    public Guid PeriodoMatriculaId { get; set; }
    public Guid? RegistradoPor { get; set; }
    public DateOnly FechaMatricula { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Contexto resuelto para presentación
    public string NombreAlumno { get; set; } = string.Empty;
    public string NombreSeccion { get; set; } = string.Empty;
    public string NombreGrado { get; set; } = string.Empty;
    public string? NombreJornada { get; set; }
    public string NombrePeriodo { get; set; } = string.Empty;
    public string CicloNombre { get; set; } = string.Empty;
}