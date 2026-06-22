using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SchoolManager.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<SupabaseTableService>();
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var jwtSecret = GetConfiguredValue(builder.Configuration, "Supabase:JwtSecret", "Jwt:Secret")
    ?? throw new InvalidOperationException(
        "JWT secret is not configured. Set Supabase__JwtSecret or Jwt__Secret in the environment."
    );

var jwtIssuer = GetConfiguredValue(builder.Configuration, "Jwt:Issuer");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(jwtIssuer),
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)
            ),
            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdmin", policy => policy.RequireRole("admin"));
    options.AddPolicy("AdminOPadre", policy => policy.RequireRole("admin", "padre"));
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

static string? GetConfiguredValue(IConfiguration configuration, params string[] keys)
{
    foreach (var key in keys)
    {
        var value = configuration[key];

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
