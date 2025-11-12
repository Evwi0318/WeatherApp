// Gör nödvändiga namespaces tillgängliga
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

// Skapar en builder för webbappen
var builder = WebApplication.CreateBuilder(args);

// Lägger till CORS så frontend får prata med backend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Lägger till HttpClient (vi använder den senare för väder-API)
builder.Services.AddHttpClient();

// Bygger webbappen
var app = builder.Build();

// Aktiverar CORS i pipeline
app.UseCors();

// Enkel test-endpoint: GET /api/ping -> { "ok": true }
app.MapGet("/api/ping", () => Results.Ok(new { ok = true }));

// Startar webbappen
app.Run();
