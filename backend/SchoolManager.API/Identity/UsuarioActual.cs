namespace SchoolManager.API.Identity;

public sealed record AmbitoGlobalAcceso(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permisos
);

public sealed record InstitucionAcceso(
    Guid Id,
    string Nombre,
    string? NombreCorto,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permisos
);

public sealed record UsuarioActual(
    Guid Id,
    Guid PersonaId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permisos
)
{
    /// <summary>
    /// Asignaciones activas cuyo ámbito en usuarios_roles es global
    /// (institucion_id NULL). Durante la transición puede contener roles legacy;
    /// platform_admin también vive aquí cuando exista una asignación explícita.
    /// </summary>
    public AmbitoGlobalAcceso AmbitoGlobal { get; init; } = new(
        Array.Empty<string>(),
        Array.Empty<string>()
    );

    /// <summary>
    /// Contextos institucionales explícitos derivados exclusivamente de
    /// usuarios_roles.institucion_id. No se infieren instituciones desde roles
    /// globales para evitar fabricar membresías que la base de datos no expresa.
    /// </summary>
    public IReadOnlyList<InstitucionAcceso> Instituciones { get; init; }
        = Array.Empty<InstitucionAcceso>();
}
