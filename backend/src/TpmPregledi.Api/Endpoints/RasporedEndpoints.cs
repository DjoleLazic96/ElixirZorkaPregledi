using System.Security.Claims;
using TpmPregledi.Api.Common;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Telo „Promeni u rasporedu“.</summary>
/// <param name="InzenjerId">Novi inženjer.</param>
/// <param name="Datum">Novi datum.</param>
public sealed record DodelaInput(string? InzenjerId, DateOnly? Datum);

/// <summary>Telo „Dodaj inženjera“ na PZ.</summary>
/// <param name="KorisnikId">Inženjer.</param>
/// <param name="Sekcije">Sekcije (prazno = ukloni).</param>
public sealed record UcesnikInput(string KorisnikId, IReadOnlyList<string> Sekcije);

/// <summary>Telo zahteva za odsustvo (Inženjer za sebe).</summary>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Napomena">Napomena.</param>
public sealed record ZahtevOdsustvaInput(DateOnly Od, DateOnly Do, string Vrsta, string? Napomena);

/// <summary>Telo unosa odsustva (Supervizor/Direktor).</summary>
/// <param name="KorisnikId">Inženjer.</param>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Napomena">Napomena.</param>
/// <param name="Prebacivanja">Prebacivanja planiranih pregleda.</param>
public sealed record OdsustvoInput(string KorisnikId, DateOnly Od, DateOnly Do, string Vrsta, string? Napomena, IReadOnlyList<PrebacivanjeInput>? Prebacivanja);

/// <summary>Telo odluke o odsustvu.</summary>
/// <param name="Odobreno">Odobri ili odbij.</param>
/// <param name="Razlog">Razlog odbijanja.</param>
/// <param name="Prebacivanja">„Odobri i prebaci“.</param>
public sealed record OdlukaOdsustvaInput(bool Odobreno, string? Razlog, IReadOnlyList<PrebacivanjeInput>? Prebacivanja);

/// <summary>Telo zahteva za prebacivanje.</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="Razlog">Razlog.</param>
/// <param name="PredlozenaZamenaId">Predlog zamene.</param>
public sealed record ZahtevPrebacivanjaInput(int PregledId, string Razlog, string? PredlozenaZamenaId);

/// <summary>Telo zahteva za raniji početak PZ-a.</summary>
/// <param name="PregledId">PZ.</param>
/// <param name="Razlog">Razlog.</param>
public sealed record ZahtevRanijegPocetkaInput(int PregledId, string Razlog);

/// <summary>Telo odluke o prebacivanju.</summary>
/// <param name="Odobreno">Odobri ili odbij.</param>
/// <param name="ZamenaId">Zamena.</param>
public sealed record OdlukaPrebacivanjaInput(bool Odobreno, string? ZamenaId);

/// <summary>Telo neradnih dana.</summary>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Vrsta">Vrsta.</param>
public sealed record NeradniDaniInput(DateOnly Od, DateOnly Do, string Naziv, string Vrsta);

/// <summary>Telo predloga.</summary>
/// <param name="Od">Prvi dan nedelje.</param>
public sealed record PredlogInput(DateOnly Od);

/// <summary>Telo potvrde.</summary>
/// <param name="Stavke">Dani rasporeda.</param>
public sealed record PotvrdaInput(IReadOnlyList<StavkaRasporeda> Stavke);

/// <summary>Telo zakazivanja PZ-a.</summary>
/// <param name="Datum">Dan.</param>
/// <param name="Tip">PZ ili null za sledeći.</param>
/// <param name="InzenjerId">Inženjer.</param>
/// <param name="Napomena">Napomena.</param>
public sealed record PzInput(DateOnly Datum, string? Tip, string InzenjerId, string? Napomena);

/// <summary>
/// Raspored, kalendar, PZ, neradni dani, odsustva i zahtevi za prebacivanje (R3.x). Kalendar čitaju svi; raspored menja
/// pravo <c>raspored.uredjivanje</c>, odsustva odobrava <c>odsustva.odobravanje</c>, neradne dane <c>neradni_dani</c>.
/// </summary>
public static class RasporedEndpoints
{
    private static bool Ima(ClaimsPrincipal u, string pravo) => u.HasClaim(JwtTokenService.PravoClaim, pravo);

