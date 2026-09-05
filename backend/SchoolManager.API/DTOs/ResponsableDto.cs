namespace SchoolManager.API.DTOs;

// Responsable tal como se expone al API. La identidad se resuelve por JOIN a
// persona; el vínculo activo con el alumno (parentesco/principal) forma parte
// de la vista de "responsables de un alumno" y NO de este DTO base.
public class ResponsableDto
{
    public Guid Id { get; set; }
    public Guid PersonaId { get; set; }
    public Guid InstitucionId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? TipoIdentificacion { get; set; }
    public string? NumeroIdentificacion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FechaDesactivacion { get; set; }
    public string? MotivoDesactivacion { get; set; }
}

// Vínculo Responsable<->Alumno dentro de la vista del alumno.
public class ResponsableVinculoDto
{
    public Guid Id { get; set; }
    public Guid ResponsableId { get; set; }
    public string? Parentesco { get; set; }
    public bool EsPrincipal { get; set; }
    public bool AccesoFinanciero { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
}

// Crear responsable con nueva persona (documento) o reutilizando una existente.
public class CrearResponsableDto
{
    public Guid InstitucionId { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string TipoIdentificacion { get; set; } = string.Empty;
    public string NumeroIdentificacion { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
}

// Crear responsable para una persona ya conocida (sin duplicar identidad).
public class CrearResponsableParaPersonaDto
{
    public Guid PersonaId { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
    public Guid InstitucionId { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
}

// Edición de los datos permitidos de la persona del responsable.
public class EditarResponsableDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
}

// Vincular un responsable a un alumno.
public class VincularResponsableDto
{
    public Guid ResponsableId { get; set; } // NOSONAR:csharpsquid:S6964 (requerido; se valida en el controller)
    public string? Parentesco { get; set; }
    public bool EsPrincipal { get; set; } // NOSONAR:csharpsquid:S6964 (valor por defecto false es válido)
    public bool AccesoFinanciero { get; set; } // NOSONAR:csharpsquid:S6964 (valor por defecto false es válido)
}

// Editar un vínculo existente.
public class EditarVinculoDto
{
    public string? Parentesco { get; set; }
    public bool? EsPrincipal { get; set; }
    public bool? AccesoFinanciero { get; set; }
}

// Desactivación soft de responsable o vínculo (motivo obligatorio).
public class DesactivarDto
{
    public string Motivo { get; set; } = string.Empty;
}