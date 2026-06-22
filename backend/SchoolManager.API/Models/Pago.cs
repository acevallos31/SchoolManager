namespace SchoolManager.API.Models;

public class Pago
{
    public Guid Id { get; set; }
    public Guid MensualidadId { get; set; }

    public decimal Monto { get; set; }
    public DateOnly FechaPago { get; set; }
    public string MetodoPago { get; set; } = string.Empty; // efectivo | transferencia | tarjeta | otro
    public string? ComprobanteUrl { get; set; }
    public Guid? RegistradoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public DateOnly FechaPago { get; set; }
    public decimal MontoPagado { get; set; }
    public string MetodoPago { get; set; } = "efectivo";
    public Guid? RegistradoPor { get; set; }
    public DateTime CreatedAt { get; set; }

}
