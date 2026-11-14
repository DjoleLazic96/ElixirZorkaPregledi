using System.Text.Json;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Popunjavanje;

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Dodatno polje uz stavku.</summary>
/// <param name="Id">ID polja (ključ u <c>polja</c> odgovora).</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Tip">Tip.</param>
/// <param name="Jedinica">Jedinica.</param>
/// <param name="Oznake">Oznake za dve vrednosti.</param>
/// <param name="Prikaz"><c>uvek</c> / <c>uz_neispravan</c>.</param>
/// <param name="Obavezno">Obavezno kad se prikaže.</param>
public sealed record PoljeDto(int Id, string Naziv, string Tip, string? Jedinica, IReadOnlyList<string> Oznake, string Prikaz, bool Obavezno);

/// <summary>Trenutni odgovor na stavku.</summary>
/// <param name="Vrednost">0 ispravan, 1 neispravan.</param>
/// <param name="Napomena">Napomena.</param>
/// <param name="Polja">Dodatna polja (JSON objekat).</param>
public sealed record OdgovorDto(short Vrednost, string? Napomena, JsonElement Polja);

/// <summary>Stavka za popunjavanje.</summary>
/// <param name="StavkaId">ID stavke.</param>
/// <param name="Aktivnost">Naziv aktivnosti.</param>
/// <param name="OdgovorIspravan">Odgovor „u redu“.</param>
/// <param name="OdgovorNeispravan">Odgovor „kvar“.</param>
/// <param name="Polja">Dodatna polja.</param>
/// <param name="Odgovor">Trenutni odgovor ili null.</param>
/// <param name="Fotografije">ID-jevi slika.</param>
/// <param name="AktivnostId">Aktivnost (za slike uputstva).</param>
/// <param name="Uputstvo">Tekst uputstva (R2.4) — ide u paket, pa „?“ radi i bez interneta; null = nije uneto.</param>
/// <param name="UputstvoSlika">Broj referentnih slika uputstva (učitavaju se sa interneta).</param>
/// <param name="StareFotografije">Slike iz ranije predate verzije (pre ove dorade) — vide se, ali se ne brišu (zamerka 15).</param>
public sealed record StavkaPopunjavanjaDto(int StavkaId, string Aktivnost, string OdgovorIspravan, string OdgovorNeispravan,
    IReadOnlyList<PoljeDto> Polja, OdgovorDto? Odgovor, IReadOnlyList<long> Fotografije, int AktivnostId = 0, string? Uputstvo = null, int UputstvoSlika = 0,
    IReadOnlyList<long>? StareFotografije = null);

/// <summary>Uređaj za popunjavanje.</summary>
/// <param name="UredjajId">ID.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Oznaka">Oznaka.</param>
/// <param name="VanFunkcije">Označen „Nije u funkciji“ na ovom pregledu.</param>
/// <param name="ProsliPutVanFunkcije">Bio van funkcije na poslednjem predatom pregledu (R4.6).</param>
/// <param name="Stavke">Aktivnosti.</param>
/// <param name="OtvoreneAkcije">Otvorene akcije iz ranijih pregleda — pop-up „da li je urađena“ (R4.7). Bez akcija otvorenih na ovom
/// pregledu i bez onih na koje je na ovom pregledu već odgovoreno „Ne“ (zamerka 46).</param>
/// <param name="AkcijeOvogPregleda">Akcije otvorene na ovom pregledu — samo oznaka uz stavku, bez pitanja (zamerka 46).</param>
public sealed record UredjajPopunjavanjaDto(int UredjajId, string Naziv, string Oznaka, bool VanFunkcije, bool ProsliPutVanFunkcije,
    IReadOnlyList<StavkaPopunjavanjaDto> Stavke, IReadOnlyList<AkcijaKratkoDto> OtvoreneAkcije, IReadOnlyList<AkcijaKratkoDto>? AkcijeOvogPregleda = null);

/// <summary>Otvorena akcija za pop-up.</summary>
/// <param name="Id">ID.</param>
/// <param name="Broj">Broj, npr. A-2026-0031.</param>
/// <param name="Aktivnost">Aktivnost na koju se odnosi.</param>
/// <param name="StavkaId">Stavka u ovoj verziji (ako postoji).</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Hitnost">Hitnost.</param>
/// <param name="Opis">Opis.</param>
public sealed record AkcijaKratkoDto(int Id, string Broj, string Aktivnost, int? StavkaId, string Vrsta, string Hitnost, string? Opis);

/// <summary>Sekcija za popunjavanje.</summary>
/// <param name="Sifra">Šifra.</param>
/// <param name="Moja">Da li prijavljeni upisuje u ovu sekciju (PZ sa dva inženjera, R3.15).</param>
/// <param name="Radi">Ko radi sekciju.</param>
/// <param name="Uredjaji">Uređaji.</param>
public sealed record SekcijaPopunjavanjaDto(string Sifra, bool Moja, string Radi, IReadOnlyList<UredjajPopunjavanjaDto> Uredjaji);

/// <summary>Ceo pregled za popunjavanje.</summary>
/// <param name="Id">ID.</param>
/// <param name="Broj">Broj.</param>
/// <param name="Tip">Tip.</param>
/// <param name="Status">Status.</param>
/// <param name="PlaniraniDatum">Planirani dan.</param>
/// <param name="ZapocetAt">Početak.</param>
/// <param name="VerzijaId">Verzija po kojoj se popunjava (za planiran: važeća; telefon je šalje uz „zapocni“, R10.5).</param>
/// <param name="Sekcije">Sekcije.</param>
public sealed record PopunjavanjeDto(int Id, string Broj, string Tip, string Status, DateOnly PlaniraniDatum, DateTime? ZapocetAt,
    int? VerzijaId, IReadOnlyList<SekcijaPopunjavanjaDto> Sekcije);

/// <summary>Paket za rad bez interneta (spec §5.1): dodeljeni pregledi za danas i narednih 7 dana i započeti.</summary>
/// <param name="Pregledi">Pregledi spremni za popunjavanje.</param>
/// <param name="GenerisanoAt">Kada je paket napravljen.</param>
public sealed record PaketDto(IReadOnlyList<PopunjavanjeDto> Pregledi, DateTime GenerisanoAt);

/// <summary>Paket prijavljenog inženjera.</summary>
public sealed record GetMojPaketQuery : IRequest<PaketDto>;

/// <summary>Operacija sa klijenta (spec §5.1).</summary>
/// <param name="OpId">UUID sa klijenta (idempotentnost).</param>
/// <param name="Vrsta"><c>odgovor</c>, <c>van_funkcije</c>, <c>sve_ispravno</c>, <c>predaja</c>.</param>
/// <param name="PregledId">Pregled.</param>
/// <param name="KlijentAt">Vreme sa klijenta.</param>
/// <param name="Podaci">Podaci operacije.</param>
public sealed record OperacijaInput(Guid OpId, string Vrsta, int PregledId, DateTime KlijentAt, JsonElement Podaci);

/// <summary>Odbijena operacija.</summary>
/// <param name="OpId">Operacija.</param>
/// <param name="Razlog">Poruka za korisnika.</param>
public sealed record OdbijenaOperacija(Guid OpId, string Razlog);

/// <summary>Rezultat sinhronizacije.</summary>
/// <param name="Prihvacene">Primenjene (i ranije primenjene).</param>
/// <param name="Odbijene">Odbijene sa razlogom.</param>
/// <param name="ServerSada">Vreme servera (klijent proverava sat).</param>
public sealed record SinhronizacijaRezultat(IReadOnlyList<Guid> Prihvacene, IReadOnlyList<OdbijenaOperacija> Odbijene, DateTime ServerSada);

// ─── Zahtevi ──────────────────────────────────────────────────────────────

