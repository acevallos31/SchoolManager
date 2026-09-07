namespace SchoolManager.API.DTOs;

// Ciclo escolar tal como lo consume el frontend. La lectura delega en la RPC
// rpc_listar_ciclos_escolares (014); el shape replica el contrato actual de
// ciclo-escolar.service.ts para no cambiar callers.
public class CicloEscolarDto
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public bool Activo { get; set; }
    public string? MotivoDesactivacion { get; set; }
}

// Periodo de matricula de un ciclo escolar. Igual convencion que el ciclo.
public class PeriodoMatriculaDto
{
    public Guid Id { get; set; }
    public Guid CicloId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public bool Activo { get; set; }
}

// Crear/actualizar un ciclo escolar (nombre + rango de fechas). La institucion
// puede venir en el query/body o resolverse del contexto; el controller la
// traduce a la RPC 014 correspondiente.
public class CicloEscolarInputDto
{
    public string Nombre { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public Guid? InstitucionId { get; set; }
}

// Crear/actualizar un periodo de matricula dentro de un ciclo.
public class PeriodoMatriculaInputDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
}
