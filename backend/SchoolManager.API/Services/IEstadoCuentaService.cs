using SchoolManager.API.DTOs;

namespace SchoolManager.API.Services;

/// <summary>
/// Puerta de entrada de la capa de aplicación para los documentos financieros
/// de consulta (047B). Compone el DTO autoritativo del estado de cuenta de un
/// alumno a partir de las RPC existentes de 021 (rpc_resumen_financiero_alumno,
/// rpc_listar_cargos_alumno, rpc_listar_pagos_alumno) y de lecturas de
/// identidad acotadas por el contexto institucional resuelto. El controlador
/// delega aquí toda la lógica; la presentación (PDF/HTML) queda en el frontend.
/// </summary>
public interface IEstadoCuentaService
{
    /// <summary>
    /// Devuelve el estado de cuenta completo (resumen + cargos + pagos +
    /// alumno + institución) dentro de una única transacción, de modo que las
    /// lecturas compartan un mismo snapshot (consistencia ACID).
    /// </summary>
    /// <param name="alumnoId">Identificador del alumno.</param>
    /// <param name="institucionId">Institución de operación (opcional; el backend la resuelve del claim/contexto).</param>
    /// <param name="sub">Claim sub del JWT (usuario autenticado).</param>
    /// <returns>El estado de cuenta, o <c>null</c> si el alumno no existe o no es accesible.</returns>
    Task<EstadoCuentaDto?> ObtenerEstadoCuentaAsync(Guid alumnoId, Guid? institucionId, string sub, CancellationToken ct);
}
