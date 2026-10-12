using System.Text.Json.Serialization;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required (PostgreSQL connection string).");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<VapidKeyProvider>();
builder.Services.AddHostedService<NudgeBackgroundService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddFido2(options =>
{
    options.RPID = builder.Configuration["Fido2:ServerDomain"]
        ?? throw new InvalidOperationException("Fido2:ServerDomain is required.");
    options.RPName = "Open Work";
    options.Origins = builder.Configuration.GetSection("Fido2:Origins").Get<string[]>() is { Length: > 0 } origins
        ? origins.ToHashSet()
        : throw new InvalidOperationException("Fido2:Origins is required.");
});

// Built directly from configuration (rather than resolved via DI) so the signing key is
// validated once at startup, and so AddJwtBearer below can use it immediately rather than
// deferring to an options-configure callback.
var jwtTokenService = new JwtTokenService(builder.Configuration);
builder.Services.AddSingleton(jwtTokenService);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = jwtTokenService.ValidationParameters);
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Apply any pending EF Core migrations against the PostgreSQL database on startup.
// Retries with backoff so a transiently unreachable database (e.g. a Postgres server that
// auto-paused/stopped and needs a moment to resume) doesn't crash-loop the whole container.
// If migrations still fail after retries, the app continues starting anyway so /healthz keeps
// responding; DB-backed requests will fail individually until the database is reachable again.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    const int maxAttempts = 5;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            startupLogger.LogWarning(ex, "Database migration failed on attempt {Attempt}/{MaxAttempts}; retrying in {Delay}.", attempt, maxAttempts, delay);
            Thread.Sleep(delay);
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Database migration failed after {MaxAttempts} attempts; starting anyway so health checks keep responding. API requests will fail until the database is reachable.", maxAttempts);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGroup("/api/auth").MapAuthEndpoints();
app.MapGroup("/api/workitems").MapWorkItemEndpoints().RequireAuthorization();
app.MapGroup("/api/push").MapPushEndpoints().RequireAuthorization();

// Serve the React build (copied into wwwroot during the Docker build) and fall back to
// index.html for client-side routes that aren't API calls.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

