using System.ComponentModel.DataAnnotations;

namespace SchoolManager.API.DTOs;

public sealed class AceptarInvitacionAccesoDto
{
    [Required, MaxLength(512)]
    public string Token { get; init; } = string.Empty;
}

public sealed class OperarVinculacionIdentidadDto
{
    [Required]
    public required Guid InstitucionId { get; init; }

    [Required]
    public required Guid InvitacionId { get; init; }

    [Required, RegularExpression("^(aprobar|rechazar)$")]
    public string Operacion { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Motivo { get; init; }
}
