// Gör nödvändiga namespaces tillgängliga
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

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

// Lägger till HttpClient (används senare för väder-API)
builder.Services.AddHttpClient();

// Bygger webbappen
var app = builder.Build();

// Aktiverar CORS i pipeline
app.UseCors();

// Enkel test-endpoint: GET /api/ping -> { "ok": true }
app.MapGet("/api/ping", () => Results.Ok(new { ok = true }));


// Skapar en GET–endpoint på /api/forecast som tar emot ?city=...
app.MapGet("/api/forecast", async (string city, IHttpClientFactory factory) =>
{
    // Hämtar en HttpClient från fabriken som används för att göra HTTP-anrop
    var client = factory.CreateClient();

    // Skapar URL till Open-Meteo för att omvandla stad till lat/long
    var geoUrl =
        $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=sv&format=json";

    // Hämtar JSON-data från geocoding-URL och sparar som JsonElement
    var geoResponse = await client.GetFromJsonAsync<JsonElement>(geoUrl);

    // Kollar om resultat-listan finns och har minst 1 träff
    if (!geoResponse.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
    {
        // Skickar felmeddelande om staden inte hittas
        return Results.BadRequest(new { error = "Kunde inte hitta staden." });
    }

    // Tar första träffen från geocoding-resultatet
    var firstResult = results[0];

    // Läser ut latitud i korrekt format (punkt istället för komma)
    double lat = double.Parse(firstResult.GetProperty("latitude").ToString(), System.Globalization.CultureInfo.InvariantCulture);

    // Läser ut longitud i korrekt format
    double lon = double.Parse(firstResult.GetProperty("longitude").ToString(), System.Globalization.CultureInfo.InvariantCulture);

    // Hämtar landets namn (t.ex. Sverige)
    string country = firstResult.GetProperty("country").GetString() ?? "";

    // Skapar väder-URL med lat/long för att hämta nuvarande väder + 24h timprognos
    var weatherUrl =
        $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
        $"&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
        "&current_weather=true&hourly=temperature_2m,weathercode&timezone=auto";

    // Hämtar JSON från väder-URL
    var weatherResponse = await client.GetFromJsonAsync<JsonElement>(weatherUrl);

    // Tar ut nuvarande väderdel från JSON
    var current = weatherResponse.GetProperty("current_weather");

    // Tar ut tim-väderdelen från JSON
    var hourly = weatherResponse.GetProperty("hourly");

    // Bygger ett nytt objekt att skicka tillbaka till frontend
    var result = new
    {
        // Stadens namn som användaren skrev in
        city = city,

        // Landet som hittades via geocoding
        country = country,

        // Nuvarande temperatur i Celsius
        currentTemperature = current.GetProperty("temperature").GetDouble(),

        // Nuvarande väderkod (används för emojis)
        currentCode = current.GetProperty("weathercode").GetInt32(),

        // Bygger en lista med 24 timmar av timprognos
        hourly = Enumerable.Range(0, hourly.GetProperty("time").GetArrayLength())
            .Select(i => new
            {
                // Klockslag för timprognosen
                time = hourly.GetProperty("time")[i].GetString(),

                // Temperatur den timmen
                temperature = hourly.GetProperty("temperature_2m")[i].GetDouble(),

                // Väderkod för timmen (molnigt, sol, regn osv)
                code = hourly.GetProperty("weathercode")[i].GetInt32()
            })
            .Take(24) // Tar bara första 24 timmarna
    };

    // Skickar tillbaka all väderdata till frontend som JSON
    return Results.Ok(result);
});


// Startar webbappen
app.Run();
