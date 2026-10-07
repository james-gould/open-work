using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OpenWork.Api.Data;
using OpenWork.Api.Endpoints;
using OpenWork.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

const string DevCorsPolicy = "DevCors";

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=openwork.db";

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<VapidKeyProvider>();
builder.Services.AddHostedService<NudgeBackgroundService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Ensure the sqlite file (and its parent directory) exist before the app starts serving traffic.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dataSource = connectionString.Split(';')
        .Select(part => part.Split('=', 2))
        .FirstOrDefault(kv => kv.Length == 2 && kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))?[1]
        .Trim();

    if (!string.IsNullOrEmpty(dataSource))
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGroup("/api/workitems").MapWorkItemEndpoints();
app.MapGroup("/api/push").MapPushEndpoints();

// Serve the React build (copied into wwwroot during the Docker build) and fall back to
// index.html for client-side routes that aren't API calls.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
