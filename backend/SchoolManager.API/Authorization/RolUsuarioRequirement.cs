using Microsoft.AspNetCore.Authorization;

namespace SchoolManager.API.Authorization;

public sealed class RolUsuarioRequirement(params string[] rolesPermitidos) : IAuthorizationRequirement
{
    public IReadOnlySet<string> RolesPermitidos { get; } =
        rolesPermitidos.ToHashSet(StringComparer.Ordinal);
}
