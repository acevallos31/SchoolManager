namespace SchoolManager.API.DTOs;

// Hijo (alumno) del que el usuario autenticado es responsable financiero
// activo (Bloque 022, portal responsable). Proyeccion de
// rpc_mis_alumnos_responsable().
public class MisAlumnoDto
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public string? Nombres { get; set; }
    public string? Apellidos { get; set; }
    public string? Parentesco { get; set; }
    public bool EsPrincipal { get; set; }
}
