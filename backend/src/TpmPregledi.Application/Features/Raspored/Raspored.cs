using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Raspored;

/// <summary>Pregled u kalendaru i rasporedu.</summary>
/// <param name="Id">ID.</param>
/// <param name="Broj">Broj, npr. DP1-2026-0142.</param>
/// <param name="Tip">DP1…PZ4.</param>
/// <param name="PlaniraniDatum">Planirani dan.</param>
/// <param name="PrvobitniDatum">Prvobitni dan (razlikuje se ako je prenet).</param>
/// <param name="Status">Status pregleda.</param>
/// <param name="DodeljenId">Inženjer.</param>
/// <param name="Dodeljen">Ime inženjera.</param>
/// <param name="Ucesnici">Drugi inženjeri na PZ-u (ime i sekcije).</param>
/// <param name="Napomena">Napomena (npr. uz promenu PZ-a).</param>
/// <param name="InzenjerOdsutan">Planiran pregled je ostao inženjeru koji je tog dana odsutan (zamerka 3).</param>
/// <param name="ZahtevNaCekanju">Vrsta zahteva inženjera koji čeka odluku (<c>prebacivanje</c>, <c>raniji_pocetak</c>) ili null.</param>
/// <param name="Propusten">Propušten i automatski prenet (crveno „nije urađen – prenet“); inače je pomeranje ručno (sivo).</param>
/// <param name="SlobodanDan">Posle izmene: dan koji je ostao bez DP-a usred niza — UI nudi „Povuci raspored“ (zamerka 29).</param>
public sealed record PregledKalendarDto(
    int Id, string Broj, string Tip, DateOnly PlaniraniDatum, DateOnly PrvobitniDatum, string Status,
    string DodeljenId, string Dodeljen, IReadOnlyList<UcesnikDto> Ucesnici, string? Napomena,
    bool InzenjerOdsutan = false, string? ZahtevNaCekanju = null, bool Propusten = false, DateOnly? SlobodanDan = null);

/// <summary>Učesnik PZ-a.</summary>
/// <param name="KorisnikId">ID.</param>
/// <param name="Ime">Ime.</param>
/// <param name="Sekcije">Sekcije koje radi.</param>
public sealed record UcesnikDto(string KorisnikId, string Ime, IReadOnlyList<string> Sekcije);

/// <summary>Neradni dan u kalendaru.</summary>
/// <param name="Datum">Datum.</param>
/// <param name="Naziv">Naziv.</param>
public sealed record NeradniDanKratkoDto(DateOnly Datum, string Naziv);

/// <summary>Odobreno odsustvo u kalendaru.</summary>
/// <param name="KorisnikId">ID.</param>
/// <param name="Ime">Ime.</param>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
/// <param name="Vrsta">Vrsta.</param>
public sealed record OdsustvoKratkoDto(string KorisnikId, string Ime, DateOnly Od, DateOnly Do, string Vrsta);

/// <summary>Raspored za period: pregledi, neradni dani, odsustva.</summary>
/// <param name="Pregledi">Pregledi čiji planirani ili prvobitni dan pada u period.</param>
/// <param name="NeradniDani">Neradni dani u periodu.</param>
/// <param name="Odsustva">Odobrena odsustva koja seku period.</param>
/// <param name="DpNaDanPz">Da li se DP radi i na dan PZ-a (R3.9) — ekran tada ne upozorava na pomeranje DP-ova.</param>
public sealed record RasporedDto(IReadOnlyList<PregledKalendarDto> Pregledi, IReadOnlyList<NeradniDanKratkoDto> NeradniDani, IReadOnlyList<OdsustvoKratkoDto> Odsustva, bool DpNaDanPz = false);

/// <summary>Stavka predloga ili potvrde rasporeda.</summary>
/// <param name="Datum">Dan.</param>
/// <param name="Tip">DP1 ili DP2.</param>
/// <param name="InzenjerId">Inženjer.</param>
public sealed record StavkaRasporeda(DateOnly Datum, string Tip, string? InzenjerId);

