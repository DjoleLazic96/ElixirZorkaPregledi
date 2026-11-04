using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Telo <c>POST /sinhronizacija</c>.</summary>
/// <param name="UredjajKlijenta">ID instalacije (faza 05).</param>
/// <param name="KlijentSada">Sat klijenta.</param>
/// <param name="Operacije">Operacije redom.</param>
public sealed record SinhronizacijaInput(string? UredjajKlijenta, DateTime? KlijentSada, IReadOnlyList<OperacijaInput> Operacije);

/// <summary>
/// Popunjavanje pregleda (pravo <c>pregledi.popunjavanje</c>): početak, čitanje, operacije i slike. Operacije idu kroz
/// <c>/sinhronizacija</c> od prvog dana, da rad bez interneta (faza 05) koristi isti ugovor.
/// </summary>
public static class PopunjavanjeEndpoints
{
    /// <summary>Najveća slika posle smanjivanja na telefonu (spec §9).</summary>
    private const long NajvecaSlika = 3 * 1024 * 1024;

    /// <summary>Mapira rute popunjavanja.</summary>
    public static IEndpointRouteBuilder MapPopunjavanjeEndpoints(this IEndpointRouteBuilder app)
    {
        var pravo = PravoPolitika.Za(Prava.PreglediPopunjavanje);

        app.MapPost("/api/v1/pregledi/{id:int}/zapocni", async (int id, IMediator m) => Results.Ok(await m.Send(new ZapocniPregledCommand(id))))
            .WithTags("Popunjavanje").RequireAuthorization(pravo).WithSummary("Početak pregleda (vezuje važeću verziju šablona)");
        app.MapGet("/api/v1/pregledi/{id:int}/popunjavanje", async (int id, IMediator m) => Results.Ok(await m.Send(new GetPopunjavanjeQuery(id))))
            .WithTags("Popunjavanje").RequireAuthorization(pravo).WithSummary("Pregled za popunjavanje: stavke, odgovori, van funkcije, slike");
        app.MapPost("/api/v1/sinhronizacija", async ([FromBody] SinhronizacijaInput i, IMediator m) =>
                Results.Ok(await m.Send(new SinhronizacijaCommand(i.UredjajKlijenta, i.KlijentSada, i.Operacije ?? []))))
            .WithTags("Popunjavanje").RequireAuthorization(pravo).WithSummary("Operacije pregleda (idempotentno po opId)");

        app.MapPost("/api/v1/fotografije", async (HttpRequest req, IMediator m) =>
            {
                if (!req.HasFormContentType) return Results.BadRequest();
                var forma = await req.ReadFormAsync();
                var fajl = forma.Files.GetFile("fajl");
                if (fajl is null || fajl.Length == 0)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Izaberite sliku."] });
                if (fajl.Length > NajvecaSlika)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Slika je prevelika."] });
                if (!FotografijeHandler.Tipovi.Contains(fajl.ContentType))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Dozvoljene su samo slike (JPEG, PNG, WebP)."] });
                if (!Guid.TryParse(forma["klijentId"], out var klijentId) || !int.TryParse(forma["pregledId"], out var pregledId)
                    || !int.TryParse(forma["stavkaId"], out var stavkaId))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["fajl"] = ["Nedostaju podaci o slici."] });

                using var ms = new MemoryStream();
                await fajl.CopyToAsync(ms);
                var id = await m.Send(new SacuvajFotografijuCommand(klijentId, pregledId, stavkaId, forma["izvor"].ToString(), ms.ToArray(), fajl.ContentType));
                return Results.Ok(new { id });
            })
            .WithTags("Popunjavanje").RequireAuthorization(pravo).DisableAntiforgery().WithSummary("Slika uz stavku (≤ 3 MB, najviše 5 po stavci)");

        app.MapGet("/api/v1/fotografije/{id:long}", async (long id, IMediator m) =>
            {
                var (sadrzaj, mime) = await m.Send(new GetFotografijaQuery(id));
                return Results.Stream(sadrzaj, mime);
            })
            .WithTags("Popunjavanje").RequireAuthorization("RequireKorisnik").WithSummary("Slika (svi vide sve preglede, R1.7)");

        return app;
    }
}
