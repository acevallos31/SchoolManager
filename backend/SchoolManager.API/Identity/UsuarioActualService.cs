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
            select
              u.id,
              u.persona_id,
              coalesce((
                select array_agg(distinct r.codigo order by r.codigo)
                from public.usuarios_roles ur
                join public.roles r on r.id = ur.rol_id
                where ur.usuario_id = u.id
                  and ur.activo = true
                  and r.activo = true
              ), '{}'::text[]) as roles,
              coalesce((
                select array_agg(distinct p.codigo order by p.codigo)
                from public.usuarios_roles ur
                join public.roles r on r.id = ur.rol_id
                join public.roles_permisos rp on rp.rol_id = r.id
                join public.permisos p on p.id = rp.permiso_id
                where ur.usuario_id = u.id
                  and ur.activo = true
                  and r.activo = true
              ), '{}'::text[]) as permisos
            from public.usuarios u
            where u.auth_user_id = $1
              and u.activo = true
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
            Array.AsReadOnly(reader.GetFieldValue<string[]>(2)),
            Array.AsReadOnly(reader.GetFieldValue<string[]>(3))
        );
    }
}
