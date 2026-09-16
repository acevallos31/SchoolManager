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
    IReadOnlyList<string> Permisos,
    bool Activo = true
);

public sealed record UsuarioActual(
    Guid Id,
    Guid PersonaId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permisos
)
{
    public string NombreCompleto { get; init; } = string.Empty;

    public AmbitoGlobalAcceso AmbitoGlobal { get; init; } = new(
        Array.Empty<string>(),
        Array.Empty<string>()
    );

    public IReadOnlyList<InstitucionAcceso> Instituciones { get; init; }
        = Array.Empty<InstitucionAcceso>();

    /// <summary>
    /// Catálogo institucional visible para platform_admin sin depender del modo
    /// mono/multi-institución. Puede incluir instituciones inactivas, marcadas
    /// mediante Activo, pero esto no fabrica membresías ni permisos locales.
    /// </summary>
    public IReadOnlyList<InstitucionAcceso> InstitucionesAdministrables { get; init; }
        = Array.Empty<InstitucionAcceso>();
}
