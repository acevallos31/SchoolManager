namespace SchoolManager.API.Identity;

/// <summary>
/// El token es válido, pero su identidad (claim <c>sub</c>) no está vinculada a
/// ningún registro de <c>public.usuarios</c>. Es el estado normal de un usuario
/// que acaba de autenticarse con Google antes de que un operador ejecute la
/// vinculación explícita (migración 027).
/// </summary>
public sealed class IdentidadNoVinculadaException(Guid authUserId)
    : Exception($"La identidad autenticada {authUserId} no está vinculada a ningún usuario.")
{
    public Guid AuthUserId { get; } = authUserId;
}

/// <summary>
/// La identidad sí está vinculada, pero el usuario está inactivo y por tanto
/// no puede operar en el sistema.
/// </summary>
public sealed class UsuarioInactivoException(Guid usuarioId)
    : Exception($"El usuario {usuarioId} está inactivo.")
{
    public Guid UsuarioId { get; } = usuarioId;
}

/// <summary>
/// La identidad sí está vinculada y el usuario está activo, pero el registro de
/// <c>public.personas</c> asociado no existe o no aporta los datos mínimos de
/// identidad. Es un estado de datos incompletos, NO una identidad sin vincular:
/// el operador debe reparar el perfil, no volver a vincular la identidad.
/// </summary>
public sealed class DatosUsuarioIncompletosException(Guid usuarioId)
    : Exception($"El usuario {usuarioId} no tiene un perfil de persona completo.")
{
    public Guid UsuarioId { get; } = usuarioId;
}