/// <summary>Stavka predloga sa imenom.</summary>
/// <param name="Datum">Dan.</param>
/// <param name="Tip">DP1 ili DP2.</param>
/// <param name="InzenjerId">Predloženi inženjer.</param>
/// <param name="Inzenjer">Ime.</param>
/// <param name="Rupa">Posle ovog dana već postoje planirani DP-ovi (dan je rupa usred niza, zamerka 30).</param>
/// <param name="DatumPre">Dan poslednjeg postojećeg DP-a pre ovog dana (za preračun tipa kad se dan odštiklira).</param>
/// <param name="TipPre">Tip tog DP-a.</param>
public sealed record PredlogStavkaDto(DateOnly Datum, string Tip, string? InzenjerId, string Inzenjer, bool Rupa = false, DateOnly? DatumPre = null, string? TipPre = null);

/// <summary>Kalendar i raspored za period (R3.22).</summary>
/// <param name="Od">Prvi dan.</param>
/// <param name="Do">Poslednji dan.</param>
public sealed record GetRasporedQuery(DateOnly Od, DateOnly Do) : IRequest<RasporedDto>;

/// <summary>Predlog DP-a za dane od <paramref name="Od"/> (a najranije od danas, zamerka 26) do kraja te nedelje (R3.10); ništa ne upisuje.</summary>
/// <param name="Od">Prvi dan (obično ponedeljak).</param>
public sealed record PredlogRasporedaQuery(DateOnly Od) : IRequest<IReadOnlyList<PredlogStavkaDto>>;

/// <summary>Potvrda rasporeda: pravi preglede sa brojevima (R6.9).</summary>
/// <param name="Stavke">Dani i inženjeri (tip računa server od DP-a dan pre, zamerka 29).</param>
/// <param name="Uskladi">Dan koji je rupa usred niza ne dobija nov DP nego se niz povuče u njega („Umetni i pomeri naredne“, zamerka 30).</param>
public sealed record PotvrdiRasporedCommand(IReadOnlyList<StavkaRasporeda> Stavke, bool Uskladi = false) : IRequest<IReadOnlyList<PregledKalendarDto>>;

/// <summary>Inženjer za izbor u rasporedu.</summary>
/// <param name="Id">ID.</param>
/// <param name="Ime">Ime i prezime.</param>
public sealed record InzenjerDto(string Id, string Ime);

/// <summary>Inženjeri u rotaciji (aktivni, sa pravom popunjavanja), po imenu.</summary>
public sealed record GetInzenjeriQuery : IRequest<IReadOnlyList<InzenjerDto>>;

/// <summary>Koji PZ je sledeći po redu (R3.8).</summary>
public sealed record SledeciPzQuery : IRequest<string>;

/// <summary>Zakazivanje PZ-a (R3.7, R3.8): bez tipa = sledeći po redu; drugi tip = „Promeni PZ“.</summary>
/// <param name="Datum">Dan.</param>
/// <param name="Tip">PZ1…PZ4 ili null za sledeći.</param>
/// <param name="InzenjerId">Inženjer.</param>
/// <param name="Napomena">Napomena (opciono).</param>
public sealed record ZakaziPzCommand(DateOnly Datum, string? Tip, string InzenjerId, string? Napomena) : IRequest<PregledKalendarDto>;

/// <summary>Prenos propuštenog DP-a za dan (R3.2–R3.5); pokreće ga pozadinski posao posle kraja radnog dana.</summary>
/// <param name="Dan">Dan čiji se nezapočet DP prenosi.</param>
public sealed record PrenesiCommand(DateOnly Dan) : IRequest<int>;

/// <summary>Ručna izmena dodele i/ili datuma (R3.6, R3.13).</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="InzenjerId">Novi inženjer (null = bez promene).</param>
/// <param name="Datum">Novi datum (null = bez promene).</param>
public sealed record IzmeniDodeluCommand(int PregledId, string? InzenjerId, DateOnly? Datum) : IRequest<PregledKalendarDto>;

