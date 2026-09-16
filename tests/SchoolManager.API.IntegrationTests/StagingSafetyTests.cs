using Microsoft.Extensions.Configuration;
using SchoolManager.API.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class StagingSafetyTests
{
    [Fact]
    public void No_staging_no_aplica_guardrail()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "https://pzhcpdznjoyukbhhodjz.supabase.co/auth/v1",
            ["ConnectionStrings:PostgreSQL"] = "Host=db.pzhcpdznjoyukbhhodjz.supabase.co;Database=postgres;Username=postgres;Password=x"
        });

        StagingSafety.Validate(configuration, isStaging: false);
    }

    [Fact]
    public void Staging_rechaza_issuer_de_supabase_produccion()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "https://pzhcpdznjoyukbhhodjz.supabase.co/auth/v1",
            ["ConnectionStrings:PostgreSQL"] = LocalConnectionString,
            ["Cors:AllowedOrigins:0"] = "http://127.0.0.1:4200"
        });

        var error = Assert.Throws<InvalidOperationException>(
            () => StagingSafety.Validate(configuration, isStaging: true)
        );

        Assert.Contains("host de producción prohibido", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Staging_rechaza_postgres_de_produccion()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "http://127.0.0.1:54321/auth/v1",
            ["ConnectionStrings:PostgreSQL"] = "Host=db.pzhcpdznjoyukbhhodjz.supabase.co;Database=postgres;Username=postgres;Password=x",
            ["Cors:AllowedOrigins:0"] = "http://127.0.0.1:4200"
        });

        var error = Assert.Throws<InvalidOperationException>(
            () => StagingSafety.Validate(configuration, isStaging: true)
        );

        Assert.Contains("host de producción prohibido", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Staging_acepta_stack_local()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "http://127.0.0.1:54321/auth/v1",
            ["ConnectionStrings:PostgreSQL"] = LocalConnectionString,
            ["Cors:AllowedOrigins:0"] = "http://127.0.0.1:4200",
            ["Cors:AllowedOrigins:1"] = "http://localhost:4200"
        });

        StagingSafety.Validate(configuration, isStaging: true);
    }

    [Fact]
    public void Staging_acepta_host_controlado_agregado_a_allowlist()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "https://auth.staging.example.test/auth/v1",
            ["ConnectionStrings:PostgreSQL"] = "Host=db.staging.example.test;Database=postgres;Username=postgres;Password=x",
            ["Cors:AllowedOrigins:0"] = "https://app.staging.example.test",
            ["E2E_ALLOWED_HOSTS"] = "auth.staging.example.test,db.staging.example.test,app.staging.example.test"
        });

        StagingSafety.Validate(configuration, isStaging: true);
    }

    private const string LocalConnectionString =
        "Host=127.0.0.1;Port=54322;Database=postgres;Username=postgres;Password=postgres";

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
