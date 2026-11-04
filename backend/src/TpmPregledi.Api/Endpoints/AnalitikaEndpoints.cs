using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Analitika;
using TpmPregledi.Application.Features.Pregledi;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Ekran uređaja, analitika, Dashboard i pretraga (R8.4–R8.14, R5.6). Čitaju svi; opšta analitika i ispunjenje traže pravo <c>analitika</c>.</summary>
public static class AnalitikaEndpoints
{
    private static readonly Dictionary<string, string> Stanje = new() { ["ispravan"] = "Ispravan", ["neispravan"] = "Neispravan", ["van_funkcije"] = "Nije u funkciji" };

    /// <summary>Mapira rute.</summary>
    public static IEndpointRouteBuilder MapAnalitikaEndpoints(this IEndpointRouteBuilder app)
    {
        var u = app.MapGroup("/api/v1/uredjaji").WithTags("Uređaji").RequireAuthorization("RequireKorisnik");
        u.MapGet("/{id:int}/ekran", async (int id, [FromQuery] int? dana, IMediator m) => Results.Ok(await m.Send(new GetUredjajEkranQuery(id, dana ?? 90))))
            .WithSummary("Ekran uređaja: status, istorija (dana 30–365), pregledi, dosije, akcije, kvarovi, analitika");
        u.MapGet("/qr/{token:guid}", async (Guid token, IMediator m) => Results.Ok(new { id = await m.Send(new GetUredjajPoQrQuery(token)) }))
            .WithSummary("Uređaj po QR tokenu");
        u.MapGet("/{id:int}/pregledi.xlsx", async (int id, IMediator m, IIzvestajPregleda izv) =>
            {
                var e = await m.Send(new GetUredjajEkranQuery(id, 30));
                var bajtovi = izv.ExcelTabela("Pregledi uređaja", ["Broj", "Datum", "Tip", "Inženjer", "Stanje uređaja", "Neispravnih", "Status"],
                    e.Pregledi.Select(p => (IReadOnlyList<object?>)[p.Broj, p.Datum, p.Tip, p.Inzenjer, Stanje.GetValueOrDefault(p.Stanje, p.Stanje), p.Neispravnih, p.Status]));
                return Results.File(bajtovi, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Pregledi {e.Prikaz}.xlsx");
            }).WithSummary("Svi pregledi uređaja u Excel");

        var a = app.MapGroup("/api/v1/analitika").WithTags("Analitika").RequireAuthorization(PravoPolitika.Za(Prava.Analitika));
        a.MapGet("/opsta", async ([FromQuery] DateOnly? od, [FromQuery(Name = "do")] DateOnly? doDatuma, IMediator m) =>
            Results.Ok(await m.Send(new GetOpstaAnalitikaQuery(od, doDatuma)))).WithSummary("Opšta analitika (podrazumevano 90 dana)");
        a.MapGet("/ispunjenje", async ([FromQuery] DateOnly? od, [FromQuery(Name = "do")] DateOnly? doDatuma, IMediator m) =>
            Results.Ok(await m.Send(new GetIspunjenjeQuery(od, doDatuma)))).WithSummary("Ispunjenje plana po mesecu i inženjeru (podrazumevano tekuća godina)");

        app.MapGet("/api/v1/dashboard", async (IMediator m) => Results.Ok(await m.Send(new GetDashboardQuery())))
            .WithTags("Dashboard").RequireAuthorization("RequireKorisnik").WithSummary("Dashboard trenutnog korisnika");
        app.MapGet("/api/v1/pretraga", async ([FromQuery] string? q, IMediator m) => Results.Ok(await m.Send(new PretragaQuery(q))))
            .WithTags("Pretraga").RequireAuthorization("RequireKorisnik").WithSummary("Pretraga u zaglavlju");
        return app;
    }
}
