using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlumnosController : ControllerBase
{
    private const string TableName = "alumnos";
    private readonly SupabaseTableService _tableService;
    private readonly SupabaseAuthAdminService _authAdminService;

    public AlumnosController(SupabaseTableService tableService, SupabaseAuthAdminService authAdminService)
    {
        _tableService = tableService;
        _authAdminService = authAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? buscar,
        [FromQuery] string? estado,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["select"] = "*",
            ["order"] = "nombres.asc"
        };

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query["estado"] = $"eq.{estado.Trim().ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var value = buscar.Trim();
            query["or"] = $"(nombres.ilike.*{value}*,apellidos.ilike.*{value}*,dni.ilike.*{value}*)";
        }

        try
        {
            var alumnos = await _tableService.GetListAsync<AlumnoDto>(TableName, query, cancellationToken);
            return Ok(alumnos);
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
            var alumno = await _tableService.GetSingleAsync<AlumnoDto>(
                TableName,
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["id"] = $"eq.{id}"
                },
                cancellationToken);

            return alumno is null ? NotFound(new { error = "Alumno no encontrado." }) : Ok(alumno);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOOperador")]
    public async Task<IActionResult> Create([FromBody] AlumnoCreateDto dto, CancellationToken cancellationToken)
    {
        var validationErrors = Validate(dto, isCreate: true);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new { errors = validationErrors });
        }

        try
        {
            var tutorId = await CreateAlumnoAccessUserAsync(dto, cancellationToken);
            var payload = ToPayload(dto, useDefaultEstado: true);

            if (tutorId.HasValue)
            {
                payload["tutor_id"] = tutorId.Value;
            }

            var alumno = await _tableService.InsertAsync<AlumnoDto>(TableName, payload, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = alumno.Id }, alumno);
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
    [Authorize(Policy = "AdminOOperador")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AlumnoUpdateDto dto, CancellationToken cancellationToken)
    {
        var validationErrors = Validate(dto, isCreate: false);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new { errors = validationErrors });
        }

        var payload = ToPayload(dto, useDefaultEstado: false);

        try
        {
            var alumno = await _tableService.UpdateAsync<AlumnoDto>(TableName, id, payload, cancellationToken);
            return alumno is null ? NotFound(new { error = "Alumno no encontrado." }) : Ok(alumno);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var alumno = await _tableService.UpdateAsync<AlumnoDto>(
                TableName,
                id,
                new
                {
                    estado = "inactivo",
                    updated_at = DateTimeOffset.UtcNow
                },
                cancellationToken);

            return alumno is null ? NotFound(new { error = "Alumno no encontrado." }) : Ok(alumno);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    private static List<string> Validate(AlumnoCreateDto dto, bool isCreate)
    {
        var errors = new List<string>();
        var nombres = FirstValue(dto.Nombres, dto.Nombre);
        var apellidos = FirstValue(dto.Apellidos, dto.Apellido);
        var dni = FirstValue(dto.Dni, dto.Identidad);

        if (isCreate || !string.IsNullOrWhiteSpace(nombres))
        {
            if (string.IsNullOrWhiteSpace(nombres))
            {
                errors.Add("Los nombres del alumno son obligatorios.");
            }
        }

        if (isCreate || !string.IsNullOrWhiteSpace(apellidos))
        {
            if (string.IsNullOrWhiteSpace(apellidos))
            {
                errors.Add("Los apellidos del alumno son obligatorios.");
            }
        }

        if (isCreate || !string.IsNullOrWhiteSpace(dni))
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                errors.Add("El DNI del alumno es obligatorio.");
            }
            else if (dni.Trim().Length < 8)
            {
                errors.Add("El DNI debe tener al menos 8 caracteres.");
            }
        }

        var fechaNacimiento = dto.FechaNacimiento ?? dto.FechaNacimientoSnake;

        if (isCreate || fechaNacimiento.HasValue)
        {
            if (!fechaNacimiento.HasValue)
            {
                errors.Add("La fecha de nacimiento del alumno es obligatoria.");
            }
            else if (fechaNacimiento.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                errors.Add("La fecha de nacimiento no puede estar en el futuro.");
            }
        }

        if (isCreate || !string.IsNullOrWhiteSpace(dto.Sexo))
        {
            var sexo = dto.Sexo?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(sexo))
            {
                errors.Add("El sexo del alumno es obligatorio.");
            }
            else if (sexo is not ("M" or "F" or "O"))
            {
                errors.Add("El sexo debe ser M, F u O.");
            }
        }

        if (isCreate && string.IsNullOrWhiteSpace(dto.PadresEncargados))
        {
            errors.Add("Debes registrar padres o encargados.");
        }

        if (isCreate && string.IsNullOrWhiteSpace(dto.Direccion))
        {
            errors.Add("La direccion del alumno es obligatoria.");
        }

        if (isCreate)
        {
            if (string.IsNullOrWhiteSpace(dto.CorreoAcceso) || !dto.CorreoAcceso.Contains('@'))
            {
                errors.Add("El correo de acceso del alumno no es valido.");
            }

            var passwordInicial = string.IsNullOrWhiteSpace(dto.PasswordAcceso)
                ? dni
                : dto.PasswordAcceso.Trim();

            if (string.IsNullOrWhiteSpace(passwordInicial) || passwordInicial.Length < 8)
            {
                errors.Add("La contrasena de acceso debe tener al menos 8 caracteres. Si la dejas vacia se usara el DNI.");
            }
        }

        return errors;
    }

    private async Task<Guid?> CreateAlumnoAccessUserAsync(AlumnoCreateDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.CorreoAcceso))
        {
            return null;
        }

        var nombres = FirstValue(dto.Nombres, dto.Nombre) ?? string.Empty;
        var apellidos = FirstValue(dto.Apellidos, dto.Apellido) ?? string.Empty;
        var nombreAlumno = $"{nombres} {apellidos}".Trim();
        var nombreUsuario = BuildNombreUsuarioAcceso(dto, nombres, nombreAlumno);

        var dni = FirstValue(dto.Dni, dto.Identidad) ?? string.Empty;
        var usuarioAcceso = string.IsNullOrWhiteSpace(dto.UsuarioAcceso)
            ? dni.Trim().ToLowerInvariant()
            : dto.UsuarioAcceso.Trim().ToLowerInvariant();
        var passwordAcceso = string.IsNullOrWhiteSpace(dto.PasswordAcceso)
            ? dni.Trim()
            : dto.PasswordAcceso.Trim();

        var authUserId = await _authAdminService.CreateUserAsync(
            dto.CorreoAcceso,
            passwordAcceso,
            nombreUsuario,
            "padre",
            cancellationToken);

        var usuario = await _tableService.InsertAsync<UsuarioDto>(
            "usuarios",
            new
            {
                id = Guid.Parse(authUserId),
                usuario = usuarioAcceso,
                nombre = nombreUsuario,
                nombre_completo = nombreUsuario,
                correo = dto.CorreoAcceso.Trim().ToLowerInvariant(),
                rol = "padre",
                supabase_uid = Guid.Parse(authUserId),
                created_at = DateTimeOffset.UtcNow,
                updated_at = DateTimeOffset.UtcNow
            },
            cancellationToken);

        return usuario.Id;
    }

    private static Dictionary<string, object?> ToPayload(AlumnoCreateDto dto, bool useDefaultEstado)
    {
        var payload = new Dictionary<string, object?>
        {
            ["updated_at"] = DateTimeOffset.UtcNow
        };

        AddIfHasValue(payload, "nombres", FirstValue(dto.Nombres, dto.Nombre));
        AddIfHasValue(payload, "apellidos", FirstValue(dto.Apellidos, dto.Apellido));
        AddIfHasValue(payload, "dni", FirstValue(dto.Dni, dto.Identidad));
        AddIfHasValue(payload, "sexo", dto.Sexo?.Trim().ToUpperInvariant());
        AddIfHasValue(payload, "padres_encargados", dto.PadresEncargados);
        AddIfHasValue(payload, "direccion", dto.Direccion);
        AddIfHasValue(payload, "correo_acceso", dto.CorreoAcceso?.Trim().ToLowerInvariant());

        var nombres = FirstValue(dto.Nombres, dto.Nombre);
        var apellidos = FirstValue(dto.Apellidos, dto.Apellido);
        var dni = FirstValue(dto.Dni, dto.Identidad);

        AddIfHasValue(payload, "usuario_acceso", string.IsNullOrWhiteSpace(dto.UsuarioAcceso) ? dni : dto.UsuarioAcceso);

        AddIfHasValue(payload, "nombre", $"{nombres} {apellidos}".Trim());
        AddIfHasValue(payload, "identidad", dni);
        AddIfHasValue(payload, "grado", "Sin asignar");

        var fechaNacimiento = dto.FechaNacimiento ?? dto.FechaNacimientoSnake;
        if (fechaNacimiento.HasValue)
        {
            payload["fecha_nacimiento"] = fechaNacimiento.Value;
        }

        if (useDefaultEstado || !string.IsNullOrWhiteSpace(dto.Estado))
        {
            payload["estado"] = string.IsNullOrWhiteSpace(dto.Estado)
                ? "activo"
                : dto.Estado.Trim().ToLowerInvariant();
        }

        return payload;
    }

    private static void AddIfHasValue(Dictionary<string, object?> payload, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            payload[key] = value.Trim();
        }
    }

    private static string? FirstValue(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string BuildNombreUsuarioAcceso(AlumnoCreateDto dto, string nombres, string nombreAlumno)
    {
        if (!string.IsNullOrWhiteSpace(dto.NombreUsuarioAcceso))
        {
            return dto.NombreUsuarioAcceso.Trim();
        }

        var primerNombre = nombres
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(primerNombre))
        {
            return $"Padre de {primerNombre}";
        }

        return string.IsNullOrWhiteSpace(dto.PadresEncargados)
            ? $"Padre de {nombreAlumno}".Trim()
            : dto.PadresEncargados.Trim();
    }
}
