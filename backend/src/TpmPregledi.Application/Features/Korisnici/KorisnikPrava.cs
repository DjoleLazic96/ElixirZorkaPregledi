using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Korisnici;

/// <summary>Čitanje i zamena štikliranih prava korisnika (tabela <c>korisnik_pravo</c>).</summary>
public static class KorisnikPrava
{
    /// <summary>Prava jednog korisnika, redom kao u <see cref="Prava.Sva"/>.</summary>
    public static async Task<IReadOnlyList<string>> ProcitajAsync(IAppDbContext db, string korisnikId, CancellationToken ct)
    {
        var skup = await db.KorisnikPrava.AsNoTracking()
            .Where(p => p.KorisnikId == korisnikId).Select(p => p.Pravo).ToListAsync(ct);
        return Uredi(skup);
    }

    /// <summary>Prava svih korisnika odjednom (lista korisnika, bez N+1).</summary>
    public static async Task<Dictionary<string, IReadOnlyList<string>>> ProcitajSveAsync(IAppDbContext db, CancellationToken ct)
    {
        var redovi = await db.KorisnikPrava.AsNoTracking().ToListAsync(ct);
        return redovi.GroupBy(p => p.KorisnikId)
                     .ToDictionary(g => g.Key, g => Uredi(g.Select(p => p.Pravo)));
    }

    /// <summary>Zamenjuje skup prava; ne snima — snima pozivalac.</summary>
    public static async Task ZameniAsync(IAppDbContext db, string korisnikId, IEnumerable<string> prava, CancellationToken ct)
    {
        var novi = prava.ToHashSet();
        var postojeci = await db.KorisnikPrava.Where(p => p.KorisnikId == korisnikId).ToListAsync(ct);

        db.KorisnikPrava.RemoveRange(postojeci.Where(p => !novi.Contains(p.Pravo)));
        foreach (var p in novi.Except(postojeci.Select(x => x.Pravo)))
            db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = korisnikId, Pravo = p });
    }

    private static IReadOnlyList<string> Uredi(IEnumerable<string> prava)
    {
        var skup = prava.ToHashSet();
        return Prava.Sva.Where(skup.Contains).ToList();
    }
}
