using BpTracker.Api.Data;
using BpTracker.Api.Endpoints;
using BpTracker.Api.Services;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

builder.Services.AddScoped<IMeasurementService, MeasurementService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();

builder.Services.Configure<GeminiSettings>(options =>
{
    options.ApiKey = builder.Configuration["GEMINI_API_KEY"] ?? string.Empty;
    var modelEnv = builder.Configuration["GEMINI_MODEL"];
    options.Model = string.IsNullOrWhiteSpace(modelEnv) ? "gemini-flash-latest" : modelEnv;
});
builder.Services.AddHttpClient<IGeminiService, GeminiService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.Services.Configure<GoogleSheetsSettings>(options =>
{
    options.ScriptUrl = builder.Configuration["GOOGLE_SCRIPT_URL"] ?? string.Empty;
});



var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("BP Tracker API")
            .WithTheme(ScalarTheme.Moon)
            .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch);
    });
}

// Configure the HTTP request pipeline.
app.UseCors();

app.MapMeasurementEndpoints();
app.MapSchemaEndpoints();
app.MapSyncEndpoints();
app.MapAnalyzeEndpoints();

// Automatically apply migrations on startup with retry logic
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<AppDbContext>();

    for (int i = 0; i < 10; i++)
    {
        try
        {
            db.Database.Migrate();
            logger.LogInformation("Database migrated successfully.");
            break;
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or NpgsqlException)
        {
            logger.LogWarning("Database is not ready yet. Retrying in 2 seconds... (Attempt {Attempt})", i + 1);
            Thread.Sleep(2000);
            if (i == 9) throw;
        }
    }
}

app.Run();
