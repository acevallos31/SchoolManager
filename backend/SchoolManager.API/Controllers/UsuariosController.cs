using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = "SoloAdmin")]
public sealed class UsuariosController : ControllerBase
{
    private readonly SupabaseTableService _tableService;
    private readonly SupabaseAuthAdminService _authAdminService;

    public UsuariosController(SupabaseTableService tableService, SupabaseAuthAdminService authAdminService)
    {
        _tableService = tableService;
        _authAdminService = authAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? rol,
        [FromQuery] bool incluirInactivos = true,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["select"] = "*",
            ["order"] = "nombre.asc"
        };

        if (!string.IsNullOrWhiteSpace(rol))
        {
            query["rol"] = $"eq.{NormalizeRole(rol)}";
        }

        if (!incluirInactivos)
        {
            query["activo"] = "eq.true";
        }

        try
        {
            var usuarios = await _tableService.GetListAsync<UsuarioDto>("usuarios", query, cancellationToken);
            return Ok(usuarios);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _tableService.GetSingleAsync<UsuarioDto>(
                "usuarios",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{id}"
                },
                cancellationToken);

            return usuario is null ? NotFound(new { error = "Usuario no encontrado." }) : Ok(usuario);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UsuarioCreateDto dto, CancellationToken cancellationToken)
    {
        var errors = Validate(dto, isCreate: true);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var role = NormalizeRole(dto.Rol);
        var usuarioLogin = BuildUsuario(dto.Usuario, dto.Correo);

        try
        {
            var authUserId = await _authAdminService.CreateUserAsync(
                dto.Correo,
                dto.Password!,
                dto.Nombre,
                role,
                cancellationToken);

            var usuario = await _tableService.InsertAsync<UsuarioDto>(
                "usuarios",
                BuildPayload(Guid.Parse(authUserId), usuarioLogin, dto, role, includeCreatedAt: true),
                cancellationToken);

            return Ok(new
            {
                usuario,
                mensaje = role == "padre"
                    ? "Usuario de padre/encargado creado correctamente."
                    : "Usuario administrativo creado correctamente."
            });
        }
        catch (SupabaseAuthAdminException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UsuarioCreateDto dto, CancellationToken cancellationToken)
    {
        var errors = Validate(dto, isCreate: false);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        try
        {
            var actual = await _tableService.GetSingleAsync<UsuarioDto>(
                "usuarios",
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{id}"
                },
                cancellationToken);

            if (actual is null)
            {
                return NotFound(new { error = "Usuario no encontrado." });
            }

            var role = NormalizeRole(dto.Rol);
            var usuarioLogin = BuildUsuario(dto.Usuario, dto.Correo);

            if (actual.SupabaseUid.HasValue)
            {
                await _authAdminService.UpdateUserAsync(
                    actual.SupabaseUid.Value.ToString(),
                    dto.Correo,
                    dto.Password,
                    dto.Nombre,
                    role,
                    cancellationToken);
            }

            var usuario = await _tableService.UpdateAsync<UsuarioDto>(
                "usuarios",
                id,
                BuildPayload(null, usuarioLogin, dto, role, includeCreatedAt: false),
                cancellationToken);

            return usuario is null ? NotFound(new { error = "Usuario no encontrado." }) : Ok(usuario);
        }
        catch (SupabaseAuthAdminException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _tableService.UpdateAsync<UsuarioDto>(
                "usuarios",
                id,
                new
                {
                    activo = false,
                    updated_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            return usuario is null ? NotFound(new { error = "Usuario no encontrado." }) : Ok(usuario);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/activar")]
    public async Task<IActionResult> Activar(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _tableService.UpdateAsync<UsuarioDto>(
                "usuarios",
                id,
                new
                {
                    activo = true,
                    updated_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            return usuario is null ? NotFound(new { error = "Usuario no encontrado." }) : Ok(usuario);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    private static Dictionary<string, object?> BuildPayload(
        Guid? authUserId,
        string usuarioLogin,
        UsuarioCreateDto dto,
        string role,
        bool includeCreatedAt)
    {
        var payload = new Dictionary<string, object?>
        {
            ["usuario"] = usuarioLogin,
            ["nombre"] = dto.Nombre.Trim(),
            ["nombre_completo"] = dto.Nombre.Trim(),
            ["correo"] = dto.Correo.Trim().ToLowerInvariant(),
            ["rol"] = role,
            ["activo"] = dto.Activo,
            ["updated_at"] = DateTimeOffset.UtcNow
        };

        if (authUserId.HasValue)
        {
            payload["id"] = authUserId.Value;
            payload["supabase_uid"] = authUserId.Value;
        }

        if (includeCreatedAt)
        {
            payload["created_at"] = DateTimeOffset.UtcNow;
        }

        return payload;
    }

    private static List<string> Validate(UsuarioCreateDto dto, bool isCreate)
    {
        var errors = new List<string>();
        var role = NormalizeRole(dto.Rol);

        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            errors.Add("El nombre del usuario es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(dto.Correo) || !dto.Correo.Contains('@'))
        {
            errors.Add("El correo del usuario no es valido.");
        }

        if (isCreate && (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8))
        {
            errors.Add("La contrasena debe tener al menos 8 caracteres.");
        }

        if (!isCreate && !string.IsNullOrWhiteSpace(dto.Password) && dto.Password.Length < 8)
        {
            errors.Add("La nueva contrasena debe tener al menos 8 caracteres.");
        }

        if (role is not ("admin" or "operador" or "padre"))
        {
            errors.Add("El rol debe ser admin, operador o padre.");
        }

        return errors;
    }

    private static string BuildUsuario(string? usuario, string correo)
    {
        return string.IsNullOrWhiteSpace(usuario)
            ? correo.Split('@')[0].Trim().ToLowerInvariant()
            : usuario.Trim().ToLowerInvariant();
    }

    private static string NormalizeRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "administrador" => "admin",
            "operator" => "operador",
            "alumno" or "alumno_padre" or "padre_familia" => "padre",
            var value => value ?? string.Empty
        };
    }
}
