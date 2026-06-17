using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    // GET api/pagos?mensualidadId={id}
    [HttpGet]
    public IActionResult GetAll([FromQuery] Guid? mensualidadId)
    {
        // TODO: listar pagos (admin: todos, padre: solo de sus hijos)
        return Ok(new List<object>());
    }

    // GET api/pagos/{id}
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        // TODO: obtener pago por id
        return Ok();
    }

    // POST api/pagos
    // Registra un pago aplicado a una mensualidad (el trigger de la BD actualiza el estado)
    [HttpPost]
    [Authorize(Policy = "SoloAdmin")]
    public IActionResult Create([FromBody] object dto)
    {
        // TODO: insertar pago en la base de datos
        return CreatedAtAction(nameof(GetById), new { id = Guid.NewGuid() }, dto);
    }
}
