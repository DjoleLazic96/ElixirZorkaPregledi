using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Version.Dtos;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// <c>GET /api/v1/version</c> — anonimno i bez rate limita: deploy smoke test proverava verziju
/// odmah posle <c>docker compose up</c>, pre nego što postoji ijedan token; UI „O aplikaciji“ i
/// login footer je prikazuju.
/// </summary>
public static class VersionEndpoints
{
    /// <summary>Mapira <c>/api/v1/version</c>.</summary>
    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/version", (IAppVersion v, IHostEnvironment env) =>
                Results.Ok(new VerzijaDto(v.Verzija, v.GitSha, v.BuildUtc, env.EnvironmentName)))
           .AllowAnonymous()
           .DisableRateLimiting()
           .WithTags("System")
           .WithSummary("Verzija, git SHA, vreme build-a i okruženje");

        return app;
    }
}
