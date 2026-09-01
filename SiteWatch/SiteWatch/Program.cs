using Microsoft.Extensions.Diagnostics.HealthChecks;
using SiteWatch.Models;
using SiteWatch.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Diagnostics (Task 2.3) -------------------------------------------------
// builder.Logging.AddAzureWebAppDiagnostics();

// --- Telemetry (Task 2.4) ---------------------------------------------------
// Reads APPLICATIONINSIGHTS_CONNECTION_STRING from App Settings; no-ops locally.
// builder.Services.AddApplicationInsightsTelemetry();

// --- Configuration binding (Task 2.3) ---------------------------------------
builder.Services.Configure<MonitorOptions>(
    builder.Configuration.GetSection(MonitorOptions.SectionName));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddScoped<MonitorService>();
builder.Services.AddRazorPages();

// --- Health check (Task 2.3) ------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<StorageHealthCheck>("storage", HealthStatus.Degraded);

// --- CORS for the PHP status page (Task 2.4) --------------------------------
var allowedOrigin = builder.Configuration["SiteWatch:AllowedOrigin"];
builder.Services.AddCors(o => o.AddPolicy("statuspage", policy =>
{
    if (!string.IsNullOrWhiteSpace(allowedOrigin))
        policy.WithOrigins(allowedOrigin).AllowAnyHeader().AllowAnyMethod();
    else
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseCors("statuspage");

app.MapRazorPages();
app.MapHealthChecks("/healthz");
app.MapGet("/api/status", async (MonitorService monitor) =>
    Results.Json(await monitor.RunChecksAsync()));

app.Run();

// Health check: confirms the configured storage backend is actually reachable,
// rather than returning a hardcoded 200.
public class StorageHealthCheck : IHealthCheck
{
    private readonly HistoryStore _store;
    public StorageHealthCheck(HistoryStore store) => _store = store;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var ok = await _store.IsReachableAsync();
        return ok
            ? HealthCheckResult.Healthy(_store.UsingTableStorage
                ? "Azure Table Storage reachable"
                : "In-memory store active")
            : HealthCheckResult.Degraded("Table Storage unreachable");
    }
}