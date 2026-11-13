using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Aktivnosti;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Nova stavka nacrta.</summary>
/// <param name="SekcijaId">Sekcija.</param>
/// <param name="UredjajId">Uređaj iz registra.</param>
/// <param name="AktivnostId">Aktivnost iz kataloga.</param>
/// <param name="PoljaIds">Dodatna polja.</param>
public sealed record NovaStavkaInput(int SekcijaId, int UredjajId, int AktivnostId, IReadOnlyList<int>? PoljaIds);

/// <summary>Izmena stavke nacrta.</summary>
/// <param name="AktivnostId">Aktivnost.</param>
/// <param name="PoljaIds">Dodatna polja.</param>
public sealed record IzmenaStavkeInput(int AktivnostId, IReadOnlyList<int>? PoljaIds);

/// <summary>Pomeranje stavke.</summary>
/// <param name="Gore">Smer.</param>
public sealed record PomeriInput(bool Gore);

/// <summary>Objava.</summary>
/// <param name="VaziOd">Od kog dana važi.</param>
public sealed record ObjavaInput(DateOnly VaziOd);

/// <summary>Predlog izmene.</summary>
/// <param name="Tip">Šablon.</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="StavkaId">Stavka.</param>
/// <param name="Opis">Opis.</param>
/// <param name="Razlog">Razlog.</param>
public sealed record PredlogIzmeneInput(string Tip, string Vrsta, int? StavkaId, string Opis, string Razlog, int? PregledId = null);

/// <summary>Odluka o predlogu.</summary>
/// <param name="Odobren">Odluka.</param>
/// <param name="Odgovor">Obrazloženje.</param>
public sealed record OdlukaPredlogaInput(bool Odobren, string? Odgovor, int? DuplikatOd = null);

/// <summary>Tekst uputstva.</summary>
/// <param name="Tekst">Tekst.</param>
public sealed record UputstvoInput(string? Tekst);

