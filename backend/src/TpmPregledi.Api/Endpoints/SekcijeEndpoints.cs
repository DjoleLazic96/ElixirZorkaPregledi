using TpmPregledi.Application.Features.Sekcije;
using MediatR;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Sekcije pogona (<c>/api/v1/sekcije</c>) — čitaju svi ulogovani (filteri, prikaz šablona).</summary>
public static class SekcijeEndpoints
{
    /// <summary>Mapira <c>/api/v1/sekcije</c>.</summary>
    public static IEndpointRouteBuilder MapSekcijeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/sekcije", async (IMediator m) => Results.Ok(await m.Send(new GetSekcijeQuery())))
            .WithTags("Sekcije")
            .RequireAuthorization("RequireKorisnik")
            .WithSummary("Sve sekcije po redosledu sa obrazaca");
        return app;
    }
}
