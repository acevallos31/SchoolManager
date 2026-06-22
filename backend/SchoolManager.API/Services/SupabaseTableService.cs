using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SchoolManager.API.Services;

public sealed class SupabaseTableService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupabaseTableService> _logger;

    public SupabaseTableService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SupabaseTableService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<T>> GetListAsync<T>(
        string table,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
        where T : class
    {
        var url = BuildUrl(table, query);
        using var request = CreateRequest(HttpMethod.Get, url);
        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("GET", table, response.StatusCode, body);
            throw new SupabaseTableException("No se pudo consultar la informacion.", (int)response.StatusCode);
        }

        return JsonSerializer.Deserialize<List<T>>(body, JsonOptions) ?? [];
    }

    public async Task<T?> GetSingleAsync<T>(
        string table,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
        where T : class
    {
        var results = await GetListAsync<T>(table, query, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<T> InsertAsync<T>(
        string table,
        object payload,
        CancellationToken cancellationToken)
        where T : class
    {
        var url = BuildUrl(table, new Dictionary<string, string?> { ["select"] = "*" });
        using var request = CreateRequest(HttpMethod.Post, url, payload);
        request.Headers.Add("Prefer", "return=representation");

        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("POST", table, response.StatusCode, body);
            throw new SupabaseTableException(MapError(body, "No se pudo crear el registro."), (int)response.StatusCode);
        }

        var created = JsonSerializer.Deserialize<List<T>>(body, JsonOptions)?.FirstOrDefault();
        return created ?? throw new SupabaseTableException("La base de datos no devolvio el registro creado.", 502);
    }

    public async Task<T?> UpdateAsync<T>(
        string table,
        Guid id,
        object payload,
        CancellationToken cancellationToken)
        where T : class
    {
        var url = BuildUrl(table, new Dictionary<string, string?>
        {
            ["id"] = $"eq.{id}",
            ["select"] = "*"
        });
        using var request = CreateRequest(HttpMethod.Patch, url, payload);
        request.Headers.Add("Prefer", "return=representation");

        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("PATCH", table, response.StatusCode, body);
            throw new SupabaseTableException(MapError(body, "No se pudo actualizar el registro."), (int)response.StatusCode);
        }

        return JsonSerializer.Deserialize<List<T>>(body, JsonOptions)?.FirstOrDefault();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, object? payload = null)
    {
        var serviceRoleKey = GetConfiguredValue("Supabase:ServiceRoleKey", "Supabase:SecretKey")
            ?? throw new SupabaseTableException("Supabase__ServiceRoleKey no esta configurada en el backend.", 500);

        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);

        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        return await client.SendAsync(request, cancellationToken);
    }

    private string BuildUrl(string table, IReadOnlyDictionary<string, string?> query)
    {
        var supabaseUrl = GetConfiguredValue("Supabase:Url")?.TrimEnd('/')
            ?? throw new SupabaseTableException("Supabase__Url no esta configurada en el backend.", 500);

        var queryString = string.Join(
            "&",
            query
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}"));

        var url = $"{supabaseUrl}/rest/v1/{table}";
        return string.IsNullOrWhiteSpace(queryString) ? url : $"{url}?{queryString}";
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

    private void LogFailure(string method, string table, System.Net.HttpStatusCode statusCode, string body)
    {
        _logger.LogWarning(
            "Supabase table request failed. Method={Method} Table={Table} Status={StatusCode} Body={Body}",
            method,
            table,
            statusCode,
            body);
    }

    private static string MapError(string body, string fallback)
    {
        if (body.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || body.Contains("violates unique constraint", StringComparison.OrdinalIgnoreCase))
        {
            return "Ya existe un registro con esos datos.";
        }

        return fallback;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed class SupabaseTableException : Exception
{
    public SupabaseTableException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
