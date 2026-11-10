using System.Text.Json;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Obavestenja;

/// <summary>
/// Upis obaveštenja iz bilo kog handlera ili posla (kao <c>PregledIstorija.Dodaj</c>): samo dodaje redove u kontekst, a pozivalac
/// snima zajedno sa svojom promenom. Push i mejl šalje posao isporuke.
/// </summary>
public static class Obavesti
{
    /// <summary>Ključ u <c>podesavanje</c>: <c>{ vrsta: [korisnikId] }</c>. Vrsta koje nema koristi podrazumevane primaoce.</summary>
    public const string KljucPrimaoci = "primaociObavestenja";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Dodaje obaveštenje svakom korisniku jednom. Uz <paramref name="kljuc"/> preskače korisnike koji ga već imaju — zakazane provere
    /// mogu da se pokreću svakog minuta bez dupliranja.
    /// </summary>
    public static async Task<int> DodajAsync(IAppDbContext db, IEnumerable<string?> korisnici, string vrsta, string naslov, string tekst, string? link,
        string? kljuc, DateTime sada, CancellationToken ct)
    {
        var ids = korisnici.Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).Distinct().ToList();
        if (ids.Count == 0) return 0;
        if (kljuc is not null)
        {
            var vec = await db.Obavestenja.Where(o => o.Kljuc == kljuc && ids.Contains(o.KorisnikId)).Select(o => o.KorisnikId).ToListAsync(ct);
            vec.AddRange(db.Obavestenja.Local.Where(o => o.Kljuc == kljuc).Select(o => o.KorisnikId));
            ids = ids.Except(vec).ToList();
        }
        foreach (var id in ids)
            db.Obavestenja.Add(new Obavestenje { KorisnikId = id, Vrsta = vrsta, Naslov = naslov, Tekst = tekst, Link = link, Kljuc = kljuc, KreiranoAt = sada });
        return ids.Count;
    }

    /// <summary>Podešeni primaoci po vrsti (sirovo, kako je Admin sačuvao).</summary>
    public static async Task<Dictionary<string, List<string>>> PodeseniAsync(IAppDbContext db, CancellationToken ct)
    {
        var red = await db.Podesavanja.AsNoTracking().FirstOrDefaultAsync(p => p.Kljuc == KljucPrimaoci, ct);
        return red is null ? [] : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(red.Vrednost, Json) ?? [];
    }

    /// <summary>
    /// Aktivni primaoci za vrstu (R9.4). Dok Admin ne sačuva izbor, podrazumevano su Supervizor i Direktor — to su korisnici
    /// sa pravom overe, koje podrazumevano imaju samo te dve role.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PrimaociAsync(IAppDbContext db, string vrsta, CancellationToken ct)
    {
        var podeseni = await PodeseniAsync(db, ct);
        var aktivni = db.Users.Where(u => u.IsActive);
        if (podeseni.TryGetValue(vrsta, out var ids))
            return await aktivni.Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        var saOverom = db.KorisnikPrava.Where(p => p.Pravo == Prava.Overa).Select(p => p.KorisnikId);
        return await aktivni.Where(u => saOverom.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
    }

    /// <summary>Isto što i <see cref="DodajAsync"/>, za primaoce vrste (uz dodatne, npr. dodeljenog inženjera).</summary>
    public static async Task<int> PrimaocimaAsync(IAppDbContext db, string vrsta, string naslov, string tekst, string? link, string? kljuc, DateTime sada,
        CancellationToken ct, params string?[] dodatni) =>
        await DodajAsync(db, (await PrimaociAsync(db, vrsta, ct)).Concat(dodatni), vrsta, naslov, tekst, link, kljuc, sada, ct);
}
