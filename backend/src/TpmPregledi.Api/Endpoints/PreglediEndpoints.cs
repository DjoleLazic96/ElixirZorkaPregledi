using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Pregledi;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Telo „Vrati na doradu“.</summary>
/// <param name="InzenjerId">Ko radi doradu.</param>
/// <param name="Razlog">Razlog.</param>
public sealed record VratiInput(string InzenjerId, string Razlog);

/// <summary>Telo sa razlogom.</summary>
/// <param name="Razlog">Razlog.</param>
public sealed record RazlogInput(string Razlog);

/// <summary>Telo odluke.</summary>
/// <param name="Odobreno">Odobri ili odbij.</param>
public sealed record OdlukaInput(bool Odobreno);

/// <summary>Telo odluke o spornom unosu.</summary>
/// <param name="Primeni">Primeni ili odbaci.</param>
public sealed record SporanInput(bool Primeni);

/// <summary>
/// Arhiva, detalj, overa, dorada, poništavanje i izveštaji (R6.x, R11.x). Čitaju svi (R1.7); odluke traže pravo <c>overa</c>;
/// zahtev za doradu šalje inženjer koji je radio pregled.
/// </summary>
public static class PreglediEndpoints
{
    /// <summary>Mapira rute arhive i overe.</summary>
    public static IEndpointRouteBuilder MapPreglediEndpoints(this IEndpointRouteBuilder app)
    {
        var overa = PravoPolitika.Za(Prava.Overa);
        var g = app.MapGroup("/api/v1/pregledi").WithTags("Pregledi").RequireAuthorization("RequireKorisnik");

        static PreglediFilteri Filteri(DateOnly? od, DateOnly? doDatuma, string? tip, string? status, string? inzenjerId, string? pretraga, int? stranica, int? velicina) =>
            new(od, doDatuma, tip, status, inzenjerId, pretraga, stranica ?? 1, velicina ?? 50);

        g.MapGet("", async ([FromQuery] DateOnly? od, [FromQuery(Name = "do")] DateOnly? doDatuma, [FromQuery] string? tip, [FromQuery] string? status,
                [FromQuery] string? inzenjerId, [FromQuery] string? pretraga, [FromQuery] int? stranica, [FromQuery] int? velicina, IMediator m) =>
                Results.Ok(await m.Send(new GetPreglediQuery(Filteri(od, doDatuma, tip, status, inzenjerId, pretraga, stranica, velicina)))))
            .WithSummary("Arhiva pregleda (filteri, stranice)");
        g.MapGet("/{id:int}", async (int id, IMediator m) => Results.Ok(await m.Send(new GetPregledQuery(id))))
            .WithSummary("Detalj pregleda: odgovori, slike, vreme, upozorenja, istorija");
        g.MapGet("/{id:int}/izvoz", async (int id, [FromQuery] string? format, [FromQuery] string? prikaz, IMediator m, IIzvestajPregleda izv) =>
            {
                var p = await m.Send(new GetPregledQuery(id));
                var rezime = prikaz == "rezime";
                var ime = rezime ? $"{p.Broj}-rezime" : p.Broj;
                var ids = p.Sekcije.SelectMany(s => s.Uredjaji).SelectMany(u => u.Stavke).SelectMany(s => s.Fotografije).ToList();
                var slike = ids.Count == 0 ? null : await m.Send(new GetSlikeZaIzvestajQuery(ids));
                return format == "docx"
                    ? Results.File(izv.Docx(p, rezime, slike), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{ime}.docx")
                    : Results.File(izv.Pdf(p, rezime, slike), "application/pdf", $"{ime}.pdf");
            })
            .WithSummary("Izveštaj pregleda (format=pdf|docx, prikaz=ceo|rezime)");

        g.MapPost("/{id:int}/overi", async (int id, IMediator m) => Results.Ok(await m.Send(new OveriCommand(id))))
            .RequireAuthorization(overa).WithSummary("Overio");
        g.MapPost("/{id:int}/vrati-na-doradu", async (int id, [FromBody] VratiInput i, IMediator m) =>
                Results.Ok(await m.Send(new VratiNaDoraduCommand(id, i.InzenjerId, i.Razlog))))
            .RequireAuthorization(overa).WithSummary("Vrati na doradu (pre i posle overe)");
        g.MapPost("/{id:int}/zahtev-za-doradu", async (int id, [FromBody] RazlogInput i, IMediator m) =>
                Results.Ok(await m.Send(new ZatraziDoraduCommand(id, i.Razlog))))
            .RequireAuthorization(PravoPolitika.Za(Prava.PreglediPopunjavanje)).WithSummary("Inženjer traži doradu svog pregleda");
        g.MapPost("/{id:int}/zahtev-za-doradu/otkazi", async (int id, IMediator m) => Results.Ok(await m.Send(new OtkaziZahtevZaDoraduCommand(id))))
            .RequireAuthorization(PravoPolitika.Za(Prava.PreglediPopunjavanje)).WithSummary("Inženjer otkazuje svoj zahtev za doradu (dok čeka odluku)");
        g.MapPost("/{id:int}/zahtev-za-doradu/odluka", async (int id, [FromBody] OdlukaInput i, IMediator m) =>
                Results.Ok(await m.Send(new OdluciODoradiCommand(id, i.Odobreno))))
            .RequireAuthorization(overa).WithSummary("Odluka o zahtevu za doradu");
        g.MapPost("/{id:int}/ponisti", async (int id, [FromBody] RazlogInput i, IMediator m) =>
                Results.Ok(await m.Send(new PonistiCommand(id, i.Razlog))))
            .RequireAuthorization(overa).WithSummary("Poništi pregled (razlog obavezan)");
        g.MapPost("/{id:int}/otkljucaj", async (int id, IMediator m) => Results.Ok(await m.Send(new OtkljucajUredjajCommand(id))))
            .RequireAuthorization(overa).WithSummary("Otključaj pregled za drugi uređaj");
        g.MapPost("/{id:int}/sporan-unos/{unosId:long}/odluka", async (int id, long unosId, [FromBody] SporanInput i, IMediator m) =>
                Results.Ok(await m.Send(new OdluciOSpornomUnosuCommand(id, unosId, i.Primeni))))
            .RequireAuthorization(overa).WithSummary("Odluka o spornom unosu");

        app.MapGet("/api/v1/izvoz/pregledi.xlsx", async ([FromQuery] DateOnly? od, [FromQuery(Name = "do")] DateOnly? doDatuma, [FromQuery] string? tip,
                [FromQuery] string? status, [FromQuery] string? inzenjerId, [FromQuery] string? pretraga, IMediator m, IIzvestajPregleda izv) =>
            {
                var strana = await m.Send(new GetPreglediQuery(Filteri(od, doDatuma, tip, status, inzenjerId, pretraga, 1, 100_000)));
                return Results.File(izv.Excel(strana.Stavke), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Pregledi.xlsx");
            })
            .WithTags("Pregledi").RequireAuthorization("RequireKorisnik").WithSummary("Arhiva u Excel (filtrirano)");
        return app;
    }
}
