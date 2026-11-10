using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Telo „pročitano“.</summary>
/// <param name="Ids">ID-jevi; prazno = sva.</param>
public sealed record ProcitanoInput(IReadOnlyList<long>? Ids);

/// <summary>Push pretplata iz <c>PushSubscription.toJSON()</c>.</summary>
/// <param name="Endpoint">Endpoint.</param>
/// <param name="P256dh">Ključ.</param>
/// <param name="Auth">Tajna.</param>
public sealed record PushPretplataInput(string Endpoint, string? P256dh, string? Auth);

/// <summary>Zvono, Web Push i primaoci obaveštenja (R9.1–R9.4).</summary>
public static class ObavestenjaEndpoints
{
    /// <summary>Mapira rute.</summary>
    public static IEndpointRouteBuilder MapObavestenjaEndpoints(this IEndpointRouteBuilder app)
    {
        var o = app.MapGroup("/api/v1/obavestenja").WithTags("Obaveštenja").RequireAuthorization("RequireKorisnik");
        o.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetZvonoQuery()))).WithSummary("Zvono: poslednja obaveštenja, nepročitana, čeka moju odluku");
        o.MapPost("/procitano", async ([FromBody] ProcitanoInput i, IMediator m) => Results.Ok(await m.Send(new OznaciProcitanoCommand(i.Ids))))
            .WithSummary("Označi pročitano (bez ID-jeva — sva)");
        o.MapPost("/probno", async (IMediator m) => { await m.Send(new PosaljiProbnoCommand()); return Results.NoContent(); })
            .WithSummary("Probno obaveštenje sebi (provera push-a)");

        var p = app.MapGroup("/api/v1/push").WithTags("Obaveštenja").RequireAuthorization("RequireKorisnik");
        p.MapGet("/kljuc", async (IMediator m) => Results.Ok(new { kljuc = await m.Send(new GetPushKljucQuery()) })).WithSummary("Javni VAPID ključ");
        p.MapPost("/pretplata", async ([FromBody] PushPretplataInput i, HttpRequest r, IMediator m) =>
            {
                await m.Send(new PrijaviPushCommand(i.Endpoint, i.P256dh ?? "", i.Auth ?? "", r.Headers.UserAgent.ToString()));
                return Results.NoContent();
            }).WithSummary("Prijava uređaja za push");
        p.MapDelete("/pretplata", async ([FromBody] PushPretplataInput i, IMediator m) => { await m.Send(new OdjaviPushCommand(i.Endpoint)); return Results.NoContent(); })
            .WithSummary("Odjava uređaja");

        var pr = app.MapGroup("/api/v1/podesavanja/primaoci").WithTags("Podešavanja").RequireAuthorization(PravoPolitika.Za(Prava.Podesavanja));
        pr.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetPrimaociQuery()))).WithSummary("Primaoci obaveštenja po vrsti");
        pr.MapPut("", async ([FromBody] Dictionary<string, List<string>> i, IMediator m) => Results.Ok(await m.Send(new PostaviPrimaoceCommand(i))))
            .WithSummary("Sačuvaj primaoce (navedene vrste)");
        return app;
    }
}
