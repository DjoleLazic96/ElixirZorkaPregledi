using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Sabloni;

/// <summary>Šablon na spisku: važeća verzija i obim.</summary>
/// <param name="Tip">DP1…PZ4.</param>
/// <param name="VazecaVerzija">Broj objavljene verzije; null ako šablon još nije uvezen.</param>
/// <param name="VaziOd">Od kada važi.</param>
/// <param name="BrojStavki">Broj stavki važeće verzije.</param>
/// <param name="BrojUredjaja">Broj uređaja u važećoj verziji.</param>
public sealed record SablonPregledDto(string Tip, int? VazecaVerzija, DateOnly? VaziOd, int BrojStavki, int BrojUredjaja);

/// <summary>Verzija šablona kao na obrascu: sekcije → uređaji → aktivnosti.</summary>
/// <param name="Tip">DP1…PZ4.</param>
/// <param name="Broj">Broj verzije.</param>
/// <param name="Status">Status verzije.</param>
/// <param name="VaziOd">Od kada važi.</param>
/// <param name="Sekcije">Sekcije redom kao na obrascu.</param>
public sealed record SablonVerzijaDto(string Tip, int Broj, string Status, DateOnly? VaziOd, IReadOnlyList<SekcijaUSablonuDto> Sekcije);

/// <summary>Sekcija u šablonu.</summary>
/// <param name="Sifra">„10“…„70“.</param>
/// <param name="Uredjaji">Uređaji redom kao na obrascu.</param>
public sealed record SekcijaUSablonuDto(string Sifra, IReadOnlyList<UredjajUSablonuDto> Uredjaji);

/// <summary>Uređaj u šablonu.</summary>
/// <param name="UredjajId">ID iz registra.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Oznaka">Oznaka.</param>
/// <param name="Stavke">Aktivnosti redom kao na obrascu.</param>
public sealed record UredjajUSablonuDto(int UredjajId, string Naziv, string Oznaka, IReadOnlyList<StavkaDto> Stavke);

/// <summary>Stavka: aktivnost sa dva odgovora i dodatnim poljima.</summary>
/// <param name="StavkaId">ID stavke u ovoj verziji.</param>
/// <param name="Kljuc">Stalni ID stavke kroz verzije.</param>
/// <param name="AktivnostId">ID aktivnosti.</param>
/// <param name="Aktivnost">Naziv aktivnosti.</param>
/// <param name="OdgovorIspravan">Odgovor „u redu“.</param>
/// <param name="OdgovorNeispravan">Odgovor „kvar“.</param>
/// <param name="Polja">Nazivi dodatnih polja.</param>
public sealed record StavkaDto(int StavkaId, Guid Kljuc, int AktivnostId, string Aktivnost, string OdgovorIspravan, string OdgovorNeispravan, IReadOnlyList<string> Polja);

/// <summary>Spisak šest šablona.</summary>
public sealed record GetSabloniQuery : IRequest<IReadOnlyList<SablonPregledDto>>;

/// <summary>Jedna verzija šablona; bez broja = važeća.</summary>
/// <param name="Tip">DP1…PZ4.</param>
/// <param name="Broj">Broj verzije ili null za važeću.</param>
public sealed record GetSablonVerzijaQuery(string Tip, int? Broj = null) : IRequest<SablonVerzijaDto>;

/// <summary>Čitanje šablona.</summary>
public sealed class SabloniQueriesHandler(IAppDbContext db) :
    IRequestHandler<GetSabloniQuery, IReadOnlyList<SablonPregledDto>>,
    IRequestHandler<GetSablonVerzijaQuery, SablonVerzijaDto>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SablonPregledDto>> Handle(GetSabloniQuery _, CancellationToken ct)
    {
        var vazece = await db.SablonVerzije.AsNoTracking()
            .Where(v => v.Status == StatusVerzije.Objavljena)
            .Select(v => new
            {
                v.Sablon!.Tip, v.Broj, v.VaziOd,
                Stavki = v.Stavke.Count,
                Uredjaja = v.Stavke.Select(s => s.UredjajId).Distinct().Count(),
            })
            .ToListAsync(ct);

        return Sablon.Tipovi.Select(t => vazece.FirstOrDefault(v => v.Tip == t) is { } v
                ? new SablonPregledDto(t, v.Broj, v.VaziOd, v.Stavki, v.Uredjaja)
                : new SablonPregledDto(t, null, null, 0, 0))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SablonVerzijaDto> Handle(GetSablonVerzijaQuery req, CancellationToken ct)
    {
        var upit = db.SablonVerzije.AsNoTracking().Where(v => v.Sablon!.Tip == req.Tip);
        upit = req.Broj is { } broj ? upit.Where(v => v.Broj == broj) : upit.Where(v => v.Status == StatusVerzije.Objavljena);
        var verzija = await upit.FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException(req.Broj is null ? $"Šablon {req.Tip} još nije uvezen." : "Verzija šablona ne postoji.");

        var stavke = await db.SablonStavke.AsNoTracking()
            .Where(s => s.VerzijaId == verzija.Id)
            .OrderBy(s => s.Redosled)
            .Select(s => new
            {
                s.Id, s.Kljuc, s.Redosled, s.UredjajId, s.AktivnostId,
                Sekcija = db.Sekcije.Where(x => x.Id == s.SekcijaId).Select(x => x.Sifra).First(),
                s.Uredjaj!.Naziv, s.Uredjaj.Oznaka,
                Aktivnost = s.Aktivnost!.Naziv, s.Aktivnost.OdgovorIspravan, s.Aktivnost.OdgovorNeispravan,
                Polja = s.Polja.Select(p => p.DodatnoPolje!.Naziv).ToList(),
            })
            .ToListAsync(ct);

        // Grupisanje čuva redosled sa obrasca: sekcija i uređaj se pojavljuju gde im je prva stavka.
        var sekcije = new List<SekcijaUSablonuDto>();
        foreach (var gs in stavke.GroupBy(s => s.Sekcija))
        {
            var uredjaji = gs.GroupBy(s => s.UredjajId)
                .Select(gu => new UredjajUSablonuDto(gu.Key, gu.First().Naziv, gu.First().Oznaka,
                    gu.Select(s => new StavkaDto(s.Id, s.Kljuc, s.AktivnostId, s.Aktivnost, s.OdgovorIspravan, s.OdgovorNeispravan, s.Polja)).ToList()))
                .ToList();
            sekcije.Add(new SekcijaUSablonuDto(gs.Key, uredjaji));
        }

        return new SablonVerzijaDto(req.Tip, verzija.Broj, verzija.Status, verzija.VaziOd, sekcije);
    }
}
