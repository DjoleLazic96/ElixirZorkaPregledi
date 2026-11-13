using TpmPregledi.Application.Features.Korisnici.Commands;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Application.Features.Korisnici.Queries;
using TpmPregledi.Api.Common;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>Telo deaktivacije: prebacivanje planiranih pregleda (zamerka 54).</summary>
/// <param name="Prebacivanja">Pregled i zamena.</param>
public sealed record DeaktivacijaInput(IReadOnlyList<TpmPregledi.Application.Features.Raspored.PrebacivanjeInput>? Prebacivanja);

/// <summary>
/// Upravljanje korisnicima i pravima (<c>/api/v1/korisnici</c>, pravo <c>korisnici</c>). Korisnici se nikad ne brišu —
/// nema DELETE rute, samo <c>deaktiviraj</c>/<c>aktiviraj</c>. Lozinka ide isključivo u telo zahteva.
/// </summary>
public static class KorisniciEndpoints
{
    /// <summary>Mapira <c>/api/v1/korisnici/*</c>.</summary>
    public static IEndpointRouteBuilder MapKorisniciEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/korisnici")
            .WithTags("Korisnici")
            .RequireAuthorization(PravoPolitika.Za(Prava.Korisnici));

        group.MapGet("",
            async ([FromQuery] bool? aktivan, IMediator m) =>
                Results.Ok(await m.Send(new GetKorisniciQuery(aktivan))))
            .WithSummary("Lista korisnika (filter aktivan)");

        group.MapPost("",
            async ([FromBody] KreirajKorisnikaInput input, IMediator m) =>
            {
                var dto = await m.Send(new KreirajKorisnikaCommand(
                    input.KorisnickoIme, input.Ime, input.Email, input.Lozinka, input.Rola));
                return Results.Created($"/api/v1/korisnici/{dto.Id}", dto);
            })
            .WithSummary("Kreiraj korisnika (korisničko ime, ime, e-mail, lozinka, rola)");

        group.MapPut("/{id}",
            async (string id, [FromBody] IzmeniKorisnikaInput input, IMediator m) =>
                Results.Ok(await m.Send(new IzmeniKorisnikaCommand(id, input.Ime, input.Email, input.Rola))))
            .WithSummary("Izmeni ime, e-mail i rolu korisnika");

        group.MapPut("/{id}/prava",
            async (string id, [FromBody] PostaviPravaInput input, IMediator m) =>
                Results.Ok(await m.Send(new PostaviPravaCommand(id, input.Prava))))
            .WithSummary("Zameni štiklirana prava korisnika (revokuje sesije)");

        group.MapPost("/{id}/deaktiviraj",
            async (string id, [FromBody] DeaktivacijaInput? input, IMediator m) =>
                Results.Ok(await m.Send(new DeaktivirajKorisnikaCommand(id, input?.Prebacivanja))))
            .WithSummary("Deaktiviraj korisnika (blokira prijavu, revokuje sesije); planirani pregledi se mogu prebaciti");

        group.MapGet("/{id}/planirani-pregledi",
            async (string id, IMediator m, TpmPregledi.Application.Common.Interfaces.IDateTimeProvider clock) =>
                Results.Ok(await m.Send(new TpmPregledi.Application.Features.Raspored.GetPreglediUOdsustvuQuery(id, clock.Today, clock.Today.AddYears(1)))))
            .WithSummary("Planirani pregledi korisnika sa predlogom zamene (pre deaktivacije, zamerka 54)");

        group.MapPost("/{id}/aktiviraj",
            async (string id, IMediator m) =>
                Results.Ok(await m.Send(new AktivirajKorisnikaCommand(id))))
            .WithSummary("Ponovo aktiviraj deaktiviranog korisnika");

        group.MapPost("/{id}/reset-lozinka",
            async (string id, [FromBody] ResetLozinkeInput input, IMediator m) =>
            {
                await m.Send(new ResetujLozinkuCommand(id, input.NovaLozinka));
                return Results.NoContent();
            })
            .WithSummary("Admin postavlja novu lozinku korisniku (revokuje sesije)");

        return app;
    }
}
