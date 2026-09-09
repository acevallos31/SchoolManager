namespace SchoolManager.API.DTOs;

// Entrada para crear una matrícula. La creación se delega a la RPC pública
// rpc_matricular_alumno, que valida en base de datos contexto (institución/ciclo),
// periodo, cupo y unicidad Alumno+Ciclo. No incluye monto: la matrícula del
// modelo académico vigente no tiene componente económica.
// Los Guid son nullable en el contrato de entrada para distinguir un campo
// omitido de Guid.Empty y evitar under-posting silencioso; el controller valida
// ambos casos antes de invocar la RPC.
public class MatriculaCreateDto
{
    public Guid? AlumnoId { get; set; }

    public Guid? SeccionId { get; set; }

    public Guid? PeriodoMatriculaId { get; set; }
}
