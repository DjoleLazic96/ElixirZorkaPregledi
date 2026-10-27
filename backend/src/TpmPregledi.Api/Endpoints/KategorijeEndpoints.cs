using TpmPregledi.Application.Common.Pagination;
using TpmPregledi.Application.Features.Kategorije.Commands;
using TpmPregledi.Application.Features.Kategorije.Dtos;
using TpmPregledi.Application.Features.Kategorije.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// PRIMER feature-a — kopiraj ili obriši. Šifarnik kategorija (<c>/api/v1/kategorije</c>):
/// čitanje <c>RequireKorisnik</c> na grupi, pisanje pooštreno na <c>RequireAdmin</c> po ruti.
/// Obrazac za svaki novi šifarnik.
/// </summary>
public static class KategorijeEndpoints
{
    /// <summary>Mapira <c>/api/v1/kategorije/*</c>.</summary>
    public static IEndpointRouteBuilder MapKategorijeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kategorije")
            .WithTags("Kategorije")
            .RequireAuthorization("RequireKorisnik");

        group.MapGet("",
            async (
                [FromQuery] int? stranica,
                [FromQuery] int? velicina,
                [FromQuery] string? pretraga,
                [FromQuery] string? sort,
                [FromQuery] string? smer,
                [FromQuery] bool? aktivan,
                IMediator m) =>
                Results.Ok(await m.Send(new GetKategorijeQuery(
                    Stranica: stranica ?? 1,
                    Velicina: velicina ?? StranicaDefaults.Default,
                    Pretraga: pretraga,
                    Sort:     sort,
                    Smer:     smer,
                    Aktivan:  aktivan))))
            .WithSummary("Paginirana lista kategorija: pretraga (šifra/naziv), sort sifra|naziv, smer, aktivan");

        group.MapGet("/{id:int}",
            async (int id, IMediator m) =>
                Results.Ok(await m.Send(new GetKategorijaQuery(id))))
            .WithSummary("Jedna kategorija po ID-u");

        group.MapPost("",
            async ([FromBody] KategorijaInput input, IMediator m) =>
            {
                var dto = await m.Send(new KreirajKategorijuCommand(input.Sifra, input.Naziv, input.Opis));
                return Results.Created($"/api/v1/kategorije/{dto.Id}", dto);
            })
            .RequireAuthorization("RequireAdmin")
            .WithSummary("Kreiraj kategoriju (422 ako aktivna šifra već postoji)");

        group.MapPut("/{id:int}",
            async (int id, [FromBody] KategorijaInput input, IMediator m) =>
                Results.Ok(await m.Send(new IzmeniKategorijuCommand(id, input.Sifra, input.Naziv, input.Opis))))
            .RequireAuthorization("RequireAdmin")
            .WithSummary("Izmeni kategoriju (cela izmena)");

        group.MapPost("/{id:int}/deaktiviraj",
            async (int id, IMediator m) =>
                Results.Ok(await m.Send(new DeaktivirajKategorijuCommand(id))))
            .RequireAuthorization("RequireAdmin")
            .WithSummary("Deaktiviraj kategoriju (soft delete)");

        group.MapPost("/{id:int}/aktiviraj",
            async (int id, IMediator m) =>
                Results.Ok(await m.Send(new AktivirajKategorijuCommand(id))))
            .RequireAuthorization("RequireAdmin")
            .WithSummary("Ponovo aktiviraj kategoriju");

        return app;
    }
}
