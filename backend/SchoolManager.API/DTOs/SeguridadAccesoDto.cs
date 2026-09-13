using System.ComponentModel.DataAnnotations;

namespace SchoolManager.API.DTOs;

public sealed class CrearRolInstitucionalDto
{
    [Required]
    public Guid InstitucionId { get; init; }

    [Required, MaxLength(80)]
    public string Codigo { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Nombre { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; init; }
}

public sealed class ClonarPlantillaRolDto
{
    [Required]
    public Guid InstitucionId { get; init; }

    [Required, MaxLength(80)]
    public string PlantillaCodigo { get; init; } = string.Empty;

    [Required, MaxLength(80)]
    public string Codigo { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Nombre { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; init; }
}

public sealed class EditarRolInstitucionalDto
{
    [Required, MaxLength(160)]
    public string Nombre { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; init; }
}

public sealed class ReemplazarPermisosRolDto
{
    [Required]
    public string[] Permisos { get; init; } = [];
}

public sealed class AsignarRolInstitucionalDto
{
    [Required]
    public Guid UsuarioId { get; init; }
}

public sealed class DesactivarRbacDto
{
    [Required, MaxLength(500)]
    public string Motivo { get; init; } = string.Empty;
}