/// <summary>
/// Izmene šablona u aplikaciji (R2.4, R2.6, R2.8–R2.10): nacrt (<c>sabloni.nacrt</c>), objava (<c>sabloni.objava</c>), predlozi (podnose svi,
/// odlučuju Direktor i Admin), istorijat i uputstva za aktivnosti.
/// </summary>
public static class IzmeneSablonaEndpoints
{
    /// <summary>Mapira rute.</summary>
    public static IEndpointRouteBuilder MapIzmeneSablonaEndpoints(this IEndpointRouteBuilder app)
    {
        var nacrt = PravoPolitika.Za(Prava.SabloniNacrt);
        var s = app.MapGroup("/api/v1/sabloni").WithTags("Šabloni").RequireAuthorization("RequireKorisnik");
        s.MapGet("/{tip}/verzije", async (string tip, IMediator m) => Results.Ok(await m.Send(new GetVerzijeQuery(tip)))).WithSummary("Sve verzije šablona");
        s.MapGet("/{tip}/nacrt", async (string tip, IMediator m) => await m.Send(new GetNacrtQuery(tip)) is { } n ? Results.Ok(n) : Results.NoContent())
            .RequireAuthorization(nacrt).WithSummary("Nacrt sa razlikama (204 ako ga nema)");
        s.MapPost("/{tip}/nacrt", async (string tip, IMediator m) => Results.Ok(await m.Send(new OtvoriNacrtCommand(tip))))
            .RequireAuthorization(nacrt).WithSummary("Otvori nacrt (kopija poslednje verzije)");
        s.MapDelete("/{tip}/nacrt", async (string tip, IMediator m) => { await m.Send(new OdbaciNacrtCommand(tip)); return Results.NoContent(); })
            .RequireAuthorization(nacrt).WithSummary("Odbaci nacrt");
        s.MapPost("/{tip}/nacrt/stavke", async (string tip, [FromBody] NovaStavkaInput i, IMediator m) =>
                Results.Ok(await m.Send(new DodajStavkuCommand(tip, i.SekcijaId, i.UredjajId, i.AktivnostId, i.PoljaIds))))
            .RequireAuthorization(nacrt).WithSummary("Dodaj stavku u nacrt");
        s.MapPut("/{tip}/nacrt/stavke/{id:int}", async (string tip, int id, [FromBody] IzmenaStavkeInput i, IMediator m) =>
                Results.Ok(await m.Send(new IzmeniStavkuCommand(tip, id, i.AktivnostId, i.PoljaIds))))
            .RequireAuthorization(nacrt).WithSummary("Izmeni aktivnost ili polja stavke");
        s.MapDelete("/{tip}/nacrt/stavke/{id:int}", async (string tip, int id, IMediator m) => Results.Ok(await m.Send(new IzbaciStavkuCommand(tip, id))))
            .RequireAuthorization(nacrt).WithSummary("Izbaci stavku iz nacrta");
        s.MapPost("/{tip}/nacrt/stavke/{id:int}/pomeri", async (string tip, int id, [FromBody] PomeriInput i, IMediator m) =>
                Results.Ok(await m.Send(new PomeriStavkuCommand(tip, id, i.Gore))))
            .RequireAuthorization(nacrt).WithSummary("Pomeri stavku gore ili dole (unutar sekcije)");
        s.MapPost("/{tip}/nacrt/objavi", async (string tip, [FromBody] ObjavaInput i, IMediator m) => Results.Ok(await m.Send(new ObjaviNacrtCommand(tip, i.VaziOd))))
            .RequireAuthorization(PravoPolitika.Za(Prava.SabloniObjava)).WithSummary("Objavi nacrt sa datumom od kog važi (Direktor)");
        s.MapGet("/istorijat", async ([FromQuery] string? tip, [FromQuery] string? vrsta, [FromQuery] string? korisnikId, [FromQuery] DateOnly? od,
                [FromQuery(Name = "do")] DateOnly? doDatuma, IMediator m) => Results.Ok(await m.Send(new GetIstorijatQuery(tip, vrsta, korisnikId, od, doDatuma))))
            .WithSummary("Istorijat šablona (filteri)");

        var p = app.MapGroup("/api/v1/predlozi-izmena").WithTags("Šabloni").RequireAuthorization("RequireKorisnik");
        p.MapGet("", async ([FromQuery] string? status, [FromQuery] string? tip, [FromQuery] bool? moji, IMediator m) => Results.Ok(await m.Send(new GetPredloziQuery(status, tip, moji ?? false))))
            .WithSummary("Predlozi izmena");
        p.MapPost("", async ([FromBody] PredlogIzmeneInput i, IMediator m) => Results.Ok(await m.Send(new PodnesiPredlogCommand(i.Tip, i.Vrsta, i.StavkaId, i.Opis, i.Razlog, i.PregledId))))
            .WithSummary("Predloži izmenu (razlog obavezan)");
        p.MapPost("/{id:int}/odluka", async (int id, [FromBody] OdlukaPredlogaInput i, IMediator m) => Results.Ok(await m.Send(new OdluciOPredloguCommand(id, i.Odobren, i.Odgovor, i.DuplikatOd))))
            .RequireAuthorization(nacrt).WithSummary("Odobri, odbij ili označi kao duplikat (Direktor, Admin)");
        p.MapPost("/{id:int}/slazem-se", async (int id, IMediator m) => Results.Ok(await m.Send(new SlazemSeSaPredlogomCommand(id))))
            .WithSummary("Slažem se sa predlogom (umesto slanja istog)");

        var a = app.MapGroup("/api/v1/aktivnosti/{id:int}/uputstvo").WithTags("Aktivnosti").RequireAuthorization("RequireKorisnik");
        a.MapGet("", async (int id, IMediator m) => Results.Ok(await m.Send(new GetUputstvoQuery(id)))).WithSummary("Uputstvo aktivnosti");
        a.MapPut("", async (int id, [FromBody] UputstvoInput i, IMediator m) => Results.Ok(await m.Send(new IzmeniUputstvoCommand(id, i.Tekst))))
            .RequireAuthorization(nacrt).WithSummary("Izmeni tekst uputstva");
        a.MapPost("/slike", async (int id, IFormFile? fajl, IMediator m) =>
            {
                if (fajl is null || fajl.Length == 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Izaberite sliku."] });
                using var ms = new MemoryStream();
                await fajl.CopyToAsync(ms);
                return Results.Ok(await m.Send(new DodajSlikuUputstvaCommand(id, ms.ToArray(), fajl.ContentType)));
            }).RequireAuthorization(nacrt).DisableAntiforgery().WithSummary("Dodaj sliku uputstvu (najviše 3, ≤ 3 MB)");
        a.MapDelete("/slike/{fajlId:long}", async (int id, long fajlId, IMediator m) => Results.Ok(await m.Send(new UkloniSlikuUputstvaCommand(id, fajlId))))
            .RequireAuthorization(nacrt).WithSummary("Ukloni sliku iz uputstva");
        a.MapGet("/slike/{fajlId:long}", async (int id, long fajlId, IMediator m) =>
            {
                var (sadrzaj, mime) = await m.Send(new GetSlikaUputstvaQuery(id, fajlId));
                return Results.Stream(sadrzaj, mime);
            }).WithSummary("Slika uputstva");
        return app;
    }
}