/// <summary>„Dodaj inženjera“ na PZ sa sekcijama (R3.14); prazne sekcije = ukloni učesnika.</summary>
/// <param name="PregledId">PZ.</param>
/// <param name="KorisnikId">Inženjer.</param>
/// <param name="Sekcije">Šifre sekcija.</param>
public sealed record PostaviUcesnikaCommand(int PregledId, string KorisnikId, IReadOnlyList<string> Sekcije) : IRequest<PregledKalendarDto>;

/// <summary>Oblik ulaza za PZ.</summary>
public sealed class ZakaziPzValidator : AbstractValidator<ZakaziPzCommand>
{
    /// <summary>Pravila.</summary>
    public ZakaziPzValidator()
    {
        RuleFor(x => x.Tip).Must(t => t is null or "PZ1" or "PZ2" or "PZ3" or "PZ4").WithMessage("Tip mora biti PZ1, PZ2, PZ3 ili PZ4.");
        RuleFor(x => x.InzenjerId).NotEmpty().WithMessage("Inženjer je obavezan.");
        RuleFor(x => x.Napomena).MaximumLength(500).WithMessage("Napomena može imati najviše 500 karaktera.");
    }
}

/// <summary>Oblik ulaza za potvrdu.</summary>
public sealed class PotvrdiRasporedValidator : AbstractValidator<PotvrdiRasporedCommand>
{
    /// <summary>Pravila.</summary>
    public PotvrdiRasporedValidator()
    {
        RuleFor(x => x.Stavke).NotEmpty().WithMessage("Raspored nema nijedan dan.");
        RuleForEach(x => x.Stavke).Must(s => s.Tip is "DP1" or "DP2").WithMessage("Raspored sadrži samo DP1 i DP2.");
    }
}

