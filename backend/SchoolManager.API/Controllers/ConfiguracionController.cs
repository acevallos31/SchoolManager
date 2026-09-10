using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using SchoolManager.API.Authorization;
using SchoolManager.API.Diagnostics;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[Route("api/configuracion")]
[Authorize]
public sealed class ConfiguracionController(
    NpgsqlDataSource dataSource,
    IAuthorizationService authorization,
    DebugModeState debugMode) : ApiControllerBase(dataSource)
{
    [HttpGet("contexto")]
    public Task<IActionResult> ObtenerContexto(CancellationToken ct) =>
        EjecutarAsync("select public.rpc_obtener_contexto_implementacion()", [], ct);

    [HttpPut("modo")]
    [Authorize(Policy = Permisos.Configuracion.EditarSistema)]
    public Task<IActionResult> ActualizarModo(ActualizarModoDto input, CancellationToken ct) =>
        EjecutarAsync("select public.rpc_actualizar_multiples_instituciones(@multi)",
            [new("multi", NpgsqlDbType.Boolean) { Value = input.MultiplesInstituciones!.Value }], ct);

    [HttpGet("debug")]
    [Authorize(Policy = Permisos.Configuracion.Debug)]
    public IActionResult ObtenerDebug() => Ok(new
    {
        habilitado = debugMode.IsEnabled,
        expiraEn = debugMode.EnabledUntil,
        requestId = HttpContext.TraceIdentifier
    });

    [HttpPut("debug")]
    [Authorize(Policy = Permisos.Configuracion.Debug)]
    public IActionResult ActualizarDebug(ActualizarDebugModeDto input)
    {
        if (!input.Habilitado.HasValue)
            return BadRequest(new { error = "Debe indicar si el modo debug se habilita o deshabilita." });

        if (!input.Habilitado.Value)
        {
            debugMode.Disable();
            return Ok(new { habilitado = false, expiraEn = (DateTimeOffset?)null, requestId = HttpContext.TraceIdentifier });
        }

        var minutos = input.Minutos ?? 60;
        if (minutos is < 5 or > 120)
            return BadRequest(new { error = "La duración del modo debug debe estar entre 5 y 120 minutos." });

        var expiraEn = debugMode.Enable(TimeSpan.FromMinutes(minutos));
        return Ok(new { habilitado = true, expiraEn, requestId = HttpContext.TraceIdentifier });
    }

    [HttpGet("institucion")]
    public async Task<IActionResult> ObtenerInstitucion([FromQuery] Guid? institucionId, CancellationToken ct)
    {
        var puedeVer = await authorization.AuthorizeAsync(User, Permisos.Configuracion.VerInstituciones);
        if (!puedeVer.Succeeded)
        {
            var puedeEditar = await authorization.AuthorizeAsync(User, Permisos.Configuracion.EditarInstituciones);
            if (!puedeEditar.Succeeded) return Forbid();
        }

        return await EjecutarAsync("select public.rpc_obtener_configuracion_institucion(@id)",
            [new("id", NpgsqlDbType.Uuid) { Value = (object?)institucionId ?? DBNull.Value }], ct);
    }

    [HttpPost("instituciones")]
    [Authorize(Policy = Permisos.Configuracion.EditarInstituciones)]
    public Task<IActionResult> CrearInstitucion(GuardarInstitucionDto input, CancellationToken ct) =>
        EjecutarAsync("select public.rpc_crear_institucion(" + ArgumentosInstitucion + ")",
            ParametrosInstitucion(input), ct);

    [HttpPut("instituciones/{id:guid}")]
    [Authorize(Policy = Permisos.Configuracion.EditarInstituciones)]
    public Task<IActionResult> ActualizarInstitucion(Guid id, GuardarInstitucionDto input, CancellationToken ct) =>
        EjecutarAsync("select public.rpc_actualizar_institucion(@id," + ArgumentosInstitucion + ")",
            [new("id", NpgsqlDbType.Uuid) { Value = id }, .. ParametrosInstitucion(input)], ct);

    private const string ArgumentosInstitucion =
        "@nombre,@corto,@direccion,@telefono,@correo,@logo,@rne,@civil,@codigo,@tipos";

    private static NpgsqlParameter[] ParametrosInstitucion(GuardarInstitucionDto input) =>
    [
        Texto("nombre", input.Nombre),
        Texto("corto", input.NombreCorto),
        Texto("direccion", input.Direccion),
        Texto("telefono", input.Telefono),
        Texto("correo", input.Correo),
        Texto("logo", input.LogoUrl),
        new("rne", NpgsqlDbType.Boolean) { Value = input.Identificadores.RneRequerido },
        new("civil", NpgsqlDbType.Boolean) { Value = input.Identificadores.IdentificacionCivilRequerida },
        new("codigo", NpgsqlDbType.Boolean) { Value = input.Identificadores.CodigoInternoRequerido },
        new("tipos", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = input.Identificadores.TiposIdentificacionPermitidos }
    ];

    private static NpgsqlParameter Texto(string nombre, string? valor) =>
        new(nombre, NpgsqlDbType.Text) { Value = (object?)valor ?? DBNull.Value };

    private async Task<IActionResult> EjecutarAsync(string sql, NpgsqlParameter[] parametros, CancellationToken ct)
    {
        try
        {
            await using var conexion = await AbrirComoUsuarioAsync(ct);
            await using var tx = await conexion.BeginTransactionAsync(ct);
            await FijarClaimAsync(conexion, tx, User.FindFirstValue("sub")!, ct);
            await using var comando = new NpgsqlCommand(sql, conexion, tx);
            comando.Parameters.AddRange(parametros);
            var json = (string)(await comando.ExecuteScalarAsync(ct))!;
            var resultado = JsonSerializer.Deserialize<JsonElement>(json);
            await tx.CommitAsync(ct);
            return Ok(resultado);
        }
        catch (PostgresException ex)
        {
            return ToError(ex, incluirCodigo: true);
        }
    }
}

public sealed class ActualizarDebugModeDto
{
    public bool? Habilitado { get; init; }
    public int? Minutos { get; init; }
}
