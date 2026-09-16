using System.ComponentModel.DataAnnotations;

namespace SchoolManager.API.DTOs;

public sealed class CrearRolInstitucionalDto
{
    [Required]
    public required Guid InstitucionId { get; init; }

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
    public required Guid InstitucionId { get; init; }

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
    public required Guid UsuarioId { get; init; }
}

public sealed class PrepararInvitacionUsuarioDto
{
    [Required]
    public required Guid InstitucionId { get; init; }

    [Required, MaxLength(160)]
    public string Nombres { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Apellidos { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(320)]
    public string Correo { get; init; } = string.Empty;

    [Required]
    public required Guid RolId { get; init; }

    [MaxLength(30)]
    public string Origen { get; init; } = "administracion";
}

public sealed class DesactivarRbacDto
{
    [Required, MaxLength(500)]
    public string Motivo { get; init; } = string.Empty;
}