/// <summary>Raspored, PZ, prenos i ručne izmene.</summary>
public sealed partial class RasporedHandler(IAppDbContext db, IBrojacService brojac, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetRasporedQuery, RasporedDto>,
    IRequestHandler<PredlogRasporedaQuery, IReadOnlyList<PredlogStavkaDto>>,
    IRequestHandler<PotvrdiRasporedCommand, IReadOnlyList<PregledKalendarDto>>,
    IRequestHandler<SledeciPzQuery, string>,
    IRequestHandler<GetInzenjeriQuery, IReadOnlyList<InzenjerDto>>,
    IRequestHandler<ZakaziPzCommand, PregledKalendarDto>,
    IRequestHandler<PrenesiCommand, int>,
    IRequestHandler<IzmeniDodeluCommand, PregledKalendarDto>,
    IRequestHandler<PostaviUcesnikaCommand, PregledKalendarDto>
{
    /// <inheritdoc />
    public async Task<RasporedDto> Handle(GetRasporedQuery req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var pregledi = await db.Pregledi.AsNoTracking().Include(p => p.Ucesnici)
            .Where(p => (p.PlaniraniDatum >= req.Od && p.PlaniraniDatum <= req.Do) || (p.PrvobitniDatum >= req.Od && p.PrvobitniDatum <= req.Do))
            .OrderBy(p => p.PlaniraniDatum).ThenBy(p => p.Tip).ToListAsync(ct);
        var neradni = await db.NeradniDani.AsNoTracking().Where(n => n.Datum >= req.Od && n.Datum <= req.Do)
            .OrderBy(n => n.Datum).Select(n => new NeradniDanKratkoDto(n.Datum, n.Naziv)).ToListAsync(ct);
        var odsustva = k.Odsustva.Where(o => o.Od <= req.Do && o.Do >= req.Od)
            .Select(o => new OdsustvoKratkoDto(o.KorisnikId, k.Ime(o.KorisnikId), o.Od, o.Do, o.Vrsta)).ToList();
        return new RasporedDto(pregledi.Select(p => Dto(p, k)).ToList(), neradni, odsustva, k.DpNaDanPz);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PredlogStavkaDto>> Handle(PredlogRasporedaQuery req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        // Do petka te nedelje (subota/nedelja kao početak → petak sledeće); dani koji su prošli se ne nude (zamerka 26).
        var kraj = req.Od.AddDays(((int)DayOfWeek.Friday - (int)req.Od.DayOfWeek + 7) % 7);
        var od = req.Od < clock.Today ? clock.Today : req.Od;
        var poslednji = await PoslednjiDpAsync(od, ct);
        var postojeci = (await db.Pregledi.AsNoTracking()
            .Where(p => p.Tip.StartsWith("DP") && p.Status != StatusPregleda.Ponisten && p.PlaniraniDatum >= od && p.PlaniraniDatum <= kraj)
            .ToListAsync(ct)).ToDictionary(p => p.PlaniraniDatum);
        var poslednjiPlaniran = await db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("DP") && p.Status == StatusPregleda.Planiran)
            .MaxAsync(p => (DateOnly?)p.PlaniraniDatum, ct);

        // Tip i rotacija idu od DP-a dan pre — i kad je taj DP u istoj nedelji — da ne nastanu dva ista zaredom (zamerka 29).
        string? tip = poslednji?.Tip, inz = poslednji?.DodeljenId;
        DateOnly? datumPre = poslednji?.PlaniraniDatum;
        string? tipPre = poslednji?.Tip;
        var predlog = new List<PredlogStavkaDto>();
        for (var d = od; d <= kraj; d = d.AddDays(1))
        {
            if (postojeci.TryGetValue(d, out var p))
            {
                (tip, inz, datumPre, tipPre) = (p.Tip, p.DodeljenId, d, p.Tip);
                continue;
            }
            if (!k.JeDanZaDp(d)) continue;
            tip = RasporedPravila.SledeciDp(tip);
            inz = k.SledeciInzenjer(d, inz);
            predlog.Add(new PredlogStavkaDto(d, tip, inz, k.Ime(inz), poslednjiPlaniran > d, datumPre, tipPre));
        }
        return predlog;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PregledKalendarDto>> Handle(PotvrdiRasporedCommand req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var dani = req.Stavke.Select(s => s.Datum).ToList();
        var zauzeti = (await db.Pregledi.Where(p => p.Tip.StartsWith("DP") && p.Status != StatusPregleda.Ponisten && dani.Contains(p.PlaniraniDatum))
            .Select(p => p.PlaniraniDatum).ToListAsync(ct)).ToHashSet();

        var greske = new List<string>();
        foreach (var s in req.Stavke)
        {
            if (s.Datum < clock.Today) greske.Add(PorukaProsliDan);
            if (!k.JeDanZaDp(s.Datum)) greske.Add($"{s.Datum:dd.MM.yyyy} nije dan za DP (vikend, neradni dan ili dan PZ-a).");
            if (zauzeti.Contains(s.Datum) || dani.Count(d => d == s.Datum) > 1) greske.Add($"Za {s.Datum:dd.MM.yyyy} DP je već u rasporedu.");
            if (s.InzenjerId is null || !k.Inzenjeri.Contains(s.InzenjerId)) greske.Add($"Za {s.Datum:dd.MM.yyyy} izaberite aktivnog inženjera.");
            else if (k.Odsustva.FirstOrDefault(o => o.KorisnikId == s.InzenjerId && o.Pokriva(s.Datum)) is { } ods) greske.Add(k.PorukaOdsutan(s.InzenjerId, s.Datum, ods));
        }
        if (greske.Count > 0) throw new BusinessRuleException(greske.Distinct().ToList());

        var sada = clock.UtcNow;
        var novi = new List<Pregled>();
        foreach (var s in req.Stavke.OrderBy(s => s.Datum))
        {
            if (req.Uskladi && await db.Pregledi.AnyAsync(p => p.Tip.StartsWith("DP") && p.Status == StatusPregleda.Planiran && p.PlaniraniDatum > s.Datum, ct))
            {
                // Rupa usred niza: niz se povuče u nju, a dan koji ostane slobodan na kraju dobija nov DP samo ako je u istoj nedelji.
                var kraj = s.Datum.AddDays(((int)DayOfWeek.Friday - (int)s.Datum.DayOfWeek + 7) % 7);
                var (oslobodjen, inzenjer) = await PovuciAsync(s.Datum, s.InzenjerId!, "umetanje", k, ct);
                if (oslobodjen is { } f && f <= kraj && inzenjer is not null) novi.Add(await NoviDpAsync(f, inzenjer, k, sada, ct));
                await db.SaveChangesAsync(ct);
                continue;
            }
            novi.Add(await NoviDpAsync(s.Datum, s.InzenjerId!, k, sada, ct));
            // Sledeća rupa traži niz iz baze, pa i ovaj DP mora biti upisan.
            if (req.Uskladi) await db.SaveChangesAsync(ct);
        }
        foreach (var g in novi.GroupBy(p => p.DodeljenId))
            await Obavesti.DodajAsync(db, [g.Key], VrsteObavestenja.Dodela, "Novi pregledi u rasporedu",
                $"Dodeljeni su vam: {string.Join(", ", g.Select(p => $"{p.Broj} ({p.PlaniraniDatum:dd.MM.yyyy})"))}.", "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return novi.Select(p => Dto(p, k)).ToList();
    }

    /// <summary>Nov DP za dan; tip je sledeći posle poslednjeg DP-a pre tog dana (i onih upravo napravljenih).</summary>
    private async Task<Pregled> NoviDpAsync(DateOnly d, string inzenjerId, RasporedKontekst k, DateTime sada, CancellationToken ct)
    {
        var pre = db.Pregledi.Local.Where(p => p.JeDp && p.Status != StatusPregleda.Ponisten && p.PlaniraniDatum < d)
            .OrderByDescending(p => p.PlaniraniDatum).FirstOrDefault();
        var izBaze = await PoslednjiDpAsync(d, ct);
        if (pre is null || (izBaze is not null && izBaze.PlaniraniDatum > pre.PlaniraniDatum)) pre = izBaze;
        var tip = RasporedPravila.SledeciDp(pre?.Tip);
        var p = new Pregled
        {
            Broj = await brojac.SledeciAsync(tip, d.Year, ct), Tip = tip, PlaniraniDatum = d, PrvobitniDatum = d, DodeljenId = inzenjerId, KreiranoAt = sada,
        };
        db.Pregledi.Add(p);
        PregledIstorija.Dodaj(db, p, "dodela", new { inzenjer = k.Ime(p.DodeljenId), inzenjerId = p.DodeljenId, datum = p.PlaniraniDatum }, currentUser.UserId, sada);
        return p;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InzenjerDto>> Handle(GetInzenjeriQuery _, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        return k.Inzenjeri.Select(i => new InzenjerDto(i, k.Ime(i))).ToList();
    }

    /// <inheritdoc />
    public async Task<string> Handle(SledeciPzQuery _, CancellationToken ct) => RasporedPravila.SledeciPz(await PoslednjiPzAsync(ct));

    /// <summary>
    /// Zakazivanje i pomeranje u prošlost nije dozvoljeno (zamerka 18): takav pregled bi se odmah započeo, a sve bi se upisalo
    /// kao urađeno tog dana (kvarovi, ispunjenje plana, istorija uređaja). Promena samo inženjera na starom pregledu ostaje dozvoljena.
    /// </summary>
    public const string PorukaProsliDan = "Ne može se zakazati za dan koji je prošao.";

    /// <inheritdoc />
    public async Task<PregledKalendarDto> Handle(ZakaziPzCommand req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        if (req.Datum < clock.Today) throw new BusinessRuleException(PorukaProsliDan);
        k.ProveriInzenjera(req.InzenjerId);
        k.ProveriDostupnost(req.InzenjerId, req.Datum);
        var sledeci = RasporedPravila.SledeciPz(await PoslednjiPzAsync(ct));
        var tip = req.Tip ?? sledeci;

        var zakazan = await db.Pregledi.AsNoTracking().FirstOrDefaultAsync(p => p.Tip == tip && p.Status == StatusPregleda.Planiran, ct);
        if (zakazan is not null) throw new BusinessRuleException($"Taj PZ je već zakazan za {zakazan.PlaniraniDatum:dd.MM.yyyy}.");
        if (await db.Pregledi.AnyAsync(p => p.Tip.StartsWith("PZ") && p.PlaniraniDatum == req.Datum && p.Status != StatusPregleda.Ponisten, ct))
            throw new BusinessRuleException($"Za {req.Datum:dd.MM.yyyy} je već zakazan PZ.");

        var sada = clock.UtcNow;
        var p = new Pregled
        {
            Broj = await brojac.SledeciAsync(tip, req.Datum.Year, ct), Tip = tip, PlaniraniDatum = req.Datum, PrvobitniDatum = req.Datum,
            DodeljenId = req.InzenjerId, KreiranoAt = sada, Napomena = string.IsNullOrWhiteSpace(req.Napomena) ? null : req.Napomena.Trim(),
        };
        db.Pregledi.Add(p);
        PregledIstorija.Dodaj(db, p, tip == sledeci ? "dodela" : "promena_pz",
            new { inzenjer = k.Ime(p.DodeljenId), inzenjerId = p.DodeljenId, datum = p.PlaniraniDatum, sledeciPoRedu = sledeci, napomena = p.Napomena }, currentUser.UserId, sada);
        await Obavesti.DodajAsync(db, [p.DodeljenId], VrsteObavestenja.Dodela, $"Zakazan {p.Broj}", $"Dodeljen vam je {p.Broj} za {p.PlaniraniDatum:dd.MM.yyyy}.", "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);

        // Na dan PZ-a DP se ne radi (R3.9): planiran DP tog dana i svi posle njega se pomeraju kao kod prenosa.
        if (!k.DpNaDanPz) await PomeriOdAsync(req.Datum, "pz", ct);
        return Dto(p, k);
    }

    /// <inheritdoc />
    public Task<int> Handle(PrenesiCommand req, CancellationToken ct) => PomeriOdAsync(req.Dan, "propusten", ct);

    /// <inheritdoc />
    public async Task<PregledKalendarDto> Handle(IzmeniDodeluCommand req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == req.PregledId, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        if (p.Status != StatusPregleda.Planiran && req.Datum is not null)
            throw new BusinessRuleException("Datum se menja samo pregledu koji nije započet.");
        if (p.Status is not (StatusPregleda.Planiran or StatusPregleda.URadu))
            throw new BusinessRuleException("Dodela se menja samo pregledu koji nije predat.");

        var stari = new { inzenjer = k.Ime(p.DodeljenId), inzenjerId = p.DodeljenId, datum = p.PlaniraniDatum };
        var stariId = p.DodeljenId;
        var stariDatum = p.PlaniraniDatum;
        if (req.InzenjerId is not null)
        {
            k.ProveriInzenjera(req.InzenjerId);
            p.DodeljenId = req.InzenjerId;
        }
        if (req.Datum is { } d)
        {
            if (d < clock.Today) throw new BusinessRuleException(PorukaProsliDan);
            if (p.JeDp && !k.JeDanZaDp(d)) throw new BusinessRuleException($"{d:dd.MM.yyyy} nije dan za DP.");
            if (p.JeDp && await db.Pregledi.AnyAsync(x => x.Id != p.Id && x.Tip.StartsWith("DP") && x.PlaniraniDatum == d && x.Status != StatusPregleda.Ponisten, ct))
                throw new BusinessRuleException($"Za {d:dd.MM.yyyy} DP je već u rasporedu.");
            p.PlaniraniDatum = d;
        }
        if (p.DodeljenId != stariId || p.PlaniraniDatum != stariDatum) k.ProveriDostupnost(p.DodeljenId, p.PlaniraniDatum);
        PregledIstorija.Dodaj(db, p, "izmena_rasporeda", new { stari, novi = new { inzenjer = k.Ime(p.DodeljenId), datum = p.PlaniraniDatum } }, currentUser.UserId, clock.UtcNow);
        await ObavestiOPromeniAsync(p, stariId, stariDatum, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        // Pomeren PZ radi isto kao zakazan (R3.9, zamerka 43): DP na novi dan i naredni idu dan napred — i popune dan koji je PZ oslobodio.
        if (!p.JeDp && stariDatum != p.PlaniraniDatum && !k.DpNaDanPz) await PomeriDpOdAsync(db, clock, p.PlaniraniDatum, "pz", ct);
        return Dto(p, k) with { SlobodanDan = stariDatum != p.PlaniraniDatum ? await SlobodanZaPovlacenjeAsync(stariDatum, ct) : null };
    }

    /// <inheritdoc />
    public async Task<PregledKalendarDto> Handle(PostaviUcesnikaCommand req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == req.PregledId, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        if (p.JeDp) throw new BusinessRuleException("Drugi inženjer se dodaje samo na PZ.");
        if (req.KorisnikId == p.DodeljenId) throw new BusinessRuleException("Inženjer je već dodeljen ovom PZ-u.");
        k.ProveriInzenjera(req.KorisnikId);
        if (req.Sekcije.Count > 0) k.ProveriDostupnost(req.KorisnikId, p.PlaniraniDatum);

        var postojeci = p.Ucesnici.FirstOrDefault(u => u.KorisnikId == req.KorisnikId);
        if (req.Sekcije.Count == 0)
        {
            if (postojeci is not null) p.Ucesnici.Remove(postojeci);
        }
        else if (postojeci is null)
        {
            p.Ucesnici.Add(new PregledUcesnik { KorisnikId = req.KorisnikId, Sekcije = [.. req.Sekcije] });
            await Obavesti.DodajAsync(db, [req.KorisnikId], VrsteObavestenja.Dodela, $"Dodati ste na {p.Broj}",
                $"Na {p.Broj} ({p.PlaniraniDatum:dd.MM.yyyy}) radite sekcije {string.Join(", ", req.Sekcije)}.", "/kalendar", null, clock.UtcNow, ct);
        }
        else postojeci.Sekcije = [.. req.Sekcije];

        PregledIstorija.Dodaj(db, p, "ucesnik", new { inzenjer = k.Ime(req.KorisnikId), inzenjerId = req.KorisnikId, sekcije = req.Sekcije }, currentUser.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Dto(p, k);
    }

    /// <summary>Promena dodele ili datuma (R9.3): novi inženjer dobija pregled, stari saznaje da više nije njegov.</summary>
    private async Task ObavestiOPromeniAsync(Pregled p, string stariId, DateOnly stariDatum, DateTime sada, CancellationToken ct)
    {
        if (stariId != p.DodeljenId)
        {
            await Obavesti.DodajAsync(db, [p.DodeljenId], VrsteObavestenja.Dodela, $"Dodeljen vam je {p.Broj}", $"{p.Broj} za {p.PlaniraniDatum:dd.MM.yyyy} je sada vaš.", "/kalendar", null, sada, ct);
            await Obavesti.DodajAsync(db, [stariId], VrsteObavestenja.Dodela, $"{p.Broj} više nije vaš", $"{p.Broj} ({stariDatum:dd.MM.yyyy}) je dodeljen drugom inženjeru.", "/kalendar", null, sada, ct);
        }
        else if (stariDatum != p.PlaniraniDatum)
            await Obavesti.DodajAsync(db, [p.DodeljenId], VrsteObavestenja.Dodela, $"Promenjen datum {p.Broj}", $"{p.Broj} je pomeren sa {stariDatum:dd.MM.yyyy} na {p.PlaniraniDatum:dd.MM.yyyy}.", "/kalendar", null, sada, ct);
    }

    /// <summary>
    /// Pomera DP-ove od dana <paramref name="dan"/>: ako tog dana postoji nezapočet ili započet a nepredat DP, on i svi kasniji
    /// planirani DP-ovi idu jedno mesto napred (<see cref="RasporedPravila.Prenesi"/>). Vraća broj pomerenih pregleda.
    /// </summary>
    private Task<int> PomeriOdAsync(DateOnly dan, string razlog, CancellationToken ct) => PomeriDpOdAsync(db, clock, dan, razlog, ct);

    /// <summary>
    /// Isto kao <see cref="PomeriOdAsync"/>, za druge handlere (odobren raniji početak PZ-a). Zbog PZ-a (<paramref name="razlog"/>
    /// <c>pz</c>) se pomera samo nezapočet DP — započet DP inženjer već radi, pa ostaje na svom danu.
    /// </summary>
    internal static async Task<int> PomeriDpOdAsync(IAppDbContext db, IDateTimeProvider clock, DateOnly dan, string razlog, CancellationToken ct)
    {
        var prvi = await db.Pregledi.FirstOrDefaultAsync(p => p.Tip.StartsWith("DP") && p.PlaniraniDatum == dan
            && (p.Status == StatusPregleda.Planiran || (p.Status == StatusPregleda.URadu && razlog != "pz")), ct);
        if (prvi is null) return 0;

        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var kasniji = await db.Pregledi.Where(p => p.Tip.StartsWith("DP") && p.PlaniraniDatum > dan && p.Status == StatusPregleda.Planiran)
            .OrderBy(p => p.PlaniraniDatum).ToListAsync(ct);
        var niz = new List<Pregled> { prvi };
        niz.AddRange(kasniji);

        var pomeranja = RasporedPravila.Prenesi(niz, k.SledeciDanZaDp, (d, posl) => k.SledeciInzenjer(d, posl) ?? posl ?? prvi.DodeljenId);
        if (razlog == "propusten") prvi.Propusten = true;
        var sada = clock.UtcNow;
        var obavestenja = new List<(string Kome, string Tekst)>();
        foreach (var m in pomeranja)
        {
            obavestenja.Add((m.NoviDodeljenId, $"{m.Pregled.Broj} → {m.NoviDatum:dd.MM.yyyy}"));
            if (m.NoviDodeljenId != m.Pregled.DodeljenId) obavestenja.Add((m.Pregled.DodeljenId, $"{m.Pregled.Broj} više nije vaš (ide {k.Ime(m.NoviDodeljenId)})"));
            var stari = new { datum = m.Pregled.PlaniraniDatum, inzenjer = k.Ime(m.Pregled.DodeljenId) };
            m.Pregled.PlaniraniDatum = m.NoviDatum;
            m.Pregled.DodeljenId = m.NoviDodeljenId;
            PregledIstorija.Dodaj(db, m.Pregled, "prenos", new { razlog, stari, novi = new { datum = m.NoviDatum, inzenjer = k.Ime(m.NoviDodeljenId) } }, null, sada);
        }
        foreach (var g in obavestenja.GroupBy(x => x.Kome))
            await Obavesti.DodajAsync(db, [g.Key], VrsteObavestenja.Dodela, "Raspored je pomeren", $"{string.Join("; ", g.Select(x => x.Tekst))}.", "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return pomeranja.Count;
    }

    private Task<Pregled?> PoslednjiDpAsync(DateOnly pre, CancellationToken ct) =>
        db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("DP") && p.Status != StatusPregleda.Ponisten && p.PlaniraniDatum < pre)
            .OrderByDescending(p => p.PlaniraniDatum).FirstOrDefaultAsync(ct);

    private async Task<string?> PoslednjiPzAsync(CancellationToken ct) =>
        await db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("PZ") && p.Status != StatusPregleda.Ponisten)
            .OrderByDescending(p => p.PlaniraniDatum).ThenByDescending(p => p.Id).Select(p => p.Tip).FirstOrDefaultAsync(ct);

    /// <summary>DTO za kalendar.</summary>
    public static PregledKalendarDto Dto(Pregled p, RasporedKontekst k) =>
        new(p.Id, p.Broj, p.Tip, p.PlaniraniDatum, p.PrvobitniDatum, p.Status, p.DodeljenId, k.Ime(p.DodeljenId),
            p.Ucesnici.Select(u => new UcesnikDto(u.KorisnikId, k.Ime(u.KorisnikId), u.Sekcije)).ToList(), p.Napomena,
            p.Status == StatusPregleda.Planiran && k.JeOdsutan(p.DodeljenId, p.PlaniraniDatum), k.ZahteviNaCekanju.GetValueOrDefault(p.Id), p.Propusten);
}
