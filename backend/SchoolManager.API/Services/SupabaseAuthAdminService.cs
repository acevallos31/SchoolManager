using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolManager.API.Services;

public sealed class SupabaseAuthAdminService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupabaseAuthAdminService> _logger;

    public SupabaseAuthAdminService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SupabaseAuthAdminService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> CreateUserAsync(
        string email,
        string password,
        string fullName,
        string role,
        CancellationToken cancellationToken)
    {
        var supabaseUrl = GetConfiguredValue("Supabase:Url")?.TrimEnd('/')
            ?? throw new SupabaseAuthAdminException("Supabase__Url no esta configurada.", 500);
        var serviceRoleKey = GetConfiguredValue("Supabase:ServiceRoleKey", "Supabase:SecretKey")
            ?? throw new SupabaseAuthAdminException("Supabase__ServiceRoleKey no esta configurada.", 500);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/auth/v1/admin/users");
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                email = email.Trim().ToLowerInvariant(),
                password,
                email_confirm = true,
                user_metadata = new
                {
                    nombre_completo = fullName,
                    rol = role
                }
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Supabase auth admin create user failed. Status={StatusCode} Body={Body}", response.StatusCode, body);

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                && body.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                throw new SupabaseAuthAdminException("Ya existe un usuario de autenticacion con ese correo.", 409);
            }

            throw new SupabaseAuthAdminException("No se pudo crear el usuario de acceso en Supabase Auth.", (int)response.StatusCode);
        }

        var created = JsonSerializer.Deserialize<SupabaseAdminUser>(body, JsonOptions);
        if (string.IsNullOrWhiteSpace(created?.Id))
        {
            throw new SupabaseAuthAdminException("Supabase Auth no devolvio el id del usuario creado.", 502);
        }

        return created.Id;
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

    private sealed record SupabaseAdminUser([property: JsonPropertyName("id")] string Id);
}

public sealed class SupabaseAuthAdminException : Exception
{
    public SupabaseAuthAdminException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
