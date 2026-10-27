using TpmPregledi.Application.Features.ServerStanje.Queries;
using MediatR;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// <c>GET /api/v1/stanje-servera</c> (Admin): jedan poziv vraća ceo snapshot (aplikacija, baza, host).
/// Očekivano trajanje do ~700 ms — CPU se meri 500 ms.
/// </summary>
public static class ServerStanjeEndpoints
{
    /// <summary>Mapira <c>/api/v1/stanje-servera</c>.</summary>
    public static IEndpointRouteBuilder MapServerStanjeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/v1/stanje-servera")
            .WithTags("Stanje servera")
            .RequireAuthorization("RequireAdmin")
            .MapGet("", async (IMediator m, CancellationToken ct) =>
                Results.Ok(await m.Send(new GetServerStanjeQuery(), ct)))
            .WithSummary("Snapshot stanja servera: aplikacija, baza, host");

        return app;
    }
}
