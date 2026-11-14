using System.Text.Json;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Raspored;

/// <summary>
/// Sve što pravila rasporeda traže iz baze, učitano jednom po zahtevu: neradni dani, dani PZ-a, inženjeri u rotaciji,
/// odobrena odsustva i podešavanje „DP na dan PZ-a“. Zajedničko za predlog, potvrdu, prenos, PZ i odsustva.
/// </summary>
public sealed class RasporedKontekst
{
    private RasporedKontekst() { }

    /// <summary>Neradni dani.</summary>
    public IReadOnlySet<DateOnly> Neradni { get; private init; } = new HashSet<DateOnly>();

    /// <summary>Dani na koje je zakazan (neponišten) PZ.</summary>
    public IReadOnlySet<DateOnly> DaniPz { get; private init; } = new HashSet<DateOnly>();

    /// <summary>Da li se DP radi na dan PZ-a (R3.9).</summary>
    public bool DpNaDanPz { get; private init; }

    /// <summary>Inženjeri u rotaciji (aktivni, sa pravom popunjavanja), po imenu.</summary>
    public IReadOnlyList<string> Inzenjeri { get; private init; } = [];

    /// <summary>Ime i prezime po ID-u (svi korisnici, za prikaz).</summary>
    public IReadOnlyDictionary<string, string> Imena { get; private init; } = new Dictionary<string, string>();

    /// <summary>Odobrena odsustva.</summary>
    public IReadOnlyList<Odsustvo> Odsustva { get; private init; } = [];

    /// <summary>Pregledi za koje zahtev inženjera (prebacivanje ili raniji početak) čeka odluku: pregled → vrsta zahteva.</summary>
    public IReadOnlyDictionary<int, string> ZahteviNaCekanju { get; private init; } = new Dictionary<int, string>();

    /// <summary>Nazivi vrsta odsustva za poruke.</summary>
    public static readonly IReadOnlyDictionary<string, string> NaziviOdsustva = new Dictionary<string, string>
    {
        ["godisnji"] = "Godišnji odmor", ["bolovanje"] = "Bolovanje", ["slobodan_dan"] = "Slobodan dan",
        ["sluzbeni_put"] = "Službeni put", ["ostalo"] = "Ostalo",
    };

    /// <summary>Učitava kontekst.</summary>
    public static async Task<RasporedKontekst> UcitajAsync(IAppDbContext db, CancellationToken ct)
    {
        var podesavanja = await new PodesavanjaHandler(db).Handle(new GetPodesavanjaQuery(), ct);
        var saPravom = db.KorisnikPrava.Where(p => p.Pravo == Prava.PreglediPopunjavanje).Select(p => p.KorisnikId);
        var korisnici = await db.Users.AsNoTracking().Select(u => new { u.Id, u.FullName, u.IsActive }).ToListAsync(ct);
        var uRotaciji = await db.Users.AsNoTracking().Where(u => u.IsActive && saPravom.Contains(u.Id))
            .OrderBy(u => u.FullName).ThenBy(u => u.UserName).Select(u => u.Id).ToListAsync(ct);

        return new RasporedKontekst
        {
            Neradni = (await db.NeradniDani.AsNoTracking().Select(n => n.Datum).ToListAsync(ct)).ToHashSet(),
            DaniPz = (await db.Pregledi.AsNoTracking()
                .Where(p => p.Tip.StartsWith("PZ") && p.Status != StatusPregleda.Ponisten)
                .Select(p => p.PlaniraniDatum).ToListAsync(ct)).ToHashSet(),
            DpNaDanPz = podesavanja.DpNaDanPz,
            Inzenjeri = uRotaciji,
            Imena = korisnici.ToDictionary(k => k.Id, k => k.FullName),
            Odsustva = await db.Odsustva.AsNoTracking().Where(o => o.Status == "odobreno").ToListAsync(ct),
            ZahteviNaCekanju = (await db.ZahteviPrebacivanja.AsNoTracking().Where(z => z.Status == "zahtev").Select(z => new { z.PregledId, z.Vrsta }).ToListAsync(ct))
                .GroupBy(z => z.PregledId).ToDictionary(g => g.Key, g => g.First().Vrsta),
        };
    }

    /// <summary>Da li je inženjer tog dana na odobrenom odsustvu.</summary>
    public bool JeOdsutan(string korisnikId, DateOnly d) => Odsustva.Any(o => o.KorisnikId == korisnikId && o.Pokriva(d));

    /// <summary>
    /// Ručno izabran inženjer ne sme biti odsutan tog dana (zamerka 2, R3.18) — inače 422 sa imenom, danom i vrstom odsustva.
    /// Predlog rasporeda odsutne već preskače; ovo pokriva ručni izbor (PZ, izmena, drugi inženjer, zamena, potvrda predloga).
    /// </summary>
    public void ProveriDostupnost(string? id, DateOnly d)
    {
        var o = id is null ? null : Odsustva.FirstOrDefault(x => x.KorisnikId == id && x.Pokriva(d));
        if (o is not null) throw new BusinessRuleException(PorukaOdsutan(id!, d, o));
    }

    /// <summary>„Ana Anić je odsutna 10.11.2026 (Godišnji odmor). Izaberite drugog inženjera.“</summary>
    public string PorukaOdsutan(string id, DateOnly d, Odsustvo o) =>
        $"{Ime(id)} je na odsustvu {d:dd.MM.yyyy} ({NaziviOdsustva.GetValueOrDefault(o.Vrsta, o.Vrsta)}). Izaberite drugog inženjera.";

    /// <summary>Da li se tog dana radi DP.</summary>
    public bool JeDanZaDp(DateOnly d) => RasporedPravila.JeDanZaDp(d, Neradni, DaniPz, DpNaDanPz);

    /// <summary>Prvi dan za DP posle datog.</summary>
    public DateOnly SledeciDanZaDp(DateOnly posle) => RasporedPravila.SledeciDanZaDp(posle, Neradni, DaniPz, DpNaDanPz);

    /// <summary>Sledeći dostupan inženjer po rotaciji za dan; ako niko nije dostupan, sledeći po redu (Supervizor menja ručno).</summary>
    public string? SledeciInzenjer(DateOnly d, string? poslednji) =>
        RasporedPravila.SledeciInzenjer(Inzenjeri, poslednji, k => !JeOdsutan(k, d))
        ?? RasporedPravila.SledeciInzenjer(Inzenjeri, poslednji, _ => true);

    /// <summary>Ime za prikaz.</summary>
    public string Ime(string? id) => id is not null && Imena.TryGetValue(id, out var i) ? i : string.Empty;

    /// <summary>Provera da je korisnik u rotaciji (aktivan, sme da popunjava) — inače 422.</summary>
    public void ProveriInzenjera(string? id)
    {
        if (id is null || !Inzenjeri.Contains(id))
            throw new BusinessRuleException("Izabrani korisnik nije aktivan inženjer sa pravom popunjavanja pregleda.");
    }
}

/// <summary>Upis događaja u istoriju pregleda (R6.7).</summary>
public static class PregledIstorija
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Dodaje događaj preko navigacije, pa radi i za nov pregled bez ID-a (snima pozivalac).</summary>
    public static void Dodaj(IAppDbContext db, Pregled p, string vrsta, object podaci, string? korisnikId, DateTime at) =>
        db.PregledDogadjaji.Add(new PregledDogadjaj
        {
            Pregled = p, Vrsta = vrsta, Podaci = JsonSerializer.Serialize(podaci, Json), KorisnikId = korisnikId, At = at,
        });
}
