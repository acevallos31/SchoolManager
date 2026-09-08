using System.ComponentModel.DataAnnotations;

namespace SchoolManager.API.DTOs;

public sealed class ActualizarModoDto
{
    [Required]
    public bool? MultiplesInstituciones { get; init; }
}

public sealed class GuardarInstitucionDto
{
    [Required]
    public required string Nombre { get; init; }
    public string? NombreCorto { get; init; }
    public string? Direccion { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
    public string? LogoUrl { get; init; }
    [Required]
    public required ConfiguracionIdentificadoresDto Identificadores { get; init; }
}

public sealed class ConfiguracionIdentificadoresDto
{
    public required bool RneRequerido { get; init; }
    public required bool IdentificacionCivilRequerida { get; init; }
    public required bool CodigoInternoRequerido { get; init; }
    [Required]
    public required string[] TiposIdentificacionPermitidos { get; init; }
}
