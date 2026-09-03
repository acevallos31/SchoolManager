namespace SchoolManager.API.DTOs;

// Entrada para crear una matrícula. La creación se delega a la RPC pública
// rpc_matricular_alumno, que valida en base de datos contexto (institución/ciclo),
// periodo, cupo y unicidad Alumno+Ciclo. No incluye monto: la matrícula del
// modelo académico vigente no tiene componente económica.
public class MatriculaCreateDto
{
    public Guid AlumnoId { get; set; }

    public Guid SeccionId { get; set; }

    public Guid PeriodoMatriculaId { get; set; }
}
