using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Sabloni;

/// <summary>Jedan red lista „Stavke“ fajla za uvoz (kolone su opisane u planu faze 02).</summary>
/// <param name="Red">Broj reda u Excelu (za poruke o grešci).</param>
/// <param name="Sablon">DP1…PZ4.</param>
/// <param name="Sekcija">Šifra sekcije.</param>
/// <param name="Redosled">Redosled u šablonu.</param>
/// <param name="Naziv">Naziv uređaja.</param>
/// <param name="Oznaka">Oznaka uređaja (može biti prazna).</param>
/// <param name="Aktivnost">Naziv aktivnosti.</param>
/// <param name="OdgovorIspravan">Odgovor „u redu“.</param>
/// <param name="OdgovorNeispravan">Odgovor „kvar“.</param>
/// <param name="Polje">Naziv dodatnog polja (prazno = bez polja).</param>
public sealed record UvozRed(
    int Red, string Sablon, string Sekcija, int? Redosled, string Naziv, string Oznaka,
    string Aktivnost, string OdgovorIspravan, string OdgovorNeispravan, string Polje);

/// <summary>Čita list „Stavke“ iz <c>.xlsx</c>; implementacija (ClosedXML) je u Infrastructure.</summary>
public interface ISablonUvozCitac
{
    /// <summary>Svi neprazni redovi lista „Stavke“, bez zaglavlja. Neispravan fajl → <see cref="BusinessRuleException"/>.</summary>
    IReadOnlyList<UvozRed> Procitaj(Stream xlsx);
}

/// <summary>Rezultat uvoza po šablonu.</summary>
/// <param name="Tip">DP1…PZ4.</param>
/// <param name="Stavki">Uvezeno stavki.</param>
/// <param name="Uredjaja">Uređaja u šablonu.</param>
public sealed record UvozSablonaDto(string Tip, int Stavki, int Uredjaja);

/// <summary>Rezultat celog uvoza.</summary>
/// <param name="Sabloni">Po šablonu.</param>
/// <param name="NovihUredjaja">Koliko je uređaja dodato u registar.</param>
/// <param name="NovihAktivnosti">Koliko je aktivnosti dodato u katalog.</param>
public sealed record UvozRezultatDto(IReadOnlyList<UvozSablonaDto> Sabloni, int NovihUredjaja, int NovihAktivnosti);

/// <summary>
/// Prvi uvoz šablona iz <c>.xlsx</c> (R2.15, Admin). Pravi verziju 1 (objavljena, važi od danas) svakog šablona iz
/// fajla, popunjava registar opreme i katalog aktivnosti i upisuje „Prvi uvoz“ u istorijat. Sve ili ništa: jedna
/// greška u bilo kom redu ne upisuje ništa, a odgovor nabraja sve greške sa brojem reda.
/// </summary>
/// <param name="Xlsx">Sadržaj fajla.</param>
public sealed record UvoziSabloneCommand(Stream Xlsx) : IRequest<UvozRezultatDto>;

