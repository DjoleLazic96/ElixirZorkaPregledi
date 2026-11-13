using TpmPregledi.Domain.Entities;
using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Akcije;
using TpmPregledi.Application.Features.Pregledi;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Telo zaduženog.</summary>
/// <param name="Zaduzen">Osoba ili služba.</param>
public sealed record ZaduzenInput(string? Zaduzen);

/// <summary>Telo zatvaranja akcije (zamerka 49).</summary>
/// <param name="Uradjeno">Šta je urađeno.</param>
/// <param name="Datum">Kada.</param>
/// <param name="Izvrsilac">Ko je uradio.</param>
public sealed record ZatvaranjeInput(string? Uradjeno, DateOnly? Datum, string? Izvrsilac);

/// <summary>Odluka o zahtevu za zatvaranje.</summary>
/// <param name="Odobreno">Odobri ili odbij.</param>
/// <param name="Razlog">Razlog odbijanja.</param>
public sealed record OdlukaZatvaranjaInput(bool Odobreno, string? Razlog);

/// <summary>Potrebne akcije i neispravnosti (R7.x, R8.1, R8.16). Čitaju svi; zatvaranje, otkazivanje i zaduženi traže pravo <c>akcije.zatvaranje</c>.</summary>
public static class AkcijeEndpoints
{
    private static IReadOnlyDictionary<string, string> Vrsta => AkcijaPravila.VrstaLabela;

    private static IReadOnlyDictionary<string, string> Hitnost => AkcijaPravila.HitnostLabela;

    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const long NajvecaSlika = 3 * 1024 * 1024;

