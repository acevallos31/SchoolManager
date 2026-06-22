namespace SchoolManager.API.DTOs;

public sealed record LoginRequest(string Correo, string Password);

public sealed record UsuarioActualDto(
    string Id,
    string SupabaseUid,
    string Nombre,
    string? NombreCompleto,
    string? Correo,
    string Rol,
    bool Activo = true
);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    long ExpiresIn,
    UsuarioActualDto Usuario
);
