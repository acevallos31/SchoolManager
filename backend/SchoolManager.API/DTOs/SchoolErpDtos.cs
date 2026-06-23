using System.Text.Json.Serialization;

namespace SchoolManager.API.DTOs;

public sealed class JornadaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public sealed class NivelDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class CicloEscolarDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("fecha_inicio")]
    public DateOnly FechaInicio { get; set; }

    [JsonPropertyName("fecha_fin")]
    public DateOnly FechaFin { get; set; }

    [JsonPropertyName("matricula_inicio")]
    public DateOnly? MatriculaInicio { get; set; }

    [JsonPropertyName("matricula_fin")]
    public DateOnly? MatriculaFin { get; set; }

    public bool Activo { get; set; } = true;
}

public sealed class PlanPagoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "mensual";

    [JsonPropertyName("tipo_plan_pago_id")]
    public Guid? TipoPlanPagoId { get; set; }

    public string? Descripcion { get; set; }

    [JsonPropertyName("monto_matricula")]
    public decimal MontoMatricula { get; set; }

    [JsonPropertyName("monto_total_anual")]
    public decimal MontoTotalAnual { get; set; }

    [JsonPropertyName("cantidad_cuotas")]
    public int CantidadCuotas { get; set; }

    [JsonPropertyName("mes_inicio")]
    public int MesInicio { get; set; } = 1;

    [JsonPropertyName("dia_vencimiento")]
    public int DiaVencimiento { get; set; } = 10;

    [JsonPropertyName("descuento_porcentaje")]
    public decimal DescuentoPorcentaje { get; set; }

    public bool Activo { get; set; } = true;
}

public sealed class PlanPagoCreateDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "mensual";
    public Guid? TipoPlanPagoId { get; set; }
    public string? Descripcion { get; set; }
    public decimal MontoMatricula { get; set; }
    public decimal MontoTotalAnual { get; set; }
    public int CantidadCuotas { get; set; }
    public int MesInicio { get; set; } = 1;
    public int DiaVencimiento { get; set; } = 10;
    public decimal DescuentoPorcentaje { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class CargoDto
{
    public Guid Id { get; set; }

    [JsonPropertyName("matricula_id")]
    public Guid MatriculaId { get; set; }

    [JsonPropertyName("alumno_id")]
    public Guid AlumnoId { get; set; }

    public string Tipo { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;

    [JsonPropertyName("numero_cuota")]
    public int? NumeroCuota { get; set; }

    public decimal Monto { get; set; }

    [JsonPropertyName("fecha_vencimiento")]
    public DateOnly FechaVencimiento { get; set; }

    public string Estado { get; set; } = "pendiente";

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class RegistrarPagoCargoDto
{
    public Guid CargoId { get; set; }

    [JsonPropertyName("cargo_id")]
    public Guid CargoIdSnake { get; set; }

    [JsonPropertyName("mensualidad_id")]
    public Guid MensualidadId { get; set; }

    public decimal MontoPagado { get; set; }

    [JsonPropertyName("monto_pagado")]
    public decimal MontoPagadoSnake { get; set; }

    public string MetodoPago { get; set; } = "efectivo";

    [JsonPropertyName("metodo_pago")]
    public string? MetodoPagoSnake { get; set; }

    public Guid CargoIdEfectivo => CargoId != Guid.Empty
        ? CargoId
        : CargoIdSnake != Guid.Empty
            ? CargoIdSnake
            : MensualidadId;

    public decimal MontoPagadoEfectivo => MontoPagado > 0 ? MontoPagado : MontoPagadoSnake;

    public string MetodoPagoEfectivo => string.IsNullOrWhiteSpace(MetodoPagoSnake) ? MetodoPago : MetodoPagoSnake;
}

public sealed class UsuarioDto
{
    public Guid Id { get; set; }
    public string? Usuario { get; set; }
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("nombre_completo")]
    public string? NombreCompleto { get; set; }

    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    [JsonPropertyName("supabase_uid")]
    public Guid? SupabaseUid { get; set; }
}

public sealed class UsuarioCreateDto
{
    public string? Usuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Rol { get; set; } = "operador";
    public bool Activo { get; set; } = true;
}
