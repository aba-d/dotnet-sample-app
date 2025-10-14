using dotnet_sample_app.Models;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dotnet-sample-app", Version = "v1" });
});

var app = builder.Build();

// ✅ Health check endpoint before HTTPS redirection
app.MapGet("/health", () => Results.Ok(new { status = "UP" }));

// Only apply HTTPS redirection for non-health requests
app.MapWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    branch =>
    {
        branch.UseHttpsRedirection();
    }
);

app.UseAuthorization();
app.MapControllers();

// Endpoint to show current environment
app.MapGet("/env", () => $"Application is running in {app.Environment.EnvironmentName} environment.");

app.Run();

// Make Program class public for integration testing
public partial class Program { }