    /// <summary>Mapira rute rasporeda.</summary>
    public static IEndpointRouteBuilder MapRasporedEndpoints(this IEndpointRouteBuilder app)
    {
        var raspored = PravoPolitika.Za(Prava.RasporedUredjivanje);
        var odsustva = PravoPolitika.Za(Prava.OdsustvaOdobravanje);

        var r = app.MapGroup("/api/v1/raspored").WithTags("Raspored").RequireAuthorization("RequireKorisnik");
        r.MapGet("", async ([FromQuery] DateOnly od, [FromQuery(Name = "do")] DateOnly doDatuma, IMediator m) =>
                Results.Ok(await m.Send(new GetRasporedQuery(od, doDatuma))))
            .WithSummary("Kalendar: pregledi, neradni dani i odsustva u periodu");
        r.MapGet("/inzenjeri", async (IMediator m) => Results.Ok(await m.Send(new GetInzenjeriQuery())))
            .WithSummary("Inženjeri u rotaciji (za izbor u rasporedu)");
        r.MapPost("/predlog", async ([FromBody] PredlogInput i, IMediator m) => Results.Ok(await m.Send(new PredlogRasporedaQuery(i.Od))))
            .RequireAuthorization(raspored).WithSummary("Predlog DP-a za nedelju (ne upisuje)");
        r.MapPost("/potvrdi", async ([FromBody] PotvrdaInput i, IMediator m) => Results.Ok(await m.Send(new PotvrdiRasporedCommand(i.Stavke))))
            .RequireAuthorization(raspored).WithSummary("Potvrda rasporeda: pravi preglede sa brojevima");
        r.MapGet("/pz/sledeci", async (IMediator m) => Results.Ok(new { tip = await m.Send(new SledeciPzQuery()) }))
            .RequireAuthorization(raspored).WithSummary("Sledeći PZ po redu");
        r.MapPost("/pz", async ([FromBody] PzInput i, IMediator m) =>
                Results.Ok(await m.Send(new ZakaziPzCommand(i.Datum, i.Tip, i.InzenjerId, i.Napomena))))
            .RequireAuthorization(raspored).WithSummary("Zakaži PZ (bez tipa = sledeći; drugi tip = „Promeni PZ“)");
        r.MapPost("/prenesi", async (IMediator m, IDateTimeProvider clock) =>
                Results.Ok(new { pomereno = await m.Send(new PrenesiCommand(clock.Today)) }))
            .RequireAuthorization(raspored).WithSummary("Ručni prenos današnjeg nezapočetog DP-a");

        var p = app.MapGroup("/api/v1/pregledi").WithTags("Pregledi").RequireAuthorization(raspored);
        p.MapPut("/{id:int}/dodela", async (int id, [FromBody] DodelaInput i, IMediator m) =>
                Results.Ok(await m.Send(new IzmeniDodeluCommand(id, i.InzenjerId, i.Datum))))
            .WithSummary("Promena inženjera i/ili datuma");
        p.MapPost("/{id:int}/ucesnici", async (int id, [FromBody] UcesnikInput i, IMediator m) =>
                Results.Ok(await m.Send(new PostaviUcesnikaCommand(id, i.KorisnikId, i.Sekcije))))
            .WithSummary("Dodaj inženjera na PZ sa sekcijama");

        var n = app.MapGroup("/api/v1/neradni-dani").WithTags("Neradni dani").RequireAuthorization("RequireKorisnik");
        n.MapGet("", async ([FromQuery] int godina, IMediator m) => Results.Ok(await m.Send(new GetNeradniDaniQuery(godina))))
            .WithSummary("Neradni dani u godini");
        n.MapPost("", async ([FromBody] NeradniDaniInput i, IMediator m) =>
                Results.Ok(await m.Send(new UnesiNeradneDaneCommand(i.Od, i.Do, i.Naziv, i.Vrsta))))
            .RequireAuthorization(PravoPolitika.Za(Prava.NeradniDani)).WithSummary("Unos neradnih dana od–do");
        n.MapDelete("/{id:int}", async (int id, IMediator m) =>
            {
                await m.Send(new ObrisiNeradniDanCommand(id));
                return Results.NoContent();
            })
            .RequireAuthorization(PravoPolitika.Za(Prava.NeradniDani)).WithSummary("Brisanje neradnog dana");

        var o = app.MapGroup("/api/v1/odsustva").WithTags("Odsustva").RequireAuthorization("RequireKorisnik");
        o.MapGet("", async ([FromQuery] string? korisnikId, [FromQuery] string? status, ClaimsPrincipal u, ICurrentUser me, IMediator m) =>
                // Bez prava odobravanja korisnik vidi samo svoja odsustva („Moja odsustva“).
                Results.Ok(await m.Send(new GetOdsustvaQuery(Ima(u, Prava.OdsustvaOdobravanje) ? korisnikId : me.UserId, status))))
            .WithSummary("Odsustva (svoja ili sva uz pravo odobravanja)");
        o.MapPost("", async ([FromBody] OdsustvoInput i, IMediator m) =>
                Results.Ok(await m.Send(new UnesiOdsustvoCommand(i.KorisnikId, i.Od, i.Do, i.Vrsta, i.Napomena, false, i.Prebacivanja))))
            .RequireAuthorization(odsustva).WithSummary("Unos odsustva (odmah odobreno) sa prebacivanjem");
        o.MapPost("/zahtev", async ([FromBody] ZahtevOdsustvaInput i, ICurrentUser me, IMediator m) =>
                Results.Ok(await m.Send(new UnesiOdsustvoCommand(me.UserId!, i.Od, i.Do, i.Vrsta, i.Napomena, true, null))))
            .RequireAuthorization(PravoPolitika.Za(Prava.PreglediPopunjavanje)).WithSummary("Zahtev inženjera za odsustvo");
        o.MapPost("/{id:int}/odluka", async (int id, [FromBody] OdlukaOdsustvaInput i, IMediator m) =>
                Results.Ok(await m.Send(new OdluciOOdsustvuCommand(id, i.Odobreno, i.Razlog, i.Prebacivanja))))
            .RequireAuthorization(odsustva).WithSummary("Odobri (i prebaci) ili odbij zahtev");
        o.MapGet("/{korisnikId}/pregledi", async (string korisnikId, [FromQuery] DateOnly od, [FromQuery(Name = "do")] DateOnly doDatuma, IMediator m) =>
                Results.Ok(await m.Send(new GetPreglediUOdsustvuQuery(korisnikId, od, doDatuma))))
            .RequireAuthorization(odsustva).WithSummary("Planirani pregledi inženjera u periodu sa predlogom zamene");

        var z = app.MapGroup("/api/v1/zahtevi-prebacivanja").WithTags("Prebacivanja").RequireAuthorization("RequireKorisnik");
        z.MapGet("", async ([FromQuery] string? status, ClaimsPrincipal u, IMediator m) =>
                Results.Ok(await m.Send(new GetZahteviPrebacivanjaQuery(status, !Ima(u, Prava.RasporedUredjivanje)))))
            .WithSummary("Zahtevi (svoji ili svi uz pravo rasporeda)");
        z.MapPost("", async ([FromBody] ZahtevPrebacivanjaInput i, IMediator m) =>
                Results.Ok(await m.Send(new PodnesiZahtevPrebacivanjaCommand(i.PregledId, i.Razlog, i.PredlozenaZamenaId))))
            .RequireAuthorization(PravoPolitika.Za(Prava.PreglediPopunjavanje)).WithSummary("Inženjer traži prebacivanje svog pregleda");
        z.MapPost("/raniji-pocetak", async ([FromBody] ZahtevRanijegPocetkaInput i, IMediator m) =>
                Results.Ok(await m.Send(new PodnesiZahtevRanijegPocetkaCommand(i.PregledId, i.Razlog))))
            .RequireAuthorization(PravoPolitika.Za(Prava.PreglediPopunjavanje)).WithSummary("Inženjer traži raniji početak svog PZ-a");
        z.MapPost("/{id:int}/odluka", async (int id, [FromBody] OdlukaPrebacivanjaInput i, IMediator m) =>
                Results.Ok(await m.Send(new OdluciOPrebacivanjuCommand(id, i.Odobreno, i.ZamenaId))))
            .RequireAuthorization(raspored).WithSummary("Odobri (sa zamenom) ili odbij");

        return app;
    }
}
