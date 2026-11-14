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

    /// <summary>Naziv neradnog dana (za poruku zašto DP ne može tog dana, zamerka 69).</summary>
    public IReadOnlyDictionary<DateOnly, string> NaziviNeradnih { get; private init; } = new Dictionary<DateOnly, string>();

    /// <summary>Subote i nedelje unete kao radni dan (zamerka 68).</summary>
    public IReadOnlySet<DateOnly> RadniVikend { get; private init; } = new HashSet<DateOnly>();

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

        var dani = await db.NeradniDani.AsNoTracking().Select(n => new { n.Datum, n.Naziv, n.Vrsta }).ToListAsync(ct);
        return new RasporedKontekst
        {
            Neradni = dani.Where(n => n.Vrsta != NeradniDan.VrstaRadni).Select(n => n.Datum).ToHashSet(),
            NaziviNeradnih = dani.Where(n => n.Vrsta != NeradniDan.VrstaRadni).ToDictionary(n => n.Datum, n => n.Naziv),
            RadniVikend = dani.Where(n => n.Vrsta == NeradniDan.VrstaRadni).Select(n => n.Datum).ToHashSet(),
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

    /// <summary>Radni dan (uključujući subotu ili nedelju unetu kao radni dan).</summary>
    public bool JeRadniDan(DateOnly d) => RasporedPravila.JeRadniDan(d, Neradni, RadniVikend);

    /// <summary>Da li DP sme tog dana — za ručni unos i izmenu, pa važi i radni vikend dan (zamerka 68).</summary>
    public bool JeDanZaDp(DateOnly d) => RasporedPravila.JeDanZaDp(d, Neradni, DaniPz, DpNaDanPz, RadniVikend);

    /// <summary>
    /// Dan koji raspored sam popunjava (predlog, „rupa usred niza“): kao <see cref="JeDanZaDp"/>, ali bez radnih vikend dana —
    /// na njih DP ide samo ručno (zamerka 68).
    /// </summary>
    public bool JeRedovanDanZaDp(DateOnly d) => RasporedPravila.JeDanZaDp(d, Neradni, DaniPz, DpNaDanPz);

    private static readonly string[] NaziviDana = ["nedelja", "ponedeljak", "utorak", "sreda", "četvrtak", "petak", "subota"];

    /// <summary>Zašto DP ne može tog dana (zamerka 69) — konkretan razlog umesto opšte poruke.</summary>
    public string ZastoNijeDanZaDp(DateOnly d) =>
        NaziviNeradnih.TryGetValue(d, out var naziv)
            ? $"{d:dd.MM.yyyy} je neradni dan ({naziv}) — tada se DP ne radi."
            : !JeRadniDan(d)
                ? $"{d:dd.MM.yyyy} je {NaziviDana[(int)d.DayOfWeek]} — DP se radi samo radnim danima. Ako se tog dana radi, Administrator ili Direktor ga unosi kao radni dan (Neradni dani)."
                : $"{d:dd.MM.yyyy} je dan PZ-a — tada se DP ne radi (podešavanje „DP na dan PZ-a“).";

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