/// <summary>Početak pregleda (R4.1): status „U radu“, vreme početka, važeća verzija šablona.</summary>
/// <param name="PregledId">Pregled.</param>
public sealed record ZapocniPregledCommand(int PregledId) : IRequest<PopunjavanjeDto>;

/// <summary>Pregled za popunjavanje.</summary>
/// <param name="PregledId">Pregled.</param>
public sealed record GetPopunjavanjeQuery(int PregledId) : IRequest<PopunjavanjeDto>;

/// <summary>Primena operacija redom (idempotentno po <c>opId</c>).</summary>
/// <param name="UredjajKlijenta">ID instalacije (faza 05).</param>
/// <param name="KlijentSada">Sat klijenta (faza 05: provera odstupanja).</param>
/// <param name="Operacije">Operacije.</param>
public sealed record SinhronizacijaCommand(string? UredjajKlijenta, DateTime? KlijentSada, IReadOnlyList<OperacijaInput> Operacije) : IRequest<SinhronizacijaRezultat>;

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Popunjavanje pregleda: početak, čitanje i operacije.</summary>
public sealed class PopunjavanjeHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, IBrojacService brojac, IFajlSkladiste? skladiste = null) :
    IRequestHandler<ZapocniPregledCommand, PopunjavanjeDto>,
    IRequestHandler<GetPopunjavanjeQuery, PopunjavanjeDto>,
    IRequestHandler<SinhronizacijaCommand, SinhronizacijaRezultat>,
    IRequestHandler<GetMojPaketQuery, PaketDto>
{
    /// <summary>Poruka kad pregled nastavlja drugi telefon (R10.5).</summary>
    public const string PorukaDrugiTelefon = "Pregled je započet na drugom telefonu. Nastaviće se kad taj telefon pošalje podatke.";

    /// <summary>Poruka kad se pregled započinje pre planiranog dana; <c>{0}</c> je datum.</summary>
    public const string PorukaPreDatuma = "Pregled može da se započne od {0:dd.MM.yyyy}.";

    /// <summary>
    /// Pregled se započinje najranije na planirani dan (odluka 2026-11-10): DP je provera stanja tog dana, a PZ ranije pomera
    /// Supervizor ili Direktor odobravanjem zahteva. Proverava se serverski dan — sat telefona napred ne otvara pregled ranije.
    /// </summary>
    private void ProveriDatumPocetka(Pregled p)
    {
        if (p.PlaniraniDatum > clock.Today) throw new BusinessRuleException(string.Format(PorukaPreDatuma, p.PlaniraniDatum));
    }

    /// <inheritdoc />
    public async Task<PaketDto> Handle(GetMojPaketQuery _, CancellationToken ct)
    {
        var granica = clock.Today.AddDays(7);
        var ja = currentUser.UserId;
        var pregledi = await db.Pregledi.Include(p => p.Ucesnici)
            .Where(p => (p.DodeljenId == ja || p.Ucesnici.Any(u => u.KorisnikId == ja))
                        && ((p.Status == StatusPregleda.Planiran && p.PlaniraniDatum <= granica)
                            || p.Status == StatusPregleda.URadu || p.Status == StatusPregleda.VracenNaDoradu))
            .OrderBy(p => p.PlaniraniDatum).ToListAsync(ct);
        var rez = new List<PopunjavanjeDto>(pregledi.Count);
        foreach (var p in pregledi) rez.Add(await IzgradiAsync(p, ct));
        return new PaketDto(rez, clock.UtcNow);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>ID telefona iz tekućeg zahteva sinhronizacije.</summary>
    private string? _uredjajKlijenta;

    /// <inheritdoc />
    public async Task<PopunjavanjeDto> Handle(ZapocniPregledCommand req, CancellationToken ct)
    {
        var p = await UcitajPregledAsync(req.PregledId, ct);
        ProveriUcesnika(p);
        if (p.Status == StatusPregleda.Planiran)
        {
            ProveriDatumPocetka(p);
            var verzija = await VazecaVerzijaAsync(p.Tip, ct) ?? throw new BusinessRuleException($"Šablon {p.Tip} još nije uvezen.");
            p.SablonVerzijaId = verzija;
            p.Status = StatusPregleda.URadu;
            p.ZapocetAt = clock.UtcNow;
            PregledIstorija.Dodaj(db, p, "pocetak", new { verzijaId = verzija }, currentUser.UserId, clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }
        else if (p.Status != StatusPregleda.URadu)
            throw new BusinessRuleException("Predat pregled se menja samo kroz doradu.");
        return await Handle(new GetPopunjavanjeQuery(p.Id), ct);
    }

    /// <inheritdoc />
    public async Task<PopunjavanjeDto> Handle(GetPopunjavanjeQuery req, CancellationToken ct)
    {
        var p = await UcitajPregledAsync(req.PregledId, ct);
        ProveriUcesnika(p);
        return await IzgradiAsync(p, ct);
    }

    /// <summary>Pregled sa stavkama: započet po svojoj verziji, planiran po važećoj (telefon može da ga započne bez interneta).</summary>
    private async Task<PopunjavanjeDto> IzgradiAsync(Pregled p, CancellationToken ct)
    {
        var verzijaId = p.SablonVerzijaId ?? await VazecaVerzijaAsync(p.Tip, ct);
        if (verzijaId is null)
            return new PopunjavanjeDto(p.Id, p.Broj, p.Tip, p.Status, p.PlaniraniDatum, p.ZapocetAt, null, []);

        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var stavke = await StavkeAsync(verzijaId.Value, ct);
        var odgovori = await db.Odgovori.AsNoTracking().Where(o => o.PregledId == p.Id && o.Dorada == p.Dorada).ToDictionaryAsync(o => o.StavkaId, ct);
        var van = (await db.UredjajiVanFunkcije.AsNoTracking().Where(v => v.PregledId == p.Id).Select(v => v.UredjajId).ToListAsync(ct)).ToHashSet();
        var sveSlike = await db.Fotografije.AsNoTracking().Where(f => f.PregledId == p.Id).Select(f => new { f.StavkaId, f.Id, f.Dorada }).ToListAsync(ct);
        var slike = sveSlike.ToLookup(f => f.StavkaId, f => f.Id);
        var stareSlike = sveSlike.Where(f => f.Dorada < p.Dorada).ToLookup(f => f.StavkaId, f => f.Id);
        var uredjajIds = stavke.Select(s => s.UredjajId).Distinct().ToList();
        var prosli = await ProsliPutVanFunkcijeAsync(p, uredjajIds, ct);
        // Pitanje „da li je urađena“ je za akcije iz ranijih pregleda (R4.7); akcija otvorena ovde i ona na koju je ovde već
        // odgovoreno „Ne“ ne pitaju ponovo do kraja ovog pregleda (zamerka 46).
        var odgovoreneOvde = await db.AkcijaDogadjaji.AsNoTracking().Where(d => d.PregledId == p.Id && d.Vrsta == "ostaje_otvorena")
            .Select(d => d.AkcijaId).ToListAsync(ct);
        var naUredjajima = (await db.Akcije.AsNoTracking()
                .Where(a => uredjajIds.Contains(a.UredjajId) && ((a.Status == "otvorena" && !odgovoreneOvde.Contains(a.Id)) || a.PregledId == p.Id))
                .Join(db.Aktivnosti, a => a.AktivnostId, x => x.Id, (a, x) => new { a, x.Naziv }).ToListAsync(ct))
            .Select(x => (x.a.UredjajId, OvogPregleda: x.a.PregledId == p.Id, Dto: new AkcijaKratkoDto(x.a.Id, x.a.Broj, x.Naziv,
                stavke.FirstOrDefault(s => s.Kljuc == x.a.StavkaKljuc)?.StavkaId, x.a.Vrsta, x.a.Hitnost, x.a.Opis)))
            .ToList();
        var akcije = naUredjajima.Where(x => !x.OvogPregleda).ToLookup(x => x.UredjajId, x => x.Dto);
        var akcijeOvde = naUredjajima.Where(x => x.OvogPregleda).ToLookup(x => x.UredjajId, x => x.Dto);

        var sekcije = stavke.GroupBy(s => s.Sekcija).Select(gs =>
        {
            var (moja, radi) = KoRadiSekciju(p, gs.Key, k);
            return new SekcijaPopunjavanjaDto(gs.Key, moja, radi, gs.GroupBy(s => s.UredjajId).Select(gu => new UredjajPopunjavanjaDto(
                gu.Key, gu.First().Naziv, gu.First().Oznaka, van.Contains(gu.Key), prosli.Contains(gu.Key),
                gu.Select(s => new StavkaPopunjavanjaDto(s.StavkaId, s.Aktivnost, s.Ok, s.Nok, s.Polja,
                    odgovori.TryGetValue(s.StavkaId, out var o) ? new OdgovorDto(o.Vrednost, o.Napomena, JsonDocument.Parse(o.Polja).RootElement.Clone()) : null,
                    slike[s.StavkaId].ToList(), s.AktivnostId, s.Uputstvo, s.UputstvoSlika, stareSlike[s.StavkaId].ToList())).ToList(), akcije[gu.Key].ToList(), akcijeOvde[gu.Key].ToList())).ToList());
        }).ToList();

        return new PopunjavanjeDto(p.Id, p.Broj, p.Tip, p.Status, p.PlaniraniDatum, p.ZapocetAt, verzijaId, sekcije);
    }

    /// <inheritdoc />
    public async Task<SinhronizacijaRezultat> Handle(SinhronizacijaCommand req, CancellationToken ct)
    {
        _uredjajKlijenta = string.IsNullOrWhiteSpace(req.UredjajKlijenta) ? null : req.UredjajKlijenta;
        // Bez prava popunjavanja (oduzeto dok je rad čekao slanje) urađeno ne propada nego postaje sporni unos (zamerka 66).
        if (!currentUser.ImaPravo(Prava.PreglediPopunjavanje))
            return await BezPravaAsync(req, ct);
        var prihvacene = new List<Guid>();
        var odbijene = new List<OdbijenaOperacija>();
        foreach (var op in req.Operacije)
        {
            if (await db.SinhronizacijaOperacije.AnyAsync(o => o.OpId == op.OpId, ct))
            {
                prihvacene.Add(op.OpId);   // već primenjeno — ponovljeno slanje posle prekida veze
                continue;
            }
            var razlog = await PrimeniJednuAsync(op, ct);
            if (razlog is null) prihvacene.Add(op.OpId);
            else odbijene.Add(new OdbijenaOperacija(op.OpId, razlog));
        }

        await ZabeleziOdstupanjeSataAsync(req, ct);
        return new SinhronizacijaRezultat(prihvacene, odbijene, clock.UtcNow);
    }

    /// <summary>Poruka telefonu kad inženjer više nema pravo popunjavanja (zamerka 66); telefon je prepoznaje po početku.</summary>
    public const string PorukaBezPrava = "Više nemate pravo da popunjavate preglede. Urađeno je poslato Supervizoru kao sporni unos.";

    /// <summary>
    /// Inženjeru je oduzeto pravo dok je imao neposlat rad (zamerka 66): ništa se ne primenjuje, a urađeno na pregledima na kojima je
    /// bio čuva se kao sporni unos (Supervizor/Direktor: Primeni / Odbaci, posle prebacivanja na drugog inženjera). Telefon dobija
    /// odbijanje za svaku operaciju, pa mu se red isprazni.
    /// </summary>
    private async Task<SinhronizacijaRezultat> BezPravaAsync(SinhronizacijaCommand req, CancellationToken ct)
    {
        var ja = currentUser.UserId!;
        var odbijene = new List<OdbijenaOperacija>();
        foreach (var op in req.Operacije)
        {
            if (await db.SinhronizacijaOperacije.AnyAsync(o => o.OpId == op.OpId, ct)) continue;
            odbijene.Add(new OdbijenaOperacija(op.OpId, PorukaBezPrava));
            if (!await BioNaPregleduAsync(op.PregledId, ja, ct)) continue;
            db.SporniUnosi.Add(new SporanUnos
            {
                PregledId = op.PregledId, KorisnikId = ja, Razlog = "Inženjer više nema pravo popunjavanja.", PrimljenoAt = clock.UtcNow,
                Operacija = JsonSerializer.Serialize(op, Json),
            });
            await ObavestiOSpornomAsync(op.PregledId, "bez_prava", ct);
            await db.SaveChangesAsync(ct);
        }
        return new SinhronizacijaRezultat([], odbijene, clock.UtcNow);
    }

    /// <summary>
    /// Da li je korisnik ikad bio dodeljen pregledu ili njegov učesnik: sadašnja dodela, ili događaj u istoriji (dodela, prebacivanje,
    /// izmena rasporeda, drugi inženjer). Noviji događaji nose ID (<c>inzenjerId</c>, <c>stariId</c>); stariji samo ime.
    /// </summary>
    private async Task<bool> BioNaPregleduAsync(int pregledId, string korisnikId, CancellationToken ct)
    {
        var p = await db.Pregledi.AsNoTracking().Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == pregledId, ct);
        if (p is null) return false;
        if (p.DodeljenId == korisnikId || p.Ucesnici.Any(u => u.KorisnikId == korisnikId)) return true;
        var ime = await db.Users.AsNoTracking().Where(u => u.Id == korisnikId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var dogadjaji = await db.PregledDogadjaji.AsNoTracking()
            .Where(d => d.PregledId == pregledId && (d.Vrsta == "dodela" || d.Vrsta == "promena_pz" || d.Vrsta == "prebacivanje" || d.Vrsta == "izmena_rasporeda" || d.Vrsta == "ucesnik"))
            .Select(d => d.Podaci).ToListAsync(ct);
        return dogadjaji.Any(json =>
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            bool Je(JsonElement e, string kId, string kIme) =>
                (e.TryGetProperty(kId, out var v) && v.GetString() == korisnikId)
                || (!string.IsNullOrEmpty(ime) && e.TryGetProperty(kIme, out var n) && n.ValueKind == JsonValueKind.String && n.GetString() == ime);
            return Je(r, "inzenjerId", "inzenjer") || Je(r, "stariId", "stari") || Je(r, "noviId", "novi")
                || (r.TryGetProperty("stari", out var st) && st.ValueKind == JsonValueKind.Object && Je(st, "inzenjerId", "inzenjer"));
        });
    }

    /// <summary>Poruka za operaciju čiji podaci nisu u očekivanom obliku (npr. stara verzija aplikacije).</summary>
    public const string PorukaNeispravnaOperacija = "Operacija ima neispravne podatke i nije primenjena.";

    /// <summary>Poruka kad operacija ni posle ponovljenih pokušaja ne može da se upiše.</summary>
    public const string PorukaNijeSacuvana = "Promena nije mogla da se sačuva. Proverite unos i pokušajte ponovo.";

    /// <summary>Najduža napomena uz odgovor (kolona <c>odgovor.napomena</c>).</summary>
    public const int NajduzaNapomena = 2000;

    /// <summary>Najduži opis akcije (kolona <c>akcija.opis</c>).</summary>
    public const int NajduziOpisAkcije = 1000;

    private const int NajvisePokusaja = 3;

    /// <summary>
    /// Primenjuje jednu operaciju; vraća razlog odbijanja ili <c>null</c> ako je prihvaćena. Neispravni podaci odbijaju samo tu
    /// operaciju (inače bi telefon beskonačno ponavljao isti paket). Paralelni upis (isti <c>opId</c> sa drugog zahteva ili
    /// istovremena izmena pregleda) se ponavlja; ako je operacija u međuvremenu upisana, smatra se prihvaćenom.
    /// </summary>
    /// <summary>Fajlovi slika obrisanih u ovoj operaciji; brišu se sa diska posle uspešnog upisa u bazu.</summary>
    private readonly List<Fajl> _zaBrisanje = [];

    private async Task<string?> PrimeniJednuAsync(OperacijaInput op, CancellationToken ct)
    {
        for (var pokusaj = 1; ; pokusaj++)
        {
            try
            {
                await PrimeniAsync(op, ct);
                db.SinhronizacijaOperacije.Add(new SinhronizacijaOperacija
                {
                    OpId = op.OpId, PregledId = op.PregledId, Vrsta = op.Vrsta, KlijentAt = op.KlijentAt.ToUniversalTime(), PrimljenoAt = clock.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                foreach (var f in _zaBrisanje)
                {
                    try { skladiste?.Obrisi(f); }
                    catch (IOException) { /* fajl ostaje na disku; zapis je već obrisan, pa se ne prikazuje */ }
                }
                _zaBrisanje.Clear();
                return null;
            }
            catch (AppException ex)
            {
                // Odbijena operacija ne zaustavlja ostale; klijent dobija razlog za korisnika.
                _zaBrisanje.Clear();
                db.ChangeTracker.Clear();
                var razlog = ex is BusinessRuleException b ? string.Join(" ", b.Greske) : ex.Message;
                // Pregled je u međuvremenu prebačen ili poništen: urađeno se čuva kao sporan unos (R6.8).
                // Samo za onoga ko je bio dodeljen tom pregledu ili njegov učesnik (zamerka 14) — ostalima se izmena samo odbija.
                if ((ex is ForbiddenException || razlog == "Pregled je poništen.") && await BioNaPregleduAsync(op.PregledId, currentUser.UserId!, ct))
                {
                    db.SporniUnosi.Add(new SporanUnos
                    {
                        PregledId = op.PregledId, KorisnikId = currentUser.UserId!, Razlog = razlog, PrimljenoAt = clock.UtcNow,
                        Operacija = JsonSerializer.Serialize(op, Json),
                    });
                    await ObavestiOSpornomAsync(op.PregledId, razlog == "Pregled je poništen." ? "ponisten" : "prebacen", ct);
                    await db.SaveChangesAsync(ct);
                }
                return razlog;
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or JsonException or OverflowException)
            {
                _zaBrisanje.Clear();
                db.ChangeTracker.Clear();
                return PorukaNeispravnaOperacija;
            }
            catch (DbUpdateException)
            {
                _zaBrisanje.Clear();
                db.ChangeTracker.Clear();
                if (await db.SinhronizacijaOperacije.AnyAsync(o => o.OpId == op.OpId, ct)) return null;
                // Posle poslednjeg pokušaja odbija se samo ova operacija (zamerka 38): greška za ceo paket bi zaglavila red
                // na telefonu, pa ne bi stiglo ni ništa posle nje.
                if (pokusaj >= NajvisePokusaja) return PorukaNijeSacuvana;
            }
        }
    }

    /// <summary>
    /// Zamerka 60: stigli su sporni unosi — primaocima (podrazumevano Supervizor i Direktor), onome ko je prebacio ili poništio
    /// pregled, novom inženjeru i samom inženjeru. Jednom dnevno po pregledu i inženjeru, ma koliko operacija stiglo.
    /// </summary>
    private async Task ObavestiOSpornomAsync(int pregledId, string slucaj, CancellationToken ct)
    {
        var ponisten = slucaj == "ponisten";
        var ja = currentUser.UserId!;
        var p = await db.Pregledi.AsNoTracking().Where(x => x.Id == pregledId).Select(x => new { x.Broj, x.DodeljenId }).FirstAsync(ct);
        var ime = await db.Users.Where(u => u.Id == ja).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Inženjer";
        var ko = await db.PregledDogadjaji.AsNoTracking()
            .Where(d => d.PregledId == pregledId && d.KorisnikId != null && d.KorisnikId != ja
                        && (d.Vrsta == "izmena_rasporeda" || d.Vrsta == "prebacivanje" || d.Vrsta == "ponistavanje"))
            .OrderByDescending(d => d.At).Select(d => d.KorisnikId).FirstOrDefaultAsync(ct);
        var bezPrava = slucaj == "bez_prava";
        var sta = ponisten ? "poništen" : "dodeljen drugom inženjeru";
        var kljuc = $"sporan_unos:{pregledId}:{ja}:{clock.Today:yyyy-MM-dd}";
        var drugi = ponisten || bezPrava || p.DodeljenId == ja ? null : p.DodeljenId;
        var primaoci = (await Obavesti.PrimaociAsync(db, VrsteObavestenja.SporanUnos, ct)).Append(ko).Append(drugi).Where(x => x != ja);
        var tekst = bezPrava
            ? $"{p.Broj} je rađen bez interneta ({ime}), a {ime} u međuvremenu više nema pravo popunjavanja. Prebacite pregled drugom inženjeru, pa na stranici pregleda odlučite o unosu (Primeni / Odbaci)."
            : $"{p.Broj} je rađen bez interneta ({ime}), a pregled je u međuvremenu {sta}. Unos čeka odluku na stranici pregleda (Primeni / Odbaci).";
        await Obavesti.DodajAsync(db, primaoci, VrsteObavestenja.SporanUnos, $"Sporni unosi: {p.Broj}", tekst, $"/pregledi/{pregledId}", kljuc, clock.UtcNow, ct);
        if (!bezPrava)   // bez prava: telefon sam javlja poruku, a obaveštenje ne bi mogao ni da otvori
            await Obavesti.DodajAsync(db, [ja], VrsteObavestenja.SporanUnos, $"{p.Broj} je {sta}",
                $"Pregled {p.Broj} je u međuvremenu {sta} — vaš unos čeka odluku Supervizora ili Direktora.", $"/pregledi/{pregledId}", kljuc, clock.UtcNow, ct);
    }

    /// <summary>Sat telefona koji odstupa više od dozvoljenog (R5.1) se beleži kontroloru; inženjer ne vidi poruku.</summary>
    private async Task ZabeleziOdstupanjeSataAsync(SinhronizacijaCommand req, CancellationToken ct)
    {
        if (req.KlijentSada is not { } klijent || req.Operacije.Count == 0) return;
        var dozvoljeno = TimeSpan.FromMinutes((await new PodesavanjaHandler(db).Handle(new GetPodesavanjaQuery(), ct)).OdstupanjeSataMin);
        var razlika = klijent.ToUniversalTime() - clock.UtcNow;
        if (razlika.Duration() <= dozvoljeno) return;
        foreach (var id in req.Operacije.Select(o => o.PregledId).Distinct())
        {
            var p = await db.Pregledi.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is not null)
                PregledIstorija.Dodaj(db, p, "odstupanje_sata", new { sekundi = (int)razlika.TotalSeconds }, currentUser.UserId, clock.UtcNow);
        }
        await db.SaveChangesAsync(ct);
    }

    // ─── Primena operacija ────────────────────────────────────────────────

    private async Task PrimeniAsync(OperacijaInput op, CancellationToken ct)
    {
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == op.PregledId, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        ProveriUcesnika(p);
        if (p.Status == StatusPregleda.Ponisten) throw new BusinessRuleException("Pregled je poništen.");
        if (op.Vrsta == "zapocni")
        {
            await ZapocniOpAsync(p, op, ct);
            return;
        }
        if (p.Status is not (StatusPregleda.URadu or StatusPregleda.VracenNaDoradu))
            throw new BusinessRuleException("Predat pregled se menja samo kroz doradu.");
        if (p.UredjajKlijenta is not null && _uredjajKlijenta is not null && p.UredjajKlijenta != _uredjajKlijenta)
            throw new BusinessRuleException(PorukaDrugiTelefon);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var stavke = await StavkeAsync(p.SablonVerzijaId!.Value, ct);
        var klijentAt = op.KlijentAt.ToUniversalTime();

        switch (op.Vrsta)
        {
            case "odgovor":
            {
                var stavkaId = op.Podaci.GetProperty("stavkaId").GetInt32();
                var vrednost = (short)op.Podaci.GetProperty("vrednost").GetInt32();
                if (vrednost is not (0 or 1)) throw new BusinessRuleException("Odgovor mora biti ispravan ili neispravan.");
                var s = stavke.FirstOrDefault(x => x.StavkaId == stavkaId) ?? throw new BusinessRuleException("Stavka nije u ovom pregledu.");
                ProveriSekciju(p, s.Sekcija, k);
                var napomena = op.Podaci.TryGetProperty("napomena", out var n) && n.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(n.GetString())
                    ? n.GetString()!.Trim() : null;
                if (napomena?.Length > NajduzaNapomena) throw new BusinessRuleException($"Napomena može imati najviše {NajduzaNapomena} znakova.");
                var polja = op.Podaci.TryGetProperty("polja", out var pp) && pp.ValueKind == JsonValueKind.Object ? pp.GetRawText() : "{}";

                var o = await db.Odgovori.FirstOrDefaultAsync(x => x.PregledId == p.Id && x.StavkaId == stavkaId && x.Dorada == p.Dorada, ct);
                if (o is null)
                    db.Odgovori.Add(new Odgovor
                    {
                        PregledId = p.Id, StavkaId = stavkaId, Vrednost = vrednost, Napomena = napomena, Polja = polja,
                        KorisnikId = currentUser.UserId!, KlijentAt = klijentAt, ServerAt = clock.UtcNow, OpId = op.OpId, Dorada = p.Dorada,
                    });
                else
                {
                    if (o.Vrednost != vrednost || o.Napomena != napomena || o.Polja != polja)
                        PregledIstorija.Dodaj(db, p, "izmena_odgovora", new
                        {
                            stavkaId, aktivnost = s.Aktivnost, uredjaj = $"{s.Naziv} {s.Oznaka}".Trim(),
                            staro = new { vrednost = o.Vrednost, napomena = o.Napomena }, novo = new { vrednost, napomena },
                        }, currentUser.UserId, clock.UtcNow);
                    (o.Vrednost, o.Napomena, o.Polja, o.KorisnikId, o.KlijentAt, o.ServerAt, o.OpId) =
                        (vrednost, napomena, polja, currentUser.UserId!, klijentAt, clock.UtcNow, op.OpId);
                }
                break;
            }
            case "obrisi_sliku":
            {
                // Zamerka 5: inženjer briše svoju sliku dok pregled nije predat. Već obrisana (ponovljeno slanje) = prihvaćeno.
                var fotoId = op.Podaci.GetProperty("fotografijaId").GetInt64();
                var f = await db.Fotografije.FirstOrDefaultAsync(x => x.Id == fotoId && x.PregledId == p.Id, ct);
                if (f is null) break;
                var s = stavke.FirstOrDefault(x => x.StavkaId == f.StavkaId) ?? throw new BusinessRuleException("Stavka nije u ovom pregledu.");
                ProveriSekciju(p, s.Sekcija, k);
                // Slika iz već predate verzije je dokaz uz napomenu; u doradi se brišu samo slike te dorade (zamerka 15).
                if (f.Dorada < p.Dorada) throw new BusinessRuleException("U doradi se brišu samo slike dodate u ovoj doradi.");
                db.Fotografije.Remove(f);
                var fajl = await db.Fajlovi.FirstOrDefaultAsync(x => x.Id == f.FajlId, ct);
                if (fajl is not null)
                {
                    db.Fajlovi.Remove(fajl);
                    _zaBrisanje.Add(fajl);   // sa diska tek kad baza potvrdi brisanje
                }
                break;
            }
            case "van_funkcije":
            {
                var uredjajId = op.Podaci.GetProperty("uredjajId").GetInt32();
                var vanFunkcije = op.Podaci.GetProperty("vanFunkcije").GetBoolean();
                var s = stavke.FirstOrDefault(x => x.UredjajId == uredjajId) ?? throw new BusinessRuleException("Uređaj nije u ovom pregledu.");
                ProveriSekciju(p, s.Sekcija, k);
                var postojeci = await db.UredjajiVanFunkcije.FirstOrDefaultAsync(v => v.PregledId == p.Id && v.UredjajId == uredjajId, ct);
                if (vanFunkcije && postojeci is null)
                    db.UredjajiVanFunkcije.Add(new UredjajVanFunkcije { PregledId = p.Id, UredjajId = uredjajId, KorisnikId = currentUser.UserId!, KlijentAt = klijentAt });
                else if (!vanFunkcije && postojeci is not null)
                    db.UredjajiVanFunkcije.Remove(postojeci);
                break;
            }
            case "sve_ispravno":
            {
                var sekcija = op.Podaci.GetProperty("sekcija").GetString() ?? string.Empty;
                ProveriSekciju(p, sekcija, k);
                var odgovoreno = (await db.Odgovori.Where(o => o.PregledId == p.Id && o.Dorada == p.Dorada).Select(o => o.StavkaId).ToListAsync(ct)).ToHashSet();
                var van = (await db.UredjajiVanFunkcije.Where(v => v.PregledId == p.Id).Select(v => v.UredjajId).ToListAsync(ct)).ToHashSet();
                var neoznacene = stavke.Where(s => s.Sekcija == sekcija && !odgovoreno.Contains(s.StavkaId) && !van.Contains(s.UredjajId)).ToList();
                foreach (var s in neoznacene)
                    db.Odgovori.Add(new Odgovor
                    {
                        PregledId = p.Id, StavkaId = s.StavkaId, Vrednost = 0, KorisnikId = currentUser.UserId!, KlijentAt = klijentAt,
                        ServerAt = clock.UtcNow, OpId = op.OpId, Dorada = p.Dorada,
                    });
                // Svaka upotreba se beleži (R4.3) — kontrolor vidi sekciju i vreme. Ponovljen klik na punoj sekciji ne upisuje ništa.
                if (neoznacene.Count > 0)
                    PregledIstorija.Dodaj(db, p, "sve_ispravno", new { sekcija, stavki = neoznacene.Count, klijentAt }, currentUser.UserId, clock.UtcNow);
                break;
            }
            case "predaja":
                await PredajAsync(p, stavke, k, klijentAt, ct);
                break;
            case "akcija":
            {
                var stavkaId = op.Podaci.GetProperty("stavkaId").GetInt32();
                var s = stavke.FirstOrDefault(x => x.StavkaId == stavkaId) ?? throw new BusinessRuleException("Stavka nije u ovom pregledu.");
                ProveriSekciju(p, s.Sekcija, k);
                var vrsta = op.Podaci.GetProperty("vrsta").GetString() ?? "";
                var hitnost = op.Podaci.GetProperty("hitnost").GetString() ?? "";
                if (!AkcijaPravila.Vrste.Contains(vrsta)) throw new BusinessRuleException("Nepoznata vrsta akcije.");
                if (!AkcijaPravila.Hitnosti.Contains(hitnost)) throw new BusinessRuleException("Nepoznata hitnost akcije.");
                var opis = op.Podaci.TryGetProperty("opis", out var o) && o.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(o.GetString()) ? o.GetString()!.Trim() : null;
                if (opis?.Length > NajduziOpisAkcije) throw new BusinessRuleException($"Opis akcije može imati najviše {NajduziOpisAkcije} znakova.");
                // Dan po beogradskom vremenu (zamerka 12): UTC posle ponoći je još jučerašnji datum.
                var danas = AnalitikaPravila.LokalniDan(klijentAt);
                var sledeciPz = await db.Pregledi.Where(x => x.Tip.StartsWith("PZ") && x.Status == StatusPregleda.Planiran && x.PlaniraniDatum >= danas)
                    .OrderBy(x => x.PlaniraniDatum).Select(x => (DateOnly?)x.PlaniraniDatum).FirstOrDefaultAsync(ct);
                var a = new Akcija
                {
                    Broj = await brojac.SledeciAsync("A", danas.Year, ct), PregledId = p.Id, UredjajId = s.UredjajId, AktivnostId = s.AktivnostId,
                    StavkaKljuc = s.Kljuc, Vrsta = vrsta, Hitnost = hitnost, Opis = opis, Rok = AkcijaPravila.Rok(hitnost, danas, sledeciPz),
                    KreiraoId = currentUser.UserId!, KreiranoAt = clock.UtcNow,
                };
                db.Akcije.Add(a);
                db.AkcijaDogadjaji.Add(new AkcijaDogadjaj { Akcija = a, Vrsta = "otvorena", Tekst = $"Otvorena na pregledu {p.Broj}", KorisnikId = currentUser.UserId, PregledId = p.Id, At = clock.UtcNow });
                if (hitnost == "hitno")
                    await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.HitnaAkcija, $"Hitna akcija {a.Broj}",
                        $"{s.Naziv} {s.Oznaka} · {s.Aktivnost}{(opis is null ? "" : $" — {opis}")} (pregled {p.Broj}).".Replace("  ", " "), "/akcije?hitnost=hitno", $"hitna_akcija:{a.Broj}", clock.UtcNow, ct);
                break;
            }
            case "akcija_odgovor":
            {
                // Pop-up na sledećem pregledu sa tim uređajem (R4.7, R7.4); važi i za akcije nastale u PZ-u.
                var akcijaId = op.Podaci.GetProperty("akcijaId").GetInt32();
                var uradjeno = op.Podaci.GetProperty("uradjeno").GetBoolean();
                var a = await db.Akcije.FirstOrDefaultAsync(x => x.Id == akcijaId, ct) ?? throw new BusinessRuleException("Akcija ne postoji.");
                if (stavke.All(s => s.UredjajId != a.UredjajId)) throw new BusinessRuleException("Akcija nije na uređaju iz ovog pregleda.");
                if (a.Status != "otvorena") break;   // već zatvorena (npr. sa ekrana) — pop-up nema šta da menja
                if (uradjeno)
                {
                    a.Status = "zatvorena";
                    a.ZatvorioId = currentUser.UserId;
                    a.ZatvorenoAt = clock.UtcNow;
                    a.Razlog = $"Urađeno (potvrđeno na pregledu {p.Broj})";
                    await TpmPregledi.Application.Features.Akcije.ZatvaranjeAkcijeHandler.ZatvoriZahteveAsync(db, a, currentUser.UserId, clock.UtcNow, ct);
                }
                db.AkcijaDogadjaji.Add(new AkcijaDogadjaj
                {
                    AkcijaId = a.Id, Vrsta = uradjeno ? "zatvorena" : "ostaje_otvorena",
                    Tekst = uradjeno ? $"Urađena — potvrđeno na pregledu {p.Broj}" : $"Nije urađena — ostaje otvorena (pregled {p.Broj})",
                    KorisnikId = currentUser.UserId, PregledId = p.Id, At = clock.UtcNow,
                });
                break;
            }
            case "predlog_izmene":
            {
                // Predlog izmene šablona poslat tokom popunjavanja (zamerka 34): radi i bez interneta, a pamti pregled.
                var vrsta = op.Podaci.GetProperty("vrsta").GetString() ?? "dodavanje";
                if (vrsta is not ("dodavanje" or "izbacivanje" or "ispravka")) throw new BusinessRuleException("Izaberite vrstu predloga.");
                var opis = op.Podaci.GetProperty("opis").GetString() ?? string.Empty;
                var razlog = op.Podaci.GetProperty("razlog").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(opis) || string.IsNullOrWhiteSpace(razlog)) throw new BusinessRuleException("Opis i razlog predloga su obavezni.");
                if (opis.Length > 1000 || razlog.Length > 1000) throw new BusinessRuleException("Opis i razlog mogu imati najviše 1000 karaktera.");
                await TpmPregledi.Application.Features.Sabloni.IzmeneSablonaHandler.NoviPredlogAsync(db, p.Tip, vrsta, null, opis, razlog, p.Id, currentUser.UserId!, clock.UtcNow, ct);
                break;
            }
            default:
                throw new BusinessRuleException($"Nepoznata operacija „{op.Vrsta}“.");
        }
    }

    /// <summary>
    /// Početak sa telefona (R4.1, R10.5): vreme početka je vreme sa telefona, verzija je ona po kojoj je telefon popunjavao
    /// (ako se šablon promenio dok je bio bez interneta, pregled ostaje na staroj), a pregled se vezuje za taj telefon.
    /// </summary>
    private async Task ZapocniOpAsync(Pregled p, OperacijaInput op, CancellationToken ct)
    {
        if (p.Status == StatusPregleda.URadu)
        {
            if (p.UredjajKlijenta is not null && _uredjajKlijenta is not null && p.UredjajKlijenta != _uredjajKlijenta)
                throw new BusinessRuleException(PorukaDrugiTelefon);
            return;
        }
        if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException("Predat pregled se menja samo kroz doradu.");
        ProveriDatumPocetka(p);

        int? verzija = null;
        if (op.Podaci.ValueKind == JsonValueKind.Object && op.Podaci.TryGetProperty("verzijaId", out var v) && v.ValueKind == JsonValueKind.Number)
        {
            var trazena = v.GetInt32();
            verzija = await db.SablonVerzije.Where(x => x.Id == trazena && x.Sablon!.Tip == p.Tip && x.Status != StatusVerzije.Nacrt)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        }
        verzija ??= await VazecaVerzijaAsync(p.Tip, ct) ?? throw new BusinessRuleException($"Šablon {p.Tip} još nije uvezen.");

        p.SablonVerzijaId = verzija;
        p.Status = StatusPregleda.URadu;
        p.ZapocetAt = op.KlijentAt.ToUniversalTime();
        p.UredjajKlijenta = _uredjajKlijenta;
        PregledIstorija.Dodaj(db, p, "pocetak", new { verzijaId = verzija, uredjajKlijenta = _uredjajKlijenta }, currentUser.UserId, clock.UtcNow);
    }

    /// <summary>
    /// Kvarovi iz predatog pregleda (R8.7): neispravan otvara kvar ili mu dodaje prijavu, ispravan ga zatvara, „nije u funkciji“
    /// ne menja ništa. Zatvoren kvar označava otvorene akcije na stavci kao „stavka sada ispravna“ (R7.15).
    /// </summary>
    private async Task ObradiKvaroveAsync(Pregled p, IReadOnlyList<StavkaInfo> stavke, Dictionary<int, Odgovor> odgovori, HashSet<int> van, CancellationToken ct)
    {
        var uredjaji = stavke.Select(s => s.UredjajId).Distinct().ToList();
        var otvoreni = await db.Kvarovi.Where(x => x.Zatvoren == null && uredjaji.Contains(x.UredjajId)).ToListAsync(ct);
        // Zamerka 11: stanje kvara određuje najnoviji predat pregled za stavku. Stariji pregled (dorada, kasna predaja sa telefona)
        // ne otvara, ne zatvara i ne broji prijavu ako je za istu stavku već predat noviji — inače bi vratio staro stanje.
        var mojaPredaja = p.PredatAt ?? DateTime.MaxValue;
        var novije = (await (
                from x in db.Pregledi
                join st in db.SablonStavke on x.SablonVerzijaId equals st.VerzijaId
                where x.Id != p.Id && x.PredatAt != null && x.Status != StatusPregleda.Ponisten && uredjaji.Contains(st.UredjajId)
                      && (x.PlaniraniDatum > p.PlaniraniDatum || (x.PlaniraniDatum == p.PlaniraniDatum && x.PredatAt > mojaPredaja))
                select new { st.UredjajId, st.AktivnostId })
            .Distinct().ToListAsync(ct)).Select(x => (x.UredjajId, x.AktivnostId)).ToHashSet();
        foreach (var s in stavke.Where(s => !van.Contains(s.UredjajId) && odgovori.ContainsKey(s.StavkaId)))
        {
            if (novije.Contains((s.UredjajId, s.AktivnostId))) continue;
            var o = odgovori[s.StavkaId];
            var kvar = otvoreni.FirstOrDefault(x => x.UredjajId == s.UredjajId && x.AktivnostId == s.AktivnostId);
            if (o.Vrednost == 1)
            {
                if (kvar is null)
                    db.Kvarovi.Add(new Kvar
                    {
                        Broj = await brojac.SledeciAsync("K", p.PlaniraniDatum.Year, ct), UredjajId = s.UredjajId, AktivnostId = s.AktivnostId,
                        StavkaKljuc = s.Kljuc, OtvorioPregledId = p.Id, PoslednjiPregledId = p.Id, Otvoren = p.PlaniraniDatum, Prijava = 1, PoslednjaNapomena = o.Napomena,
                    });
                else
                {
                    if (kvar.PoslednjiPregledId != p.Id) kvar.Prijava++;   // dorada istog pregleda nije nova prijava
                    kvar.PoslednjiPregledId = p.Id;
                    kvar.PoslednjaNapomena = o.Napomena;
                }
            }
            else if (kvar is not null)
            {
                kvar.Zatvoren = p.PlaniraniDatum;
                kvar.ZatvorioPregledId = p.Id;
                var akcije = await db.Akcije.Where(a => a.Status == "otvorena" && a.UredjajId == s.UredjajId && a.AktivnostId == s.AktivnostId).ToListAsync(ct);
                foreach (var a in akcije) a.StavkaSadaIspravna = true;
            }
        }
    }

    /// <summary>
    /// Obavezna dodatna polja koja se vide uz ovaj odgovor (zamerka 13): „uvek“, ili „uz neispravan“ kad je odgovor neispravan,
    /// a vrednost je prazna. Vraća nazive polja.
    /// </summary>
    private static IEnumerable<string> NedostajucaPolja(StavkaInfo s, Odgovor o)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(o.Polja) ? "{}" : o.Polja);
        var r = doc.RootElement;
        var nedostaju = new List<string>();
        foreach (var pd in s.Polja.Where(x => x.Obavezno && (x.Prikaz == "uvek" || o.Vrednost == 1)))
        {
            var ima = r.ValueKind == JsonValueKind.Object && r.TryGetProperty(pd.Id.ToString(), out var v) && v.ValueKind switch
            {
                JsonValueKind.String => !string.IsNullOrWhiteSpace(v.GetString()),
                JsonValueKind.Array => v.GetArrayLength() > 0 && v.EnumerateArray().All(x => x.ValueKind != JsonValueKind.Null && !(x.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(x.GetString()))),
                JsonValueKind.Null or JsonValueKind.Undefined => false,
                _ => true,
            };
            if (!ima) nedostaju.Add(pd.Naziv);
        }
        return nedostaju;
    }

    /// <summary>
    /// Ponovljeni kvar (R8.2): ista stavka neispravna bar N puta u poslednjih M dana (pragovi iz podešavanja), računajući i ovu predaju.
    /// </summary>
    private async Task PonovljeniKvarAsync(Pregled p, IReadOnlyList<StavkaInfo> stavke, Dictionary<int, Odgovor> odgovori, HashSet<int> van, CancellationToken ct)
    {
        var neispravne = stavke.Where(s => !van.Contains(s.UredjajId) && odgovori.TryGetValue(s.StavkaId, out var o) && o.Vrednost == 1).ToList();
        if (neispravne.Count == 0) return;
        var podesavanja = await new PodesavanjaHandler(db).Handle(new GetPodesavanjaQuery(), ct);
        var od = p.PlaniraniDatum.AddDays(-podesavanja.PonovljeniKvarDana + 1);
        foreach (var s in neispravne)
        {
            var ranije = await (
                    from o in db.Odgovori
                    join x in db.Pregledi on o.PregledId equals x.Id
                    join st in db.SablonStavke on o.StavkaId equals st.Id
                    where x.Id != p.Id && x.PredatAt != null && x.Status != StatusPregleda.Ponisten && o.Dorada == x.Dorada && o.Vrednost == 1
                          && st.UredjajId == s.UredjajId && st.AktivnostId == s.AktivnostId && x.PlaniraniDatum >= od && x.PlaniraniDatum <= p.PlaniraniDatum
                    select x.Id)
                .Distinct().CountAsync(ct);
            if (ranije + 1 >= podesavanja.PonovljeniKvarBroj)
                await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.PonovljeniKvar, $"Ponovljeni kvar: {s.Naziv} {s.Oznaka}".TrimEnd(),
                    $"{s.Aktivnost} je neispravno {ranije + 1} puta u poslednjih {podesavanja.PonovljeniKvarDana} dana (pregled {p.Broj}).",
                    $"/uredjaji/{s.UredjajId}", $"ponovljeni_kvar:{s.UredjajId}:{s.AktivnostId}:{p.Id}", clock.UtcNow, ct);
        }
    }

    /// <summary>Važeća verzija za danas; zakazana čiji je dan došao važi i pre nego što je zakazana provera prebaci (do minut kašnjenja).</summary>
    private Task<int?> VazecaVerzijaAsync(string tip, CancellationToken ct)
    {
        var danas = clock.Today;
        return db.SablonVerzije
            .Where(v => v.Sablon!.Tip == tip && (v.Status == StatusVerzije.Objavljena || (v.Status == StatusVerzije.Zakazana && v.VaziOd <= danas)))
            .OrderByDescending(v => v.Broj).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>Predaja (R4.3, R3.15): sve stavke odgovorene osim uređaja van funkcije; napomena uz svaki neispravan.</summary>
    private async Task PredajAsync(Pregled p, IReadOnlyList<StavkaInfo> stavke, RasporedKontekst k, DateTime predajaAt, CancellationToken ct)
    {
        var odgovori = await db.Odgovori.Where(o => o.PregledId == p.Id && o.Dorada == p.Dorada).ToDictionaryAsync(o => o.StavkaId, ct);
        var van = (await db.UredjajiVanFunkcije.Where(v => v.PregledId == p.Id).Select(v => v.UredjajId).ToListAsync(ct)).ToHashSet();
        var potrebne = stavke.Where(s => !van.Contains(s.UredjajId)).ToList();
        var bezOdgovora = potrebne.Where(s => !odgovori.ContainsKey(s.StavkaId)).ToList();
        var bezNapomene = potrebne.Count(s => odgovori.TryGetValue(s.StavkaId, out var o) && o.Vrednost == 1 && string.IsNullOrWhiteSpace(o.Napomena));

        if (p.Ucesnici.Count > 0 && bezOdgovora.Count > 0)
        {
            var sekcije = bezOdgovora.Select(s => s.Sekcija).Distinct().Select(sek => $"sekcija {sek} ({KoRadiSekciju(p, sek, k).Radi})");
            throw new BusinessRuleException($"PZ se predaje kad su sve sekcije gotove. Nedostaje: {string.Join(", ", sekcije)}.");
        }
        if (bezOdgovora.Count > 0 || bezNapomene > 0)
            throw new BusinessRuleException($"Pregled još nije potpun: nedostaje {bezOdgovora.Count} odgovora i {bezNapomene} obaveznih napomena.");
        var bezPolja = potrebne.Where(s => odgovori.ContainsKey(s.StavkaId))
            .SelectMany(s => NedostajucaPolja(s, odgovori[s.StavkaId]).Select(n => $"{n} – {s.Naziv} {s.Oznaka}".TrimEnd())).ToList();
        if (bezPolja.Count > 0)
            throw new BusinessRuleException($"Nedostaje {bezPolja.Count} {(bezPolja.Count == 1 ? "obavezno polje" : "obaveznih polja")} ({string.Join("; ", bezPolja.Take(5))}{(bezPolja.Count > 5 ? "; …" : "")}).");

        await ObradiKvaroveAsync(p, stavke, odgovori, van, ct);
        await PonovljeniKvarAsync(p, stavke, odgovori, van, ct);

        // Vreme dorade ne ulazi u aktivno vreme pregleda (R6.6) — računa se samo pri prvoj predaji.
        if (p.Dorada > 0)
        {
            p.Status = StatusPregleda.CekaOveru;
            p.PredatAt = clock.UtcNow;
            PregledIstorija.Dodaj(db, p, "predaja", new { dorada = p.Dorada }, currentUser.UserId, clock.UtcNow);
            return;
        }
        var granica = TimeSpan.FromMinutes((await new PodesavanjaHandler(db).Handle(new GetPodesavanjaQuery(), ct)).GranicaPauzeMin);
        // Predaja je i sama operacija (još nije upisana), pa ulazi u raspon.
        var vremena = await db.SinhronizacijaOperacije.Where(o => o.PregledId == p.Id).Select(o => o.KlijentAt).ToListAsync(ct);
        vremena.Add(predajaAt);
        (p.AktivnoSekundi, p.Delova) = AktivnoVreme.Izracunaj(vremena, granica);
        p.Status = StatusPregleda.CekaOveru;
        p.PredatAt = clock.UtcNow;
        PregledIstorija.Dodaj(db, p, "predaja", new { aktivnoSekundi = p.AktivnoSekundi, delova = p.Delova }, currentUser.UserId, clock.UtcNow);
    }

    // ─── Pomoćno ──────────────────────────────────────────────────────────

    /// <summary>Stavka verzije sa svim što ekran i provere traže.</summary>
    private sealed record StavkaInfo(int StavkaId, string Sekcija, int UredjajId, string Naziv, string Oznaka, string Aktivnost, string Ok, string Nok,
        IReadOnlyList<PoljeDto> Polja, Guid Kljuc, int AktivnostId)
    {
        /// <summary>Tekst uputstva aktivnosti.</summary>
        public string? Uputstvo { get; init; }

        /// <summary>Broj slika uputstva.</summary>
        public int UputstvoSlika { get; init; }
    }

    private async Task<IReadOnlyList<StavkaInfo>> StavkeAsync(int verzijaId, CancellationToken ct)
    {
        var redovi = await db.SablonStavke.AsNoTracking().Where(s => s.VerzijaId == verzijaId).OrderBy(s => s.Redosled)
            .Select(s => new
            {
                s.Id, s.Kljuc, s.AktivnostId, Sekcija = db.Sekcije.Where(x => x.Id == s.SekcijaId).Select(x => x.Sifra).First(), s.UredjajId,
                s.Uredjaj!.Naziv, s.Uredjaj.Oznaka, Aktivnost = s.Aktivnost!.Naziv, s.Aktivnost.OdgovorIspravan, s.Aktivnost.OdgovorNeispravan,
                Polja = s.Polja.Select(p => p.DodatnoPolje!).ToList(), s.Aktivnost.UputstvoTekst, s.Aktivnost.UputstvoSlike,
            })
            .ToListAsync(ct);
        return redovi.Select(r => new StavkaInfo(r.Id, r.Sekcija, r.UredjajId, r.Naziv, r.Oznaka, r.Aktivnost, r.OdgovorIspravan, r.OdgovorNeispravan,
            r.Polja.Select(p => new PoljeDto(p.Id, p.Naziv, p.Tip, p.Jedinica, p.Oznake, p.Prikaz, p.Obavezno)).ToList(), r.Kljuc, r.AktivnostId)
        {
            Uputstvo = r.UputstvoTekst, UputstvoSlika = r.UputstvoSlike.Length,
        }).ToList();
    }

    private async Task<Pregled> UcitajPregledAsync(int id, CancellationToken ct) =>
        await db.Pregledi.Include(p => p.Ucesnici).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Pregled ne postoji.");

    /// <summary>Pregled popunjava samo dodeljeni inženjer ili učesnik PZ-a (R1.8, R3.13).</summary>
    private void ProveriUcesnika(Pregled p)
    {
        if (p.DodeljenId != currentUser.UserId && p.Ucesnici.All(u => u.KorisnikId != currentUser.UserId))
            throw new ForbiddenException("Ovaj pregled je dodeljen drugom inženjeru.");
    }

    /// <summary>Ko radi sekciju: učesnik kome je dodeljena, inače dodeljeni inženjer (R3.14, R3.15).</summary>
    private (bool Moja, string Radi) KoRadiSekciju(Pregled p, string sekcija, RasporedKontekst k)
    {
        var ucesnik = p.Ucesnici.FirstOrDefault(u => u.Sekcije.Contains(sekcija));
        var radiId = ucesnik?.KorisnikId ?? p.DodeljenId;
        return (radiId == currentUser.UserId, k.Ime(radiId));
    }

    private void ProveriSekciju(Pregled p, string sekcija, RasporedKontekst k)
    {
        var (moja, radi) = KoRadiSekciju(p, sekcija, k);
        if (!moja) throw new BusinessRuleException($"Sekciju {sekcija} radi {radi}.");
    }

    /// <summary>Uređaji koji su bili van funkcije na poslednjem predatom pregledu koji ih sadrži (R4.6).</summary>
    private async Task<HashSet<int>> ProsliPutVanFunkcijeAsync(Pregled p, IReadOnlyList<int> uredjaji, CancellationToken ct)
    {
        var poslednji = await (
                from pr in db.Pregledi.AsNoTracking()
                where pr.PredatAt != null && pr.Id != p.Id && pr.Status != StatusPregleda.Ponisten   // poništen se ne računa (zamerka 41)
                join s in db.SablonStavke.AsNoTracking() on pr.SablonVerzijaId equals s.VerzijaId
                where uredjaji.Contains(s.UredjajId)
                select new { s.UredjajId, pr.Id, pr.PredatAt })
            .Distinct().ToListAsync(ct);
        var poUredjaju = poslednji.GroupBy(x => x.UredjajId).Select(g => g.OrderByDescending(x => x.PredatAt).First()).ToList();
        var ids = poUredjaju.Select(x => x.Id).Distinct().ToList();
        var van = await db.UredjajiVanFunkcije.AsNoTracking().Where(v => ids.Contains(v.PregledId)).Select(v => new { v.PregledId, v.UredjajId }).ToListAsync(ct);
        return poUredjaju.Where(x => van.Any(v => v.PregledId == x.Id && v.UredjajId == x.UredjajId)).Select(x => x.UredjajId).ToHashSet();
    }
}
