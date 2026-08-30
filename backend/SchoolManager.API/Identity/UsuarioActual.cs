namespace SchoolManager.API.Identity;

public sealed record UsuarioActual(
    Guid Id,
    Guid PersonaId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permisos
);
