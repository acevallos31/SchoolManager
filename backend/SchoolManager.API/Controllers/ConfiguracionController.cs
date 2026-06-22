using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/configuracion")]
[Authorize(Policy = "AdminOOperador")]
public sealed class ConfiguracionController : ControllerBase
{
    private static readonly Dictionary<string, string> Tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jornadas"] = "jornadas",
        ["niveles"] = "niveles",
        ["grados"] = "grados",
        ["secciones"] = "secciones",
        ["ciclos"] = "ciclos_escolares",
        ["tipos-plan-pago"] = "tipos_plan_pago"
    };

    private readonly SupabaseTableService _tableService;

    public ConfiguracionController(SupabaseTableService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet("{catalogo}")]
    public async Task<IActionResult> Get(string catalogo, CancellationToken cancellationToken)
    {
        if (!Tables.TryGetValue(catalogo, out var table))
        {
            return NotFound(new { error = "Catalogo no reconocido." });
        }

        try
        {
            var data = await _tableService.GetListAsync<Dictionary<string, object?>>(
                table,
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["order"] = "nombre.asc"
                },
                cancellationToken);

            return Ok(data);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("{catalogo}")]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> Create(string catalogo, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (!Tables.TryGetValue(catalogo, out var table))
        {
            return NotFound(new { error = "Catalogo no reconocido." });
        }

        var payload = ToSnakePayload(body);
        if (!payload.ContainsKey("nombre") || string.IsNullOrWhiteSpace(payload["nombre"]?.ToString()))
        {
            return BadRequest(new { error = "El nombre es obligatorio." });
        }

        payload["created_at"] = DateTimeOffset.UtcNow;
        payload["updated_at"] = DateTimeOffset.UtcNow;

        try
        {
            var item = await _tableService.InsertAsync<Dictionary<string, object?>>(table, payload, cancellationToken);
            return Ok(item);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPut("{catalogo}/{id:guid}")]
    [Authorize(Policy = "SoloAdmin")]
    public async Task<IActionResult> Update(string catalogo, Guid id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (!Tables.TryGetValue(catalogo, out var table))
        {
            return NotFound(new { error = "Catalogo no reconocido." });
        }

        var payload = ToSnakePayload(body);
        payload["updated_at"] = DateTimeOffset.UtcNow;

        try
        {
            var item = await _tableService.UpdateAsync<Dictionary<string, object?>>(table, id, payload, cancellationToken);
            return item is null ? NotFound(new { error = "Registro no encontrado." }) : Ok(item);
        }
        catch (SupabaseTableException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    private static Dictionary<string, object?> ToSnakePayload(JsonElement body)
    {
        var payload = new Dictionary<string, object?>();

        foreach (var property in body.EnumerateObject())
        {
            payload[ToSnakeCase(property.Name)] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number when property.Value.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when property.Value.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText()
            };
        }

        return payload;
    }

    private static string ToSnakeCase(string value)
    {
        var chars = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(current));
        }

        return new string(chars.ToArray());
    }
}
