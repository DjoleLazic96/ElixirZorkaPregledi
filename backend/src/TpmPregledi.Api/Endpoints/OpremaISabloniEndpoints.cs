using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Aktivnosti;
using TpmPregledi.Application.Features.DodatnaPolja;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.Features.Uredjaji;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// Registar opreme, katalog aktivnosti, dodatna polja i šabloni. Čitaju svi ulogovani (R1.7); izmene registra i
/// kataloga traže pravo <c>sabloni.nacrt</c> (R2.3); uvoz šablona i nova dodatna polja su za Admina (R2.13, R2.15).
/// </summary>
public static class OpremaISabloniEndpoints
{
    /// <summary>Najveći fajl za uvoz šablona (šest šablona je ~60 KB).</summary>
    private const long NajveciUvoz = 5 * 1024 * 1024;

    /// <summary>Mapira <c>/uredjaji</c>, <c>/aktivnosti</c>, <c>/dodatna-polja</c>, <c>/sabloni</c>.</summary>
    public static IEndpointRouteBuilder MapOpremaISabloniEndpoints(this IEndpointRouteBuilder app)
    {
        var nacrt = PravoPolitika.Za(Prava.SabloniNacrt);

        var u = app.MapGroup("/api/v1/uredjaji").WithTags("Uređaji").RequireAuthorization("RequireKorisnik");
        u.MapGet("", async ([FromQuery] int? sekcijaId, [FromQuery] string? status, [FromQuery] string? pretraga,
                [FromQuery] bool? bezSablona, IMediator m) =>
                Results.Ok(await m.Send(new GetUredjajiQuery(sekcijaId, status, pretraga, bezSablona ?? false))))
            .WithSummary("Registar opreme (filteri sekcija, status, pretraga, bezSablona)");
        u.MapGet("/{id:int}", async (int id, IMediator m) => Results.Ok(await m.Send(new GetUredjajQuery(id))))
            .WithSummary("Jedan uređaj");
        u.MapPost("", async ([FromBody] UredjajInput input, IMediator m) =>
            {
                var dto = await m.Send(new KreirajUredjajCommand(input));
                return Results.Created($"/api/v1/uredjaji/{dto.Id}", dto);
            })
            .RequireAuthorization(nacrt).WithSummary("Novi uređaj");
        u.MapPut("/{id:int}", async (int id, [FromBody] UredjajInput input, IMediator m) =>
                Results.Ok(await m.Send(new IzmeniUredjajCommand(id, input))))
            .RequireAuthorization(nacrt).WithSummary("Izmena uređaja");
        u.MapPost("/{id:int}/van-upotrebe", async (int id, IMediator m) =>
                Results.Ok(await m.Send(new PostaviStatusUredjajaCommand(id, Domain.Entities.StatusUredjaja.VanUpotrebe))))
            .RequireAuthorization(nacrt).WithSummary("Uređaj van upotrebe (umesto brisanja)");
        u.MapPost("/{id:int}/u-upotrebi", async (int id, IMediator m) =>
                Results.Ok(await m.Send(new PostaviStatusUredjajaCommand(id, Domain.Entities.StatusUredjaja.Aktivan))))
            .RequireAuthorization(nacrt).WithSummary("Vrati uređaj u upotrebu");

        var a = app.MapGroup("/api/v1/aktivnosti").WithTags("Aktivnosti").RequireAuthorization("RequireKorisnik");
        a.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetAktivnostiQuery())))
            .WithSummary("Katalog aktivnosti");
        a.MapPost("", async ([FromBody] AktivnostInput input, IMediator m) =>
            {
                var dto = await m.Send(new KreirajAktivnostCommand(input));
                return Results.Created($"/api/v1/aktivnosti/{dto.Id}", dto);
            })
            .RequireAuthorization(nacrt).WithSummary("Nova aktivnost");
        a.MapPut("/{id:int}", async (int id, [FromBody] AktivnostInput input, IMediator m) =>
                Results.Ok(await m.Send(new IzmeniAktivnostCommand(id, input))))
            .RequireAuthorization(nacrt).WithSummary("Izmena aktivnosti (par odgovora samo ako nije u važećem šablonu)");

        var p = app.MapGroup("/api/v1/dodatna-polja").WithTags("Dodatna polja").RequireAuthorization("RequireKorisnik");
        p.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetDodatnaPoljaQuery())))
            .WithSummary("Dodatna polja");
        p.MapPost("", async ([FromBody] DodatnoPoljeInput input, IMediator m) =>
            {
                var dto = await m.Send(new KreirajDodatnoPoljeCommand(input));
                return Results.Created($"/api/v1/dodatna-polja/{dto.Id}", dto);
            })
            .RequireAuthorization("RequireAdmin").WithSummary("Novo dodatno polje (Admin)");

        var s = app.MapGroup("/api/v1/sabloni").WithTags("Šabloni").RequireAuthorization("RequireKorisnik");
        s.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetSabloniQuery())))
            .WithSummary("Šest šablona sa važećom verzijom");
        s.MapGet("/{tip}", async (string tip, IMediator m) => Results.Ok(await m.Send(new GetSablonVerzijaQuery(tip.ToUpperInvariant()))))
            .WithSummary("Važeća verzija šablona (sekcije → uređaji → aktivnosti)");
        s.MapGet("/{tip}/verzije/{broj:int}", async (string tip, int broj, IMediator m) =>
                Results.Ok(await m.Send(new GetSablonVerzijaQuery(tip.ToUpperInvariant(), broj))))
            .WithSummary("Određena verzija šablona");
        s.MapPost("/uvoz", async (IFormFile? fajl, IMediator m) =>
            {
                if (fajl is null || fajl.Length == 0)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Izaberite .xlsx fajl."] });
                if (fajl.Length > NajveciUvoz)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Fajl je veći od 5 MB."] });

                await using var tok = fajl.OpenReadStream();
                return Results.Ok(await m.Send(new UvoziSabloneCommand(tok)));
            })
            .RequireAuthorization("RequireAdmin")
            .DisableAntiforgery()   // JWT u zaglavlju, ne cookie — CSRF nije primenljiv
            .WithSummary("Prvi uvoz šablona iz .xlsx (Admin)");

        return app;
    }
}
