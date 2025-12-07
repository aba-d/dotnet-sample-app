using dotnet_sample_app.Models;
using Microsoft.OpenApi.Models;
using dotnet_sample_app.Infrastructure.Vault;

var builder = WebApplication.CreateBuilder(args);

// ✅ Load Vault secrets at startup
var vaultSecrets = await VaultSecretService.GetSecretsAsync();

builder.Configuration.AddInMemoryCollection(
    vaultSecrets.ToDictionary(
        x => x.Key,
        x => x.Value?.ToString()
    )
);

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
