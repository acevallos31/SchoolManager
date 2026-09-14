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

        Guid usuarioId;
        Guid personaId;
        string nombreCompleto;
        string[] roles;
        string[] permisos;
        string[] rolesGlobales;
        string[] permisosGlobales;

        await using (var command = dataSource.CreateCommand("""
            select
              u.id,
              u.persona_id,
              u.activo,
              p.nombres,
              p.apellidos,
              coalesce((
                select array_agg(distinct r.codigo order by r.codigo)
                from public.usuarios_roles ur
                join public.roles r on r.id = ur.rol_id
                where ur.usuario_id = u.id
                  and ur.activo = true
                  and r.activo = true
                  and (
                    ur.institucion_id is null
                    or exists (
                      select 1
                      from public.instituciones i
                      where i.id = ur.institucion_id
                        and i.activo = true
                    )
                  )
              ), '{}'::text[]) as roles,
              coalesce((
                select array_agg(distinct pe.codigo order by pe.codigo)
                from public.usuarios_roles ur
                join public.roles r on r.id = ur.rol_id
                join public.roles_permisos rp on rp.rol_id = r.id
                join public.permisos pe on pe.id = rp.permiso_id
                where ur.usuario_id = u.id
                  and ur.activo = true
                  and r.activo = true
                  and (
                    ur.institucion_id is null
                    or exists (
                      select 1
                      from public.instituciones i
                      where i.id = ur.institucion_id
                        and i.activo = true
                    )
                  )
              ), '{}'::text[]) as permisos,
              coalesce((
                select array_agg(distinct r.codigo order by r.codigo)
                from public.usuarios_roles ur
                join public.roles r on r.id = ur.rol_id
                where ur.usuario_id = u.id
                  and ur.institucion_id is null
                  and ur.activo = true
                  and r.activo = true
              ), '{}'::text[]) as roles_globales,
              coalesce((
                select array_agg(distinct pe.codigo order by pe.codigo)
                from public.usuarios_roles ur
                join public.roles r on r.id = ur.rol_id
                join public.roles_permisos rp on rp.rol_id = r.id
                join public.permisos pe on pe.id = rp.permiso_id
                where ur.usuario_id = u.id
                  and ur.institucion_id is null
                  and ur.activo = true
                  and r.activo = true
              ), '{}'::text[]) as permisos_globales
            from public.usuarios u
            join public.personas p on p.id = u.persona_id
            where u.auth_user_id = $1
            """))
        {
            command.Parameters.AddWithValue(authUserId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new IdentidadNoVinculadaException(authUserId);
            }

            if (!reader.GetBoolean(2))
            {
                throw new UsuarioInactivoException(reader.GetGuid(0));
            }

            usuarioId = reader.GetGuid(0);
            personaId = reader.GetGuid(1);
            nombreCompleto = string.Join(
                ' ',
                new[] { reader.GetString(3), reader.GetString(4) }
                    .Where(valor => !string.IsNullOrWhiteSpace(valor))
            ).Trim();
            roles = reader.GetFieldValue<string[]>(5);
            permisos = reader.GetFieldValue<string[]>(6);
            rolesGlobales = reader.GetFieldValue<string[]>(7);
            permisosGlobales = reader.GetFieldValue<string[]>(8);
        }

        var instituciones = await ObtenerInstitucionesAsync(usuarioId, cancellationToken);
        var esSuperadministrador = rolesGlobales.Contains("platform_admin", StringComparer.Ordinal);
        var institucionesAdministrables = esSuperadministrador
            ? await ObtenerInstitucionesAdministrablesAsync(cancellationToken)
            : Array.Empty<InstitucionAcceso>();

        return new UsuarioActual(
            usuarioId,
            personaId,
            Array.AsReadOnly(roles),
            Array.AsReadOnly(permisos)
        )
        {
            NombreCompleto = nombreCompleto,
            AmbitoGlobal = new AmbitoGlobalAcceso(
                Array.AsReadOnly(rolesGlobales),
                Array.AsReadOnly(permisosGlobales)
            ),
            Instituciones = instituciones,
            InstitucionesAdministrables = institucionesAdministrables
        };
    }

    private async Task<IReadOnlyList<InstitucionAcceso>> ObtenerInstitucionesAsync(
        Guid usuarioId,
        CancellationToken cancellationToken
    )
    {
        await using var command = dataSource.CreateCommand("""
            select
              i.id,
              i.nombre,
              i.nombre_corto,
              coalesce(
                array_agg(distinct r.codigo order by r.codigo),
                '{}'::text[]
              ) as roles,
              coalesce(
                array_agg(distinct p.codigo order by p.codigo)
                  filter (where p.codigo is not null),
                '{}'::text[]
              ) as permisos
            from public.usuarios_roles ur
            join public.roles r
              on r.id = ur.rol_id
             and r.activo = true
            join public.instituciones i
              on i.id = ur.institucion_id
             and i.activo = true
            left join public.roles_permisos rp on rp.rol_id = r.id
            left join public.permisos p on p.id = rp.permiso_id
            where ur.usuario_id = $1
              and ur.institucion_id is not null
              and ur.activo = true
            group by i.id, i.nombre, i.nombre_corto
            order by lower(i.nombre), i.id
            """);
        command.Parameters.AddWithValue(usuarioId);

        var resultado = new List<InstitucionAcceso>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            resultado.Add(new InstitucionAcceso(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                Array.AsReadOnly(reader.GetFieldValue<string[]>(3)),
                Array.AsReadOnly(reader.GetFieldValue<string[]>(4))
            ));
        }

        return resultado.AsReadOnly();
    }

    private async Task<IReadOnlyList<InstitucionAcceso>> ObtenerInstitucionesAdministrablesAsync(
        CancellationToken cancellationToken
    )
    {
        await using var command = dataSource.CreateCommand("""
            select i.id, i.nombre, i.nombre_corto
            from public.instituciones i
            where i.activo = true
            order by lower(i.nombre), i.id
            """);

        var resultado = new List<InstitucionAcceso>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            resultado.Add(new InstitucionAcceso(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                Array.Empty<string>(),
                Array.Empty<string>()
            ));
        }

        return resultado.AsReadOnly();
    }
}
