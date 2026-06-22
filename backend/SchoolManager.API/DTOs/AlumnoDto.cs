using System.Text.Json.Serialization;

namespace SchoolManager.API.DTOs;

public class AlumnoDto
{
    public Guid Id { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Sexo { get; set; } = string.Empty;

    [JsonPropertyName("dni")]
    public string Dni { get; set; } = string.Empty;

    [JsonPropertyName("fecha_nacimiento")]
    public DateOnly? FechaNacimiento { get; set; }

    [JsonPropertyName("padres_encargados")]
    public string PadresEncargados { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    [JsonPropertyName("correo_acceso")]
    public string? CorreoAcceso { get; set; }

    [JsonPropertyName("usuario_acceso")]
    public string? UsuarioAcceso { get; set; }

    [JsonPropertyName("tutor_id")]
    public Guid? TutorId { get; set; }

    public string Estado { get; set; } = "activo";

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    public string Nombre => $"{Nombres} {Apellidos}".Trim();
    public string Identidad => Dni;
    public int? Edad => FechaNacimiento is null ? null : CalcularEdad(FechaNacimiento.Value);

    private static int CalcularEdad(DateOnly fechaNacimiento)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var edad = hoy.Year - fechaNacimiento.Year;

        if (fechaNacimiento > hoy.AddYears(-edad))
        {
            edad--;
        }

        return edad;
    }
}

public class AlumnoCreateDto
{
    public string? Nombres { get; set; }
    public string? Apellidos { get; set; }
    public string? Sexo { get; set; }

    [JsonPropertyName("dni")]
    public string? Dni { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [JsonPropertyName("fecha_nacimiento")]
    public DateOnly? FechaNacimientoSnake { get; set; }

    public string? PadresEncargados { get; set; }
    public string? Direccion { get; set; }
    public string? UsuarioAcceso { get; set; }
    public string? NombreUsuarioAcceso { get; set; }
    public string? CorreoAcceso { get; set; }
    public string? PasswordAcceso { get; set; }

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
