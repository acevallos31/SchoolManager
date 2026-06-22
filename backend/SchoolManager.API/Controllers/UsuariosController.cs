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
    public async Task<IActionResult> GetAll([FromQuery] string? rol, CancellationToken cancellationToken)
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UsuarioCreateDto dto, CancellationToken cancellationToken)
    {
        var errors = Validate(dto);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var role = NormalizeRole(dto.Rol);
        try
        {
            var authUserId = await _authAdminService.CreateUserAsync(
                dto.Correo,
                dto.Password,
                dto.Nombre,
                role,
                cancellationToken);

            var usuario = await _tableService.InsertAsync<UsuarioDto>(
                "usuarios",
                new
                {
                    id = Guid.Parse(authUserId),
                    usuario = string.IsNullOrWhiteSpace(dto.Usuario)
                        ? dto.Correo.Split('@')[0].Trim().ToLowerInvariant()
                        : dto.Usuario.Trim().ToLowerInvariant(),
                    nombre = dto.Nombre.Trim(),
                    nombre_completo = dto.Nombre.Trim(),
                    correo = dto.Correo.Trim().ToLowerInvariant(),
                    rol = role,
                    supabase_uid = Guid.Parse(authUserId),
                    created_at = DateTimeOffset.UtcNow,
                    updated_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            return Ok(new
            {
                usuario,
                mensaje = role == "padre"
                    ? "Usuario de alumno/padre creado correctamente."
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

    private static List<string> Validate(UsuarioCreateDto dto)
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

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
        {
            errors.Add("La contrasena debe tener al menos 8 caracteres.");
        }

        if (role is not ("admin" or "operador" or "padre"))
        {
            errors.Add("El rol debe ser admin, operador o padre.");
        }

        return errors;
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
