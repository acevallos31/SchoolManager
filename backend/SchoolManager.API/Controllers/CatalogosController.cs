using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;
using SchoolManager.API.Services;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CatalogosController : ControllerBase
{
    private readonly SupabaseTableService _tableService;

    public CatalogosController(SupabaseTableService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet("grados")]
    public Task<IActionResult> GetGrados(CancellationToken cancellationToken)
    {
        return GetCatalogo("grados", cancellationToken);
    }

    [HttpGet("secciones")]
    public Task<IActionResult> GetSecciones(CancellationToken cancellationToken)
    {
        return GetCatalogo("secciones", cancellationToken);
    }

    [HttpGet("jornadas")]
    public Task<IActionResult> GetJornadas(CancellationToken cancellationToken)
    {
        return GetCatalogo("jornadas", cancellationToken);
    }

    [HttpGet("niveles")]
    public Task<IActionResult> GetNiveles(CancellationToken cancellationToken)
    {
        return GetCatalogo("niveles", cancellationToken);
    }

    [HttpGet("ciclos")]
    public Task<IActionResult> GetCiclos(CancellationToken cancellationToken)
    {
        return GetCatalogo("ciclos_escolares", cancellationToken);
    }

    [HttpGet("planes-pago")]
    public Task<IActionResult> GetPlanesPago(CancellationToken cancellationToken)
    {
        return GetCatalogo("planes_pago", cancellationToken);
    }

    private async Task<IActionResult> GetCatalogo(string table, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _tableService.GetListAsync<CatalogoDto>(
                table,
                new Dictionary<string, string?>
                {
                    ["select"] = "*",
                    ["activo"] = "eq.true",
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
}
