using TpmPregledi.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Analitika;

/// <summary>Rezultat pretrage u zaglavlju (R8.14).</summary>
/// <param name="Vrsta"><c>pregled</c>, <c>akcija</c>, <c>kvar</c>, <c>uredjaj</c>.</param>
/// <param name="Id">ID za link.</param>
/// <param name="Tekst">Tekst u kom je pronađen pojam (frontend ga žuto obeležava).</param>
/// <param name="Opis">Dodatni opis.</param>
public sealed record PretragaRezultatDto(string Vrsta, int Id, string Tekst, string? Opis);

/// <summary>Pretraga po broju pregleda, akcije ili kvara i delu naziva ili oznake uređaja.</summary>
/// <param name="Q">Pojam (najmanje 2 znaka).</param>
public sealed record PretragaQuery(string? Q) : IRequest<IReadOnlyList<PretragaRezultatDto>>;

/// <summary>Pretraga.</summary>
public sealed class PretragaHandler(IAppDbContext db) : IRequestHandler<PretragaQuery, IReadOnlyList<PretragaRezultatDto>>
{
    /// <summary>Najviše rezultata po vrsti.</summary>
    public const int PoVrsti = 8;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PretragaRezultatDto>> Handle(PretragaQuery req, CancellationToken ct)
    {
        var q = req.Q?.Trim().ToLower() ?? string.Empty;
        if (q.Length < 2) return [];

        var uredjaji = (await db.Uredjaji.AsNoTracking()
                .Where(u => u.Naziv.ToLower().Contains(q) || u.Oznaka.ToLower().Contains(q) || (u.Naziv + " " + u.Oznaka).ToLower().Contains(q))
                .OrderBy(u => u.Naziv).ThenBy(u => u.Oznaka).Take(PoVrsti)
                .Select(u => new { u.Id, u.Naziv, u.Oznaka, Sekcija = u.Sekcija!.Sifra }).ToListAsync(ct))
            .Select(u => new PretragaRezultatDto("uredjaj", u.Id, string.IsNullOrEmpty(u.Oznaka) ? u.Naziv : $"{u.Naziv} {u.Oznaka}", $"Sekcija {u.Sekcija}"));
        var pregledi = (await db.Pregledi.AsNoTracking().Where(p => p.Broj.ToLower().Contains(q))
                .OrderByDescending(p => p.PlaniraniDatum).Take(PoVrsti).Select(p => new { p.Id, p.Broj, p.PlaniraniDatum }).ToListAsync(ct))
            .Select(p => new PretragaRezultatDto("pregled", p.Id, p.Broj, p.PlaniraniDatum.ToString("dd.MM.yyyy")));
        var akcije = await db.Akcije.AsNoTracking().Where(a => a.Broj.ToLower().Contains(q))
            .OrderByDescending(a => a.Id).Take(PoVrsti).Select(a => new PretragaRezultatDto("akcija", a.Id, a.Broj, a.Opis)).ToListAsync(ct);
        var kvarovi = await db.Kvarovi.AsNoTracking().Where(x => x.Broj.ToLower().Contains(q))
            .OrderByDescending(x => x.Id).Take(PoVrsti).Select(x => new PretragaRezultatDto("kvar", x.Id, x.Broj, x.PoslednjaNapomena)).ToListAsync(ct);

        return [.. uredjaji, .. pregledi, .. akcije, .. kvarovi];
    }
}
