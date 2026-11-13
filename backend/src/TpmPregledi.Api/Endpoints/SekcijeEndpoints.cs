using TpmPregledi.Application.Features.Sekcije;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Telo nove sekcije.</summary>
/// <param name="Sifra">Šifra.</param>
/// <param name="Naziv">Naziv (opciono).</param>
public sealed record NovaSekcijaInput(string Sifra, string? Naziv);

/// <summary>Telo izmene sekcije.</summary>
/// <param name="Naziv">Naziv.</param>
/// <param name="Aktivna">Aktivna.</param>
public sealed record IzmenaSekcijeInput(string Naziv, bool Aktivna);

/// <summary>Telo redosleda.</summary>
/// <param name="Ids">ID-jevi sekcija redom.</param>
public sealed record RedosledSekcijaInput(IReadOnlyList<int> Ids);

/// <summary>Sekcije pogona (<c>/api/v1/sekcije</c>) — čitaju svi ulogovani (filteri, prikaz šablona), uređuje Admin (zamerka 33).</summary>
public static class SekcijeEndpoints
{
    /// <summary>Mapira <c>/api/v1/sekcije</c>.</summary>
    public static IEndpointRouteBuilder MapSekcijeEndpoints(this IEndpointRouteBuilder app)
    {
        var s = app.MapGroup("/api/v1/sekcije").WithTags("Sekcije").RequireAuthorization("RequireKorisnik");
        s.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetSekcijeQuery())))
            .WithSummary("Sve sekcije po redosledu (i neaktivne)");
        s.MapPost("", async ([FromBody] NovaSekcijaInput i, IMediator m) => Results.Ok(await m.Send(new NovaSekcijaCommand(i.Sifra, i.Naziv))))
            .RequireAuthorization("RequireAdmin").WithSummary("Nova sekcija (Admin)");
        s.MapPut("/{id:int}", async (int id, [FromBody] IzmenaSekcijeInput i, IMediator m) => Results.Ok(await m.Send(new IzmeniSekcijuCommand(id, i.Naziv, i.Aktivna))))
            .RequireAuthorization("RequireAdmin").WithSummary("Preimenovanje i aktivnost sekcije (Admin)");
        s.MapPut("/redosled", async ([FromBody] RedosledSekcijaInput i, IMediator m) => Results.Ok(await m.Send(new RedosledSekcijaCommand(i.Ids))))
            .RequireAuthorization("RequireAdmin").WithSummary("Redosled sekcija (Admin)");
        return app;
    }
}
