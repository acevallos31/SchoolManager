namespace SchoolManager.API.DTOs;

// Grado academico tal como lo consume el frontend. La lectura delega en la RPC
// rpc_listar_grados (016); el shape replica el contrato actual de
// estructura-academica.service.ts para no cambiar callers.
public class GradoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

// Jornada academica (manana/tarde/etc.). Misma convencion que el grado.
public class JornadaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

// Seccion de un ciclo (grado + jornada + grupo). Incluye el contexto nominal de
// grado/jornada (grado_nombre/jornada_nombre) que la RPC resuelve por JOIN, de
// forma que el listado de secciones llega listo para pintar sin round-trips.
// La jornada es opcional (la seccion puede no tener jornada asignada).
public class SeccionDto
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public Guid CicloId { get; set; }
    public Guid GradoId { get; set; }
    public string GradoNombre { get; set; } = string.Empty;
    public Guid? JornadaId { get; set; }
    public string? JornadaNombre { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? Cupo { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset? FechaDesactivacion { get; set; }
    public string? MotivoDesactivacion { get; set; }
}

// Crear/actualizar un grado (nombre + orden). La institucion puede venir en el
// body o resolverse del contexto; el controller la traduce a la RPC 016.
public class GradoInputDto
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
    public Guid? InstitucionId { get; set; }
}

// Crear/actualizar una jornada. La institucion puede venir en el body o
// resolverse del contexto.
public class JornadaInputDto
{
    public string Nombre { get; set; } = string.Empty;
    public Guid? InstitucionId { get; set; }
}

// Crear/actualizar una seccion dentro de un ciclo. cicloId/gradoId son
// obligatorios (el controller los valida antes de invocar la DB); jornadaId y
// cupo son opcionales. La institucion puede venir en el body o resolverse del
// contexto.
public class SeccionInputDto
{
    public Guid CicloId { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
    public Guid GradoId { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
    public Guid? JornadaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? Cupo { get; set; }
    public Guid? InstitucionId { get; set; }
}
