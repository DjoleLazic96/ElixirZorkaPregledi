using TpmPregledi.Application.Features.Korisnici.Commands;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Application.Features.Korisnici.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// Upravljanje korisnicima (<c>/api/v1/korisnici</c>, Admin). Korisnici se nikad ne brišu —
/// nema DELETE rute, samo <c>deaktiviraj</c>/<c>aktiviraj</c>. Lozinka ide isključivo u telo zahteva.
/// </summary>
public static class KorisniciEndpoints
{
    /// <summary>Mapira <c>/api/v1/korisnici/*</c>.</summary>
    public static IEndpointRouteBuilder MapKorisniciEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/korisnici")
            .WithTags("Korisnici")
            .RequireAuthorization("RequireAdmin");

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

        group.MapPost("/{id}/deaktiviraj",
            async (string id, IMediator m) =>
                Results.Ok(await m.Send(new DeaktivirajKorisnikaCommand(id))))
            .WithSummary("Deaktiviraj korisnika (blokira prijavu, revokuje sesije)");

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
