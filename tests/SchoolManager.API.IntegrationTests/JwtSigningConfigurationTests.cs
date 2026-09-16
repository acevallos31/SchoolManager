using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SchoolManager.API.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class JwtSigningConfigurationTests
{
    [Fact]
    public void Produccion_conserva_ES256_y_metadata_HTTPS()
    {
        var settings = JwtSigningConfiguration.Resolve(
            BuildConfiguration(),
            isStaging: false,
            "https://proyecto.supabase.co/auth/v1"
        );

        Assert.True(settings.RequireHttpsMetadata);
        Assert.Equal(new[] { SecurityAlgorithms.EcdsaSha256 }, settings.ValidAlgorithms);
        Assert.Null(settings.IssuerSigningKey);
    }

    [Fact]
    public void Staging_local_exige_secret_efimero()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            JwtSigningConfiguration.Resolve(
                BuildConfiguration(),
                isStaging: true,
                "http://127.0.0.1:54321/auth/v1"
            )
        );

        Assert.Contains("JWT_SECRET", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Staging_local_rechaza_secret_corto()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "demasiado-corto"
        });

        Assert.Throws<InvalidOperationException>(() =>
            JwtSigningConfiguration.Resolve(
                configuration,
                isStaging: true,
                "http://localhost:54321/auth/v1"
            )
        );
    }

    [Fact]
    public void Staging_local_usa_HS256_sin_relajar_produccion()
    {
        const string localSecret = "local-e2e-secret-with-at-least-32-bytes-long";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = localSecret
        });

        var settings = JwtSigningConfiguration.Resolve(
            configuration,
            isStaging: true,
            "http://127.0.0.1:54321/auth/v1"
        );

        Assert.False(settings.RequireHttpsMetadata);
        Assert.Equal(new[] { SecurityAlgorithms.HmacSha256 }, settings.ValidAlgorithms);
        var key = Assert.IsType<SymmetricSecurityKey>(settings.IssuerSigningKey);
        Assert.True(key.KeySize >= 256);
    }

    [Fact]
    public void Staging_remoto_HTTPS_conserva_ES256()
    {
        var settings = JwtSigningConfiguration.Resolve(
            BuildConfiguration(),
            isStaging: true,
            "https://auth.staging.example.test/auth/v1"
        );

        Assert.True(settings.RequireHttpsMetadata);
        Assert.Equal(new[] { SecurityAlgorithms.EcdsaSha256 }, settings.ValidAlgorithms);
        Assert.Null(settings.IssuerSigningKey);
    }

    [Fact]
    public void Issuer_invalido_falla_antes_de_configurar_JWT()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSigningConfiguration.Resolve(BuildConfiguration(), isStaging: true, "no-es-url")
        );
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?>? values = null
    ) => new ConfigurationBuilder()
        .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
        .Build();
}
