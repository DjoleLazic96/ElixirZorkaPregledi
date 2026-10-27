using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Common.Pagination;
using TpmPregledi.Application.Features.Kategorije.Dtos;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TpmPregledi.Application.Features.Kategorije.Queries;

/// <summary>
/// PRIMER feature-a — kopiraj ili obriši. Paginirana lista kategorija sa pretragom, sortom i
/// filterom statusa. Obrazac za svaku listu koja može da naraste.
/// </summary>
/// <param name="Stranica">1-bazirana stranica (default 1).</param>
/// <param name="Velicina">Veličina stranice (default 50, max 200).</param>
/// <param name="Pretraga">Contains, case-insensitive, nad šifrom i nazivom.</param>
/// <param name="Sort">Whitelist: <c>sifra</c> (default) ili <c>naziv</c>; nepoznato se tiho ignoriše.</param>
/// <param name="Smer"><c>asc</c> (default za šifarnik) ili <c>desc</c>.</param>
/// <param name="Aktivan">Filter po statusu; null = sve (admin ekran „Prikaži i neaktivne“).</param>
public sealed record GetKategorijeQuery(
    int Stranica = 1,
    int Velicina = StranicaDefaults.Default,
    string? Pretraga = null,
    string? Sort = null,
    string? Smer = null,
    bool? Aktivan = null)
    : IRequest<StranicaDto<KategorijaDto>>;

/// <summary>Filter → count → sort (whitelist + stabilan tiebreak po Id) → stranica → projekcija.</summary>
public sealed class GetKategorijeHandler(IAppDbContext db, ILogger<GetKategorijeHandler> logger)
    : IRequestHandler<GetKategorijeQuery, StranicaDto<KategorijaDto>>
{
    /// <inheritdoc />
    public async Task<StranicaDto<KategorijaDto>> Handle(GetKategorijeQuery req, CancellationToken ct)
    {
        var (stranica, velicina) = StranicaDefaults.Normalizuj(req.Stranica, req.Velicina);

        var upit = db.Kategorije.AsNoTracking();

        if (req.Aktivan is { } aktivan)
            upit = upit.Where(k => k.Aktivan == aktivan);

        if (!string.IsNullOrWhiteSpace(req.Pretraga))
        {
            // ToLower().Contains umesto EF.Functions.ILike: radi na Npgsql-u i na InMemory
            // provideru, a Application sloj ne sme da zavisi od Npgsql-a.
            var term = req.Pretraga.Trim().ToLower();
            upit = upit.Where(k => k.Sifra.ToLower().Contains(term) || k.Naziv.ToLower().Contains(term));
        }

        var ukupno = await upit.CountAsync(ct);

        var stavke = await Sortiraj(upit, req.Sort, req.Smer)
            .Skip((stranica - 1) * velicina)
            .Take(velicina)
            .Select(k => new KategorijaDto(k.Id, k.Sifra, k.Naziv, k.Opis, k.Aktivan, k.CreatedAt, k.UpdatedAt))
            .ToListAsync(ct);

        return new StranicaDto<KategorijaDto>(stavke, ukupno, stranica, velicina);
    }

    /// <summary>
    /// Whitelist sorta u <c>switch</c>-u: nepoznata vrednost ne obara prikaz (tipo na frontu),
    /// samo se zabeleži na Debug nivou i padne na podrazumevani sort.
    /// </summary>
    private IOrderedQueryable<Kategorija> Sortiraj(IQueryable<Kategorija> upit, string? sort, string? smer)
    {
        var opadajuce = string.Equals(smer, "desc", StringComparison.OrdinalIgnoreCase);
        var kolona    = sort?.Trim().ToLowerInvariant();

        if (kolona is not (null or "" or "sifra" or "naziv"))
        {
            logger.LogDebug("Nepoznat sort parametar {Sort} za kategorije — koristim podrazumevani.", sort);
            kolona = "sifra";
        }

        return (kolona, opadajuce) switch
        {
            ("naziv", true)  => upit.OrderByDescending(k => k.Naziv).ThenByDescending(k => k.Id),
            ("naziv", false) => upit.OrderBy(k => k.Naziv).ThenBy(k => k.Id),
            (_, true)        => upit.OrderByDescending(k => k.Sifra).ThenByDescending(k => k.Id),
            _                => upit.OrderBy(k => k.Sifra).ThenBy(k => k.Id),
        };
    }
}
