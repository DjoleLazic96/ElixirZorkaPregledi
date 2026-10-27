using TpmPregledi.Api.Common;
using TpmPregledi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// Health endpointi — anonimni i izuzeti iz rate limitera (Docker healthcheck i Traefik ih zovu
/// stalno sa loopback-a). Ne otkrivaju verziju ni detalje greške (samo tip izuzetka).
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Mapira <c>/api/v1/health</c>, <c>/health/live</c>, <c>/health/ready</c>.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/health")
            .WithTags("System")
            .AllowAnonymous()
            .DisableRateLimiting();

        group.MapGet("", () => Results.Ok(new { status = "ok" }))
             .WithSummary("Health check za Traefik/IT monitoring");

        group.MapGet("/live", () => Results.Ok(new { status = "live" }))
             .WithSummary("Liveness — proces odgovara (ne dira bazu)");

        // Readiness proverava i bazu — Docker healthcheck i deploy smoke test MORAJU ovaj endpoint,
        // jer bi /live bio „zdrav“ i kad je baza nedostupna.
        group.MapGet("/ready", async (AppDbContext db, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                return Results.Ok(new { status = "ready" });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title:      "Baza nedostupna",
                    detail:     ex.GetType().Name,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?> { ["traceId"] = TraceId.Trenutni(http) });
            }
        })
        .WithSummary("Readiness — proverava vezu sa bazom (503 ako ne radi)");

        return app;
    }
}
