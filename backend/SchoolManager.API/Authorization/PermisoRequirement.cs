using Microsoft.AspNetCore.Authorization;

namespace SchoolManager.API.Authorization;

public sealed class PermisoRequirement(string permiso) : IAuthorizationRequirement
{
    public string Permiso { get; } = permiso;
}
