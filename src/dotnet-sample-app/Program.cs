using dotnet_sample_app.Models;
using Microsoft.OpenApi.Models;
using dotnet_sample_app.Infrastructure.Vault;

var builder = WebApplication.CreateBuilder(args);

// ✅ Load Vault secrets at startup (optional)
Dictionary<string, object>? vaultSecrets = null;
try
{
    // Attempt to load Vault secrets; if VAULT_* env vars are missing or Vault is unreachable
    // we continue startup with existing configuration (useful for tests and local dev).
    vaultSecrets = await VaultSecretService.GetSecretsAsync();
    if (vaultSecrets != null && vaultSecrets.Any())
    {
        builder.Configuration.AddInMemoryCollection(
            vaultSecrets.ToDictionary(
                x => x.Key,
                x => x.Value?.ToString()
            )
        );
    }
}
catch (Exception ex)
{
    // Do not crash the app on Vault errors during startup; log and continue.
    Console.WriteLine($"[WARN] Vault secrets not loaded: {ex.Message}");
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dotnet-sample-app", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ✅ Add endpoint to show current environment
app.MapGet("/env", () =>
{
    var env = app.Environment.EnvironmentName;
    return $"Application is running in {env} environment.";
});

// ✅ Add health check endpoint
app.MapGet("/health", () =>
{
    // Optional: Add more checks here (DB, external services, etc.)
    return Results.Ok(new { status = "UP" });
});

app.Run();

// Make Program class public for integration testing
public partial class Program { }
