namespace SchoolManager.API.DTOs;

// Usado para listar / mostrar una mensualidad
public class MensualidadDto
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public decimal Monto { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
}

// Usado para respuestas detalladas
public class MensualidadResponseDto
{
    public Guid Id { get; set; }
    public Guid AlumnoId { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public string NombreMes { get; set; } = string.Empty;
    public decimal MontoOriginal { get; set; }
    public decimal Descuento { get; set; }
    public decimal MontoFinal { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateOnly FechaLimite { get; set; }
}

// Usado al generar mensualidades nuevas
public class MensualidadCreateDto
{
    public Guid AlumnoId { get; set; }
    public Guid? MatriculaId { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public decimal Monto { get; set; }
    public DateOnly FechaVencimiento { get; set; }
}

public class DescuentoDto
{
    public decimal Descuento { get; set; }
}

public class PagoCreateDto
{
    public Guid MensualidadId { get; set; }
    public decimal MontoPagado { get; set; }
    public string MetodoPago { get; set; } = "efectivo";
}

public class MatriculaCreateDto
{
    public Guid AlumnoId { get; set; }
    public Guid CicloId { get; set; }
    public Guid GradoId { get; set; }
    public Guid SeccionId { get; set; }
    public Guid PlanPagoId { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "pendiente";
}

public class MatriculaDto
{
    public Guid Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("alumno_id")]
    public Guid AlumnoId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("ciclo_id")]
    public Guid CicloId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("grado_id")]
    public Guid GradoId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("seccion_id")]
    public Guid SeccionId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("plan_pago_id")]
    public Guid? PlanPagoId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("fecha_matricula")]
    public DateOnly FechaMatricula { get; set; }

    public decimal Monto { get; set; }
    public string Estado { get; set; } = "pendiente";

    [System.Text.Json.Serialization.JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

public class CatalogoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
