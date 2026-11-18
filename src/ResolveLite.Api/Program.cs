using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ResolveLite.Api.Documents;
using ResolveLite.Infrastructure;
using ResolveLite.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

const long MaxRequestBytes = 2 * 1024 * 1024;   // hard backstop; UploadRules enforces the 1 MB business limit

// ── Services ────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();

// Enums travel as readable strings ("Pending"), not numbers (0).
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Antiforgery for form uploads. Angular's HttpClient sends the X-XSRF-TOKEN header automatically.
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    if (!builder.Environment.IsDevelopment())
    {
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;   // TLS ends at Azure ingress
    }
});

// Request size limits: never rely on server defaults (30 MB / 128 MB).
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxRequestBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxRequestBytes);

// Everything Azure: options, credential, BlobServiceClient, repository.
builder.Services.AddResolveLiteInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

// ── Pipeline ────────────────────────────────────────────────────────────────
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError
});
app.MapStaticAssets();
app.UseAntiforgery();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.MapGet("/api/info", (IHostEnvironment env) => TypedResults.Ok(new AppInfo(
    Name: "ResolveLite",
    Version: typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
    Environment: env.EnvironmentName)));

// Issues the antiforgery cookie pair. Angular calls this once at startup.
app.MapGet("/api/antiforgery/token", (IAntiforgery antiforgery, HttpContext context, IHostEnvironment env) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
    {
        HttpOnly = false,                    // Angular must be able to read it
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/"
    });
    return TypedResults.NoContent();
});

app.MapDocumentEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Shape of GET /api/info.</summary>
public sealed record AppInfo(string Name, string Version, string Environment);

// Makes Program visible to WebApplicationFactory<Program> in integration tests.
public partial class Program;
