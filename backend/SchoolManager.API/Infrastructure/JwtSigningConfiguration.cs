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
        bool isStaging,
        string issuer
    )
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
            || (issuerUri.Scheme != Uri.UriSchemeHttp && issuerUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Jwt:Issuer must be a valid http/https URL.");
        }

        var localSupabaseHttp = isStaging
            && issuerUri.Scheme == Uri.UriSchemeHttp
            && issuerUri.Port == LocalSupabasePort
            && IsLoopbackHost(issuerUri.Host);

        if (issuerUri.Scheme == Uri.UriSchemeHttp && !localSupabaseHttp)
        {
            throw new InvalidOperationException(
                "Jwt:Issuer solo puede usar HTTP en Staging contra Supabase loopback :54321."
            );
        }

        // Supabase CLI >= 2.71.1 firma las sesiones de usuario con ES256 por
        // defecto, igual que producción. El staging local confía en el JWKS de
        // GoTrue y solo permite metadata HTTP porque el issuer está limitado a
        // loopback :54321. No se reutiliza JWT_SECRET ni se habilita HS256.
        return new JwtSigningSettings(
            RequireHttpsMetadata: !localSupabaseHttp,
            ValidAlgorithms: [SecurityAlgorithms.EcdsaSha256],
            IssuerSigningKey: null
        );
    }

    private static bool IsLoopbackHost(string host) =>
        host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
