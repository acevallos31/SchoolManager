using System.Security.Claims;
using Npgsql;

namespace SchoolManager.API.Identity;

public sealed class UsuarioActualService(NpgsqlDataSource dataSource) : IUsuarioActualService
{
    public async Task<UsuarioActual> ObtenerAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default
    )
    {
        var sub = principal.FindFirstValue("sub");

        if (!Guid.TryParse(sub, out var authUserId))
        {
            throw new UnauthorizedAccessException("El claim sub no contiene un UUID válido.");
        }

        await using var command = dataSource.CreateCommand("""
            select id, persona_id, rol
            from public.usuarios
            where auth_user_id = $1
              and activo = true
            """);
        command.Parameters.AddWithValue(authUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new UnauthorizedAccessException("No existe un usuario activo para la identidad autenticada.");
        }

        return new UsuarioActual(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2)
        );
    }
}
