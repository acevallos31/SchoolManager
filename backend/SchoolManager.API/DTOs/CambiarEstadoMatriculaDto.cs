namespace SchoolManager.API.DTOs;

// Ejemplos de estados validos: pendiente, activa, finalizada, retirada,
// anulada, trasladada. Las transiciones son validadas por la RPC
// rpc_cambiar_estado_matricula. El motivo es obligatorio para los estados
// terminales (retirada, anulada, trasladada).
public class CambiarEstadoMatriculaDto
{
    public string Estado { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}