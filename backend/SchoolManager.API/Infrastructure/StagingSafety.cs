using Npgsql;

namespace SchoolManager.API.Infrastructure;

public static class StagingSafety
{
    private static readonly HashSet<string> ForbiddenProductionHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "schoolmanager.vercel.app",
        "schoolmanager.nocpbx.com",
        "schoolmanager-xdxx.onrender.com",
        "pzhcpdznjoyukbhhodjz.supabase.co",
        "db.pzhcpdznjoyukbhhodjz.supabase.co"
    };

    private static readonly string[] DefaultAllowedHosts = ["localhost", "127.0.0.1", "::1"];

    public static void Validate(IConfiguration configuration, bool isStaging)
    {
        if (!isStaging)
        {
            return;
        }

        var allowedHosts = ResolveAllowedHosts(configuration);
        var jwtIssuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("[STAGING GUARDRAIL] Jwt:Issuer no está configurado para staging.");
        ValidateHttpUrl(jwtIssuer, "Jwt:Issuer", allowedHosts);

        var connectionString = configuration.GetConnectionString("PostgreSQL");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("[STAGING GUARDRAIL] ConnectionStrings:PostgreSQL no está configurado para staging.");
        }

        ValidatePostgreSql(connectionString, allowedHosts);

        foreach (var origin in configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        {
            ValidateHttpUrl(origin, "Cors:AllowedOrigins", allowedHosts);
        }
    }

    private static HashSet<string> ResolveAllowedHosts(IConfiguration configuration)
    {
        var hosts = new HashSet<string>(DefaultAllowedHosts, StringComparer.OrdinalIgnoreCase);
        var configured = configuration["E2E_ALLOWED_HOSTS"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            return hosts;
        }

        foreach (var host in configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            hosts.Add(host);
        }

        return hosts;
    }

    private static void ValidateHttpUrl(string rawUrl, string label, IReadOnlySet<string> allowedHosts)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"[STAGING GUARDRAIL] {label} debe ser una URL http/https válida.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException($"[STAGING GUARDRAIL] {label} no debe incluir credenciales en la URL.");
        }

        ValidateHost(uri.Host, label, allowedHosts);
    }

    private static void ValidatePostgreSql(string connectionString, IReadOnlySet<string> allowedHosts)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("[STAGING GUARDRAIL] ConnectionStrings:PostgreSQL no es válida.", exception);
        }

        if (string.IsNullOrWhiteSpace(builder.Host))
        {
            throw new InvalidOperationException("[STAGING GUARDRAIL] ConnectionStrings:PostgreSQL no contiene Host.");
        }

        ValidateHost(builder.Host, "ConnectionStrings:PostgreSQL", allowedHosts);
    }

    private static void ValidateHost(string host, string label, IReadOnlySet<string> allowedHosts)
    {
        if (ForbiddenProductionHosts.Contains(host))
        {
            throw new InvalidOperationException($"[STAGING GUARDRAIL] {label} apunta al host de producción prohibido '{host}'.");
        }

        if (!allowedHosts.Contains(host))
        {
            throw new InvalidOperationException(
                $"[STAGING GUARDRAIL] {label} apunta a '{host}', fuera de la allowlist de staging. "
                + "Define E2E_ALLOWED_HOSTS únicamente para hosts de staging controlados."
            );
        }
    }
}
