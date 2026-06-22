using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

=======
using SchoolManager.API.DTOs;


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

    // POST /api/pagos
    [HttpPost]
    public IActionResult RegistrarPago([FromBody] PagoCreateDto dto)
    {
        if (dto.MontoPagado <= 0)
            return BadRequest(new { error = "El monto del pago debe ser mayor a cero" });
        // TODO: Insertar pago y actualizar mensualidad a 'pagada'
        return Created("/api/pagos", new { mensaje = "Pago registrado", pago = dto });
    }

    // GET /api/pagos/{mensualidadId}
    [HttpGet("{mensualidadId:guid}")]
    public IActionResult GetPago(Guid mensualidadId)
    {
        // TODO: Obtener pago asociado a una mensualidad
        return Ok(new { mensaje = "Detalle del pago", mensualidadId });

    }
}
