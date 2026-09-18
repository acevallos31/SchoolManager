using SchoolManager.API.DTOs;

namespace SchoolManager.API.Services;

/// <summary>
/// Puerta de entrada de la capa de aplicación para los documentos de cobranza.
/// Compone el DTO autoritativo de un recibo de pago a partir de las RPC
/// existentes (rpc_obtener_pago, rpc_obtener_aplicaciones_pago) y de lecturas
/// de identidad acotadas por el contexto institucional resuelto. El controlador
/// delega aquí toda la lógica; la presentación (PDF/HTML) queda en el frontend.
/// </summary>
public interface IDocumentoReciboService
{
    /// <summary>
    /// Devuelve el recibo de pago completo (cabecera + detalle + alumno +
    /// institución emisora) dentro de una única transacción, de modo que las
    /// lecturas compartan un mismo snapshot (consistencia ACID).
    /// </summary>
    /// <param name="pagoId">Identificador del pago.</param>
    /// <param name="institucionId">Institución de operación (opcional; el backend la resuelve del claim/contexto).</param>
    /// <param name="sub">Claim sub del JWT (usuario autenticado).</param>
    /// <returns>El recibo, o <c>null</c> si el pago no existe o no es accesible.</returns>
    Task<ReciboPagoDto?> ObtenerReciboPagoAsync(Guid pagoId, Guid? institucionId, string sub, CancellationToken ct);
}