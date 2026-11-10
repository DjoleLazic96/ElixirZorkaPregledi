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

/// <summary>Potrebne akcije i neispravnosti (R7.x, R8.1, R8.16). Čitaju svi; zatvaranje, otkazivanje i zaduženi traže pravo <c>akcije.zatvaranje</c>.</summary>
public static class AkcijeEndpoints
{
    private static readonly Dictionary<string, string> Vrsta = new()
    {
        ["ocistiti"] = "Očistiti", ["podmazati"] = "Podmazati ili dopuniti", ["zameniti"] = "Zameniti", ["popraviti"] = "Popraviti ili sanirati", ["ostalo"] = "Ostalo",
    };

    private static readonly Dictionary<string, string> Hitnost = new()
    {
        ["hitno"] = "Hitno", ["7_dana"] = "U narednih 7 dana", ["sledeci_pz"] = "U sledećem planskom zastoju", ["pratiti"] = "Pratiti",
    };

    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Mapira <c>/akcije</c>, <c>/kvarovi</c> i izvoz.</summary>
    public static IEndpointRouteBuilder MapAkcijeEndpoints(this IEndpointRouteBuilder app)
    {
        var pravo = PravoPolitika.Za(Prava.AkcijeZatvaranje);
        var a = app.MapGroup("/api/v1/akcije").WithTags("Akcije").RequireAuthorization("RequireKorisnik");
        a.MapGet("", async ([FromQuery] string? status, [FromQuery] string? hitnost, [FromQuery] int? sekcijaId, [FromQuery] int? uredjajId,
                [FromQuery] bool? sadaIspravna, IMediator m) =>
                Results.Ok(await m.Send(new GetAkcijeQuery(new AkcijeFilteri(status, hitnost, sekcijaId, uredjajId, sadaIspravna)))))
            .WithSummary("Potrebne akcije (filteri status, hitnost, sekcija, uređaj, stavka sada ispravna)");
        a.MapGet("/{id:int}", async (int id, IMediator m) => Results.Ok(await m.Send(new GetAkcijaQuery(id)))).WithSummary("Akcija sa istorijom");
        a.MapPost("/{id:int}/zatvori", async (int id, [FromBody] RazlogInput i, IMediator m) => Results.Ok(await m.Send(new ZatvoriAkcijuCommand(id, false, i.Razlog))))
            .RequireAuthorization(pravo).WithSummary("Zatvori akciju (razlog)");
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
                var bajtovi = izv.ExcelTabela("Akcije", ["Broj", "Pregled", "Uređaj", "Sekcija", "Aktivnost", "Vrsta", "Hitnost", "Rok", "Status", "Zadužen", "Opis", "Kvar"],
                    lista.Select(x => (IReadOnlyList<object?>)[x.Broj, x.Pregled, x.Uredjaj, x.Sekcija, x.Aktivnost, Vrsta.GetValueOrDefault(x.Vrsta, x.Vrsta),
                        Hitnost.GetValueOrDefault(x.Hitnost, x.Hitnost), x.Rok, x.Status, x.Zaduzen, x.Opis, x.KvarBroj]));
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
