using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SchoolManager.API.Infrastructure;

public sealed record JwtSigningSettings(
    bool RequireHttpsMetadata,
    IReadOnlyList<string> ValidAlgorithms,
    SecurityKey? IssuerSigningKey
);

public static class JwtSigningConfiguration
{
    private const int LocalSupabasePort = 54321;

    public static JwtSigningSettings Resolve(
        IConfiguration configuration,
        bool isStaging,
        string issuer
    )
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
            || (issuerUri.Scheme != Uri.UriSchemeHttp && issuerUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Jwt:Issuer must be a valid http/https URL.");
        }

        var localSupabase = isStaging
            && issuerUri.Scheme == Uri.UriSchemeHttp
            && issuerUri.Port == LocalSupabasePort
            && IsLoopbackHost(issuerUri.Host);

        if (!localSupabase)
        {
            return new JwtSigningSettings(
                RequireHttpsMetadata: !isStaging || issuerUri.Scheme == Uri.UriSchemeHttps,
                ValidAlgorithms: [SecurityAlgorithms.EcdsaSha256],
                IssuerSigningKey: null
            );
        }

        var localSecret = configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(localSecret)
            || Encoding.UTF8.GetByteCount(localSecret) < 32)
        {
            throw new InvalidOperationException(
                "[STAGING GUARDRAIL] Supabase local requiere JWT_SECRET efímero de al menos 32 bytes."
            );
        }

        // S6781 presupone un secreto JWT persistente o distribuido. Aquí la clave
        // es efímera, generada por Supabase CLI, vive solo en .env.e2e.local ignorado
        // por Git y esta rama solo se alcanza en Staging + loopback :54321.
#pragma warning disable S6781
        var localSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(localSecret));
#pragma warning restore S6781

        return new JwtSigningSettings(
            RequireHttpsMetadata: false,
            ValidAlgorithms: [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey: localSigningKey
        );
    }

    private static bool IsLoopbackHost(string host) =>
        host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
