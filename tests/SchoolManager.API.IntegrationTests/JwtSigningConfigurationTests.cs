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
            isStaging: false,
            "https://proyecto.supabase.co/auth/v1"
        );

        Assert.True(settings.RequireHttpsMetadata);
        Assert.Equal(new[] { SecurityAlgorithms.EcdsaSha256 }, settings.ValidAlgorithms);
        Assert.Null(settings.IssuerSigningKey);
    }

    [Theory]
    [InlineData("http://127.0.0.1:54321/auth/v1")]
    [InlineData("http://localhost:54321/auth/v1")]
    public void Staging_local_usa_ES256_y_JWKS_sin_secret(string issuer)
    {
        var settings = JwtSigningConfiguration.Resolve(isStaging: true, issuer);

        Assert.False(settings.RequireHttpsMetadata);
        Assert.Equal(new[] { SecurityAlgorithms.EcdsaSha256 }, settings.ValidAlgorithms);
        Assert.Null(settings.IssuerSigningKey);
    }

    [Fact]
    public void Staging_remoto_HTTPS_conserva_ES256()
    {
        var settings = JwtSigningConfiguration.Resolve(
            isStaging: true,
            "https://auth.staging.example.test/auth/v1"
        );

        Assert.True(settings.RequireHttpsMetadata);
        Assert.Equal(new[] { SecurityAlgorithms.EcdsaSha256 }, settings.ValidAlgorithms);
        Assert.Null(settings.IssuerSigningKey);
    }

    [Theory]
    [InlineData(false, "http://127.0.0.1:54321/auth/v1")]
    [InlineData(true, "http://auth.staging.example.test:54321/auth/v1")]
    [InlineData(true, "http://127.0.0.1:54322/auth/v1")]
    public void HTTP_fuera_del_loopback_staging_esperado_se_rechaza(bool isStaging, string issuer)
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSigningConfiguration.Resolve(isStaging, issuer)
        );
    }

    [Theory]
    [InlineData("no-es-url")]
    [InlineData("ftp://127.0.0.1:54321/auth/v1")]
    public void Issuer_invalido_falla_antes_de_configurar_JWT(string issuer)
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSigningConfiguration.Resolve(isStaging: true, issuer)
        );
    }
}
