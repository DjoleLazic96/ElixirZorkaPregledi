using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Podešavanja aplikacije (<c>/api/v1/podesavanja</c>, pravo <c>podesavanja</c>).</summary>
public static class PodesavanjaEndpoints
{
    /// <summary>Mapira <c>/api/v1/podesavanja</c>.</summary>
    public static IEndpointRouteBuilder MapPodesavanjaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/podesavanja")
            .WithTags("Podešavanja")
            .RequireAuthorization(PravoPolitika.Za(Prava.Podesavanja));

        group.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetPodesavanjaQuery())))
            .WithSummary("Trenutna podešavanja");

        group.MapPut("", async ([FromBody] PodesavanjaDto input, IMediator m) =>
                Results.Ok(await m.Send(new IzmeniPodesavanjaCommand(input))))
            .WithSummary("Izmeni sva podešavanja");

        return app;
    }
}
