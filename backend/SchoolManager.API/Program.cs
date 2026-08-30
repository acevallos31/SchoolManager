using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SchoolManager.API.Authorization;
using SchoolManager.API.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? new[]
    {
        "http://localhost:4200",
        "https://schoolmanager.vercel.app"
    };
var explicitAllowedOrigins = allowedOrigins
    .Select(origin => origin.TrimEnd('/'))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
var vercelPreviewHostPrefix = builder.Configuration["Cors:VercelPreviewHostPrefix"]
    ?? "school-manager-";
var vercelPreviewHostSuffix = builder.Configuration["Cors:VercelPreviewHostSuffix"]
    ?? "-acevallos31s-projects.vercel.app";

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                  IsAllowedFrontendOrigin(
                      origin,
                      explicitAllowedOrigins,
                      vercelPreviewHostPrefix,
                      vercelPreviewHostSuffix
                  ))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = jwtIssuer;
        options.MetadataAddress = $"{jwtIssuer.TrimEnd('/')}/.well-known/openid-configuration";
        options.Audience = jwtAudience;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            NameClaimType = "sub"
        };
    });

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("PostgreSQL");

    if (string.IsNullOrWhiteSpace(connectionString)
        || connectionString.Contains("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "PostgreSQL connection is not configured. Set ConnectionStrings__PostgreSQL."
        );
    }

    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
builder.Services.AddScoped<IAuthorizationHandler, RolUsuarioHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new RolUsuarioRequirement("admin"));
    });
    options.AddPolicy("AdminOPadre", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new RolUsuarioRequirement("admin", "padre"));
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "SchoolManager.API",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapControllers();

app.Run();

static bool IsAllowedFrontendOrigin(
    string origin,
    IReadOnlySet<string> explicitAllowedOrigins,
    string vercelPreviewHostPrefix,
    string vercelPreviewHostSuffix
)
{
    if (explicitAllowedOrigins.Contains(origin.TrimEnd('/')))
    {
        return true;
    }

    return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && uri.Host.StartsWith(vercelPreviewHostPrefix, StringComparison.OrdinalIgnoreCase)
        && uri.Host.EndsWith(vercelPreviewHostSuffix, StringComparison.OrdinalIgnoreCase)
        && uri.Host.Length > vercelPreviewHostPrefix.Length + vercelPreviewHostSuffix.Length;
}

public partial class Program;