/// <summary>Validacija svih redova, pa jedan <c>SaveChanges</c> (jedna transakcija).</summary>
public sealed class UvoziSabloneHandler(
    IAppDbContext db, ISablonUvozCitac citac, ICurrentUser currentUser, IDateTimeProvider clock)
    : IRequestHandler<UvoziSabloneCommand, UvozRezultatDto>
{
    /// <summary>Više grešaka od ovoga se ne nabraja — fajl je verovatno pogrešan.</summary>
    private const int NajviseGresaka = 50;

    /// <inheritdoc />
    public async Task<UvozRezultatDto> Handle(UvoziSabloneCommand req, CancellationToken ct)
    {
        var redovi = citac.Procitaj(req.Xlsx);
        if (redovi.Count == 0)
            throw new BusinessRuleException("List „Stavke“ nema nijedan red za uvoz.");

        var sekcije = await db.Sekcije.ToDictionaryAsync(s => s.Sifra, ct);
        var polja = await db.DodatnaPolja.ToDictionaryAsync(p => p.Naziv, ct);
        var sabloni = await db.Sabloni.ToDictionaryAsync(s => s.Tip, ct);

        var greske = Proveri(redovi, sekcije, polja, sabloni);

        var tipovi = redovi.Select(r => r.Sablon).Where(sabloni.ContainsKey).Distinct().ToList();
        var vecUvezeni = await db.SablonVerzije.Where(v => tipovi.Contains(v.Sablon!.Tip))
            .Select(v => v.Sablon!.Tip).Distinct().ToListAsync(ct);
        greske.InsertRange(0, vecUvezeni.OrderBy(t => t).Select(t => $"Šablon {t} je već uvezen; izmene idu kroz nacrt."));

        if (greske.Count > 0)
            throw new BusinessRuleException(greske.Count > NajviseGresaka
                ? [.. greske.Take(NajviseGresaka), $"… i još {greske.Count - NajviseGresaka} grešaka."]
                : greske);

        // Registar i katalog: postojeći zapisi se koriste, novi se dodaju (isti uređaj iz više šablona = jedan red).
        var uredjaji = await db.Uredjaji.ToDictionaryAsync(u => (u.Naziv, u.Oznaka), ct);
        var aktivnosti = await db.Aktivnosti.ToDictionaryAsync(a => (a.Naziv, a.OdgovorIspravan, a.OdgovorNeispravan), ct);
        int noviU = 0, noviA = 0;
        var danas = clock.Today;
        var sada = clock.UtcNow;
        var rezultat = new List<UvozSablonaDto>();

        foreach (var tip in Sablon.Tipovi.Where(tipovi.Contains))
        {
            var verzija = new SablonVerzija
            {
                SablonId = sabloni[tip].Id, Broj = 1, Status = StatusVerzije.Objavljena,
                VaziOd = danas, ObjavioId = currentUser.UserId, ObjavljenoAt = sada,
            };
            var redoviSablona = redovi.Where(r => r.Sablon == tip).Select((r, i) => (r, i)).OrderBy(x => x.r.Redosled ?? int.MaxValue).ThenBy(x => x.i).Select(x => x.r).ToList();

            var redosled = 0;
            foreach (var r in redoviSablona)
            {
                if (!uredjaji.TryGetValue((r.Naziv, r.Oznaka), out var u))
                {
                    u = new Uredjaj { Naziv = r.Naziv, Oznaka = r.Oznaka, SekcijaId = sekcije[r.Sekcija].Id, Vrsta = VrstaIzNaziva(r.Naziv) };
                    uredjaji[(r.Naziv, r.Oznaka)] = u;
                    db.Uredjaji.Add(u);
                    noviU++;
                }
                if (!aktivnosti.TryGetValue((r.Aktivnost, r.OdgovorIspravan, r.OdgovorNeispravan), out var a))
                {
                    a = new Aktivnost { Naziv = r.Aktivnost, OdgovorIspravan = r.OdgovorIspravan, OdgovorNeispravan = r.OdgovorNeispravan };
                    aktivnosti[(r.Aktivnost, r.OdgovorIspravan, r.OdgovorNeispravan)] = a;
                    db.Aktivnosti.Add(a);
                    noviA++;
                }

                var stavka = new SablonStavka { SekcijaId = sekcije[r.Sekcija].Id, Uredjaj = u, Aktivnost = a, Redosled = ++redosled };
                if (r.Polje.Length > 0)
                    stavka.Polja.Add(new SablonStavkaPolje { DodatnoPolje = polja[r.Polje] });
                verzija.Stavke.Add(stavka);
            }

            db.SablonVerzije.Add(verzija);
            db.SablonDogadjaji.Add(new SablonDogadjaj
            {
                SablonId = sabloni[tip].Id, Vrsta = "prvi_uvoz", KorisnikId = currentUser.UserId, At = sada,
                Opis = $"Prvi uvoz iz Excela: {verzija.Stavke.Count} stavki, verzija 1 važi od {danas:dd.MM.yyyy}.",
            });
            rezultat.Add(new UvozSablonaDto(tip, verzija.Stavke.Count, redoviSablona.Select(r => (r.Naziv, r.Oznaka)).Distinct().Count()));
        }

        await db.SaveChangesAsync(ct);

        // Verzija dobija ID tek posle upisa — događaj se vezuje za nju u drugom koraku (ista operacija za korisnika).
        foreach (var d in db.SablonDogadjaji.Local.Where(d => d.Vrsta == "prvi_uvoz" && d.VerzijaId == null))
            d.VerzijaId = db.SablonVerzije.Local.First(v => v.SablonId == d.SablonId && v.Broj == 1).Id;
        await db.SaveChangesAsync(ct);

        return new UvozRezultatDto(rezultat, noviU, noviA);
    }

    /// <summary>Reči posle kojih vrsta ima i drugu reč („Merna vaga“, „Koš vage“, „Procesno sito“…).</summary>
    private static readonly HashSet<string> DvoclanaVrsta = ["Merna", "Koš", "Procesno", "Završno", "Završni", "Baterija", "Puž"];

    /// <summary>Vrsta uređaja pri uvozu: prva reč naziva, ili prve dve kad je prva samo odrednica. Menja se u registru.</summary>
    public static string VrstaIzNaziva(string naziv)
    {
        var reci = naziv.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return reci.Length > 1 && DvoclanaVrsta.Contains(reci[0]) && !reci[1].Contains('-') && !reci[1].StartsWith('(')
            ? $"{reci[0]} {reci[1]}"
            : reci.FirstOrDefault() ?? string.Empty;
    }

    /// <summary>Provera svakog reda; vraća poruke „Red N: …“ za korisnika.</summary>
    public static List<string> Proveri(
        IReadOnlyList<UvozRed> redovi, IDictionary<string, Sekcija> sekcije,
        IDictionary<string, DodatnoPolje> polja, IDictionary<string, Sablon> sabloni)
    {
        var greske = new List<string>();
        var stavke = new HashSet<(string, string, string, string)>();
        var sekcijaUredjaja = new Dictionary<(string, string), (string Sekcija, int Red)>();

        foreach (var r in redovi)
        {
            var g = new List<string>();
            if (!sabloni.ContainsKey(r.Sablon)) g.Add($"nepoznat šablon „{r.Sablon}“");
            if (!sekcije.ContainsKey(r.Sekcija)) g.Add($"nepoznata sekcija „{r.Sekcija}“");
            if (r.Naziv.Length == 0) g.Add("nedostaje naziv uređaja");
            if (r.Aktivnost.Length == 0) g.Add("nedostaje aktivnost");
            if (r.OdgovorIspravan.Length == 0 || r.OdgovorNeispravan.Length == 0) g.Add("nedostaje odgovor");
            else if (r.OdgovorIspravan == r.OdgovorNeispravan) g.Add("dva odgovora su ista");
            if (r.Polje.Length > 0 && !polja.ContainsKey(r.Polje)) g.Add($"nepoznato dodatno polje „{r.Polje}“");

            if (r.Naziv.Length > 0 && r.Aktivnost.Length > 0 && !stavke.Add((r.Sablon, r.Naziv, r.Oznaka, r.Aktivnost)))
                g.Add($"aktivnost „{r.Aktivnost}“ se za ovaj uređaj već pojavljuje u šablonu {r.Sablon}");

            if (r.Naziv.Length > 0 && sekcije.ContainsKey(r.Sekcija))
            {
                if (sekcijaUredjaja.TryGetValue((r.Naziv, r.Oznaka), out var prvi) && prvi.Sekcija != r.Sekcija)
                    g.Add($"uređaj je u sekciji {r.Sekcija}, a u redu {prvi.Red} u sekciji {prvi.Sekcija}");
                else
                    sekcijaUredjaja.TryAdd((r.Naziv, r.Oznaka), (r.Sekcija, r.Red));
            }

            if (g.Count > 0) greske.Add($"Red {r.Red} ({r.Sablon}, {(r.Naziv + " " + r.Oznaka).Trim()}): {string.Join("; ", g)}.");
        }

        return greske;
    }
}