    /// <summary>Mapira <c>/akcije</c>, <c>/kvarovi</c> i izvoz.</summary>
    public static IEndpointRouteBuilder MapAkcijeEndpoints(this IEndpointRouteBuilder app)
    {
        var pravo = PravoPolitika.Za(Prava.AkcijeZatvaranje);
        var a = app.MapGroup("/api/v1/akcije").WithTags("Akcije").RequireAuthorization("RequireKorisnik");
        a.MapGet("", async ([FromQuery] string? status, [FromQuery] string? hitnost, [FromQuery] int? sekcijaId, [FromQuery] int? uredjajId,
                [FromQuery] bool? sadaIspravna, [FromQuery] bool? saZahtevom, IMediator m) =>
                Results.Ok(await m.Send(new GetAkcijeQuery(new AkcijeFilteri(status, hitnost, sekcijaId, uredjajId, sadaIspravna, saZahtevom)))))
            .WithSummary("Potrebne akcije (filteri status, hitnost, sekcija, uređaj, stavka sada ispravna)");
        a.MapGet("/{id:int}", async (int id, IMediator m) => Results.Ok(await m.Send(new GetAkcijaQuery(id)))).WithSummary("Akcija sa istorijom");
        a.MapPost("/{id:int}/zatvori", async (int id, [FromBody] ZatvaranjeInput i, IMediator m) =>
                Results.Ok(await m.Send(new ZatvoriAkcijuCommand(id, false, null, i.Uradjeno, i.Datum, i.Izvrsilac))))
            .RequireAuthorization(pravo).WithSummary("Zatvori akciju (šta je urađeno, kada, ko)");
        a.MapPost("/{id:int}/zahtev-zatvaranja", async (int id, HttpRequest req, IMediator m) =>
            {
                if (!req.HasFormContentType) return Results.BadRequest();
                var forma = await req.ReadFormAsync();
                if (!Guid.TryParse(forma["klijentId"], out var klijentId) || !DateOnly.TryParse(forma["datum"], System.Globalization.CultureInfo.InvariantCulture, out var datum))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["datum"] = ["Izaberite kada je urađeno."] });
                byte[]? slika = null;
                var fajl = forma.Files.GetFile("fajl");
                if (fajl is { Length: > 0 })
                {
                    if (fajl.Length > NajvecaSlika) return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Slika je prevelika."] });
                    using var ms = new MemoryStream();
                    await fajl.CopyToAsync(ms);
                    slika = ms.ToArray();
                }
                return Results.Ok(await m.Send(new PodnesiZahtevZatvaranjaCommand(id, klijentId, forma["uradjeno"].ToString(), datum, forma["izvrsilac"].ToString(), slika, fajl?.ContentType)));
            })
            .RequireAuthorization(PravoPolitika.Za(Prava.PreglediPopunjavanje)).DisableAntiforgery().WithSummary("Inženjer traži zatvaranje akcije (opciono slika ≤ 3 MB)");
        a.MapPost("/zahtevi-zatvaranja/{zid:int}/odluka", async (int zid, [FromBody] OdlukaZatvaranjaInput i, IMediator m) =>
                Results.Ok(await m.Send(new OdluciOZahtevuZatvaranjaCommand(zid, i.Odobreno, i.Razlog))))
            .RequireAuthorization(pravo).WithSummary("Odobri ili odbij zahtev za zatvaranje");
        a.MapPost("/zahtevi-zatvaranja/{zid:int}/otkazi", async (int zid, IMediator m) => Results.Ok(await m.Send(new OtkaziZahtevZatvaranjaCommand(zid))))
            .RequireAuthorization(PravoPolitika.Za(Prava.PreglediPopunjavanje)).WithSummary("Inženjer otkazuje svoj zahtev za zatvaranje");
        a.MapGet("/{id:int}/slika", async (int id, [FromQuery] int? zahtev, IMediator m) =>
            {
                var (sadrzaj, mime) = await m.Send(new GetSlikaZatvaranjaQuery(id, zahtev));
                return Results.Stream(sadrzaj, mime);
            })
            .WithSummary("Slika uz zatvaranje akcije ili uz zahtev");
        a.MapPost("/{id:int}/otkazi", async (int id, [FromBody] RazlogInput i, IMediator m) => Results.Ok(await m.Send(new ZatvoriAkcijuCommand(id, true, i.Razlog))))
            .RequireAuthorization(pravo).WithSummary("Otkaži akciju (razlog)");
        a.MapPut("/{id:int}/zaduzen", async (int id, [FromBody] ZaduzenInput i, IMediator m) => Results.Ok(await m.Send(new PostaviZaduzenogCommand(id, i.Zaduzen))))
            .RequireAuthorization(pravo).WithSummary("Zaduženi (osoba ili služba)");

        var k = app.MapGroup("/api/v1/kvarovi").WithTags("Neispravnosti").RequireAuthorization("RequireKorisnik");
        k.MapGet("", async ([FromQuery] bool? otvoreno, [FromQuery] int? sekcijaId, [FromQuery] int? uredjajId, [FromQuery] string? pretraga, IMediator m) =>
                Results.Ok(await m.Send(new GetKvaroviQuery(new KvaroviFilteri(otvoreno, sekcijaId, uredjajId, pretraga)))))
            .WithSummary("Kvarovi (filter trenutno otvoreno, sekcija, uređaj, pretraga)");
        k.MapGet("/{id:int}", async (int id, IMediator m) => Results.Ok(await m.Send(new GetKvarQuery(id)))).WithSummary("Kvar sa prijavama i akcijama");

        app.MapGet("/api/v1/izvoz/akcije.xlsx", async ([FromQuery] string? status, [FromQuery] string? hitnost, [FromQuery] int? sekcijaId, [FromQuery] bool? sadaIspravna,
                IMediator m, IIzvestajPregleda izv) =>
            {
                var lista = await m.Send(new GetAkcijeQuery(new AkcijeFilteri(status, hitnost, sekcijaId, null, sadaIspravna)));
                var bajtovi = izv.ExcelTabela("Akcije", ["Broj", "Pregled", "Uređaj", "Sekcija", "Aktivnost", "Vrsta", "Hitnost", "Rok", "Status", "Zadužen", "Opis", "Kvar",
                        "Urađeno", "Datum urađeno", "Uradio"],
                    lista.Select(x => (IReadOnlyList<object?>)[x.Broj, x.Pregled, x.Uredjaj, x.Sekcija, x.Aktivnost, Vrsta.GetValueOrDefault(x.Vrsta, x.Vrsta),
                        Hitnost.GetValueOrDefault(x.Hitnost, x.Hitnost), x.Rok, x.Status, x.Zaduzen, x.Opis, x.KvarBroj, x.Uradjeno ?? x.Razlog, x.UradjenoDatum, x.Izvrsilac]));
                return Results.File(bajtovi, Xlsx, "Akcije.xlsx");
            }).WithTags("Akcije").RequireAuthorization("RequireKorisnik").WithSummary("Akcije u Excel (filtrirano)");

        app.MapGet("/api/v1/izvoz/kvarovi.xlsx", async ([FromQuery] bool? otvoreno, [FromQuery] int? sekcijaId, [FromQuery] string? pretraga, IMediator m, IIzvestajPregleda izv) =>
            {
                var lista = await m.Send(new GetKvaroviQuery(new KvaroviFilteri(otvoreno, sekcijaId, null, pretraga)));
                var bajtovi = izv.ExcelTabela("Neispravnosti", ["Broj", "Uređaj", "Sekcija", "Aktivnost", "Otvoren", "Zatvoren", "Trajanje (dana)", "Prijava", "Pregled", "Napomena"],
                    lista.Select(x => (IReadOnlyList<object?>)[x.Broj, x.Uredjaj, x.Sekcija, x.Aktivnost, x.Otvoren, x.Zatvoren, x.TrajanjeDana, x.Prijava, x.OtvorioPregled, x.PoslednjaNapomena]));
                return Results.File(bajtovi, Xlsx, "Neispravnosti.xlsx");
            }).WithTags("Neispravnosti").RequireAuthorization("RequireKorisnik").WithSummary("Neispravnosti u Excel (filtrirano)");
        return app;
    }
}
