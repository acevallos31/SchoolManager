using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.API.DTOs;

namespace SchoolManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Correo) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Correo y contrasena son obligatorios." });
        }

        var supabaseUrl = GetConfiguredValue("Supabase:Url")?.TrimEnd('/');
        var publishableKey = GetConfiguredValue("Supabase:PublishableKey", "Supabase:AnonKey");

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(publishableKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "La autenticacion no esta configurada en el backend."
            });
        }

        var client = _httpClientFactory.CreateClient();
        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{supabaseUrl}/auth/v1/token?grant_type=password");

        tokenRequest.Headers.Add("apikey", publishableKey);
        tokenRequest.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                email = request.Correo.Trim().ToLowerInvariant(),
                password = request.Password
            }),
            Encoding.UTF8,
            "application/json");

        using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            if (tokenResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                return Unauthorized(new { error = "Correo o contrasena incorrectos." });
            }

            _logger.LogWarning(
                "Supabase auth failed with status {StatusCode}: {Body}",
                tokenResponse.StatusCode,
                tokenBody);

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "No se pudo validar el acceso con el proveedor de autenticacion."
            });
        }

        var authSession = JsonSerializer.Deserialize<SupabaseAuthSession>(tokenBody, JsonOptions);

        if (authSession?.AccessToken is null || authSession.User?.Id is null)
        {
            _logger.LogWarning("Supabase auth response did not include a valid session: {Body}", tokenBody);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "El proveedor de autenticacion no devolvio una sesion valida."
            });
        }

        UsuarioActualDto? usuario;

        try
        {
            usuario = await GetUsuarioActual(
                client,
                supabaseUrl,
                publishableKey,
                authSession.AccessToken,
                authSession.User.Id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load usuario profile.");
            usuario = BuildBootstrapAdmin(authSession.User);

            if (usuario is not null)
            {
                return Ok(new LoginResponse(
                    authSession.AccessToken,
                    authSession.TokenType ?? "bearer",
                    authSession.ExpiresIn,
                    usuario));
            }

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "No se pudo consultar el perfil de usuario. Revisa Supabase__ServiceRoleKey y la tabla usuarios."
            });
        }

        if (usuario is null)
        {
            usuario = BuildBootstrapAdmin(authSession.User);

            if (usuario is not null)
            {
                return Ok(new LoginResponse(
                    authSession.AccessToken,
                    authSession.TokenType ?? "bearer",
                    authSession.ExpiresIn,
                    usuario));
            }

            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Tu cuenta existe, pero no esta registrada en SchoolManager."
            });
        }

        if (usuario.Rol is not ("admin" or "padre"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Tu usuario tiene un rol no reconocido."
            });
        }

        return Ok(new LoginResponse(
            authSession.AccessToken,
            authSession.TokenType ?? "bearer",
            authSession.ExpiresIn,
            usuario));
    }

    private UsuarioActualDto? BuildBootstrapAdmin(SupabaseAuthUser user)
    {
        var bootstrapAdminEmail = GetConfiguredValue("Auth:BootstrapAdminEmail")
            ?? "admin@schoolmanager.com";

        if (!string.Equals(user.Email, bootstrapAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new UsuarioActualDto(
            user.Id,
            user.Id,
            "Administrador",
            "Administrador",
            user.Email,
            "admin");
    }

    private async Task<UsuarioActualDto?> GetUsuarioActual(
        HttpClient client,
        string supabaseUrl,
        string publishableKey,
        string accessToken,
        string supabaseUid,
        CancellationToken cancellationToken)
    {
        var serviceRoleKey = GetConfiguredValue("Supabase:ServiceRoleKey", "Supabase:SecretKey");
        var bearerToken = serviceRoleKey ?? accessToken;
        var requestUrl =
            $"{supabaseUrl}/rest/v1/usuarios?select=id,supabase_uid,nombre_completo,correo,rol&supabase_uid=eq.{Uri.EscapeDataString(supabaseUid)}&limit=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("apikey", serviceRoleKey ?? publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Could not load usuario profile. Status {StatusCode}: {Body}",
                response.StatusCode,
                body);
            throw new InvalidOperationException("No se pudo consultar el perfil de usuario.");
        }

        var usuarios = JsonSerializer.Deserialize<List<SupabaseUsuario>>(body, JsonOptions);
        var usuario = usuarios?.FirstOrDefault();

        if (usuario is null)
        {
            return null;
        }

        return new UsuarioActualDto(
            usuario.Id,
            usuario.SupabaseUid,
            usuario.NombreCompleto ?? usuario.Correo ?? "Usuario",
            usuario.NombreCompleto,
            usuario.Correo,
            usuario.Rol);
    }

    private string? GetConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.Contains("REEMPLAZAR", StringComparison.OrdinalIgnoreCase)
                || value.Contains("TU-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("TU_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return value;
        }

        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SupabaseAuthSession(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] long ExpiresIn,
        SupabaseAuthUser? User);

    private sealed record SupabaseAuthUser(string Id, string? Email);

    private sealed record SupabaseUsuario(
        string Id,
        [property: JsonPropertyName("supabase_uid")] string SupabaseUid,
        [property: JsonPropertyName("nombre_completo")] string? NombreCompleto,
        string? Correo,
        string Rol);
}
