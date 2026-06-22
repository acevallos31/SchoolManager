using System.Text.Json.Serialization;

namespace SchoolManager.API.DTOs;

public class AlumnoDto
{
    public Guid Id { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public int Edad { get; set; }
    public string Sexo { get; set; } = string.Empty;

    [JsonPropertyName("dni")]
    public string Dni { get; set; } = string.Empty;

    [JsonPropertyName("padres_encargados")]
    public string PadresEncargados { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    public string? Grado { get; set; }
    public string? Seccion { get; set; }
    public string Estado { get; set; } = "activo";

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    public string Nombre => $"{Nombres} {Apellidos}".Trim();
    public string Identidad => Dni;
}

public class AlumnoCreateDto
{
    public string? Nombres { get; set; }
    public string? Apellidos { get; set; }
    public int? Edad { get; set; }
    public string? Sexo { get; set; }

    [JsonPropertyName("dni")]
    public string? Dni { get; set; }

    public string? PadresEncargados { get; set; }
    public string? Direccion { get; set; }

    public string? Grado { get; set; }
    public string? Seccion { get; set; }
    public string? Estado { get; set; }

    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public string? Identidad { get; set; }
}

public sealed class AlumnoUpdateDto : AlumnoCreateDto
{
}

public sealed class AlumnoResponseDto : AlumnoDto
{
}
