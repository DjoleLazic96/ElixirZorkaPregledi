using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Raspored;

// ─── Neradni dani ─────────────────────────────────────────────────────────

/// <summary>Neradni dan.</summary>
/// <param name="Id">ID.</param>
/// <param name="Datum">Datum.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Vrsta"><c>drzavni</c>, <c>verski</c>, <c>fabrika</c>.</param>
public sealed record NeradniDanDto(int Id, DateOnly Datum, string Naziv, string Vrsta);

/// <summary>Neradni dani u godini.</summary>
/// <param name="Godina">Godina.</param>
public sealed record GetNeradniDaniQuery(int Godina) : IRequest<IReadOnlyList<NeradniDanDto>>;

/// <summary>Unos od–do pravi po jedan red za svaki dan (R3.17).</summary>
/// <param name="Od">Prvi dan.</param>
/// <param name="Do">Poslednji dan.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="PomeriDp">Nezapočeti DP-ovi sa tih dana i naredni idu dalje u nizu (zamerka 52); ekran pita pre unosa.</param>
public sealed record UnesiNeradneDaneCommand(DateOnly Od, DateOnly Do, string Naziv, string Vrsta, bool PomeriDp = false) : IRequest<IReadOnlyList<NeradniDanDto>>;

/// <summary>Brisanje neradnog dana (pogrešan unos).</summary>
/// <param name="Id">ID.</param>
public sealed record ObrisiNeradniDanCommand(int Id) : IRequest;

/// <summary>Oblik ulaza.</summary>
public sealed class UnesiNeradneDaneValidator : AbstractValidator<UnesiNeradneDaneCommand>
{
    /// <summary>Pravila.</summary>
    public UnesiNeradneDaneValidator()
    {
        RuleFor(x => x.Naziv).NotEmpty().WithMessage("Naziv je obavezan.").MaximumLength(200).WithMessage("Naziv može imati najviše 200 karaktera.");
        RuleFor(x => x.Vrsta).Must(v => v is "drzavni" or "verski" or "fabrika").WithMessage("Vrsta mora biti državni praznik, verski praznik ili neradni dan fabrike.");
        RuleFor(x => x).Must(x => x.Do >= x.Od).WithMessage("Datum „do“ ne može biti pre datuma „od“.")
            .Must(x => x.Do.DayNumber - x.Od.DayNumber < 31).WithMessage("Najviše 31 dan odjednom.");
    }
}

/// <summary>Neradni dani.</summary>
public sealed class NeradniDaniHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetNeradniDaniQuery, IReadOnlyList<NeradniDanDto>>,
    IRequestHandler<UnesiNeradneDaneCommand, IReadOnlyList<NeradniDanDto>>,
    IRequestHandler<ObrisiNeradniDanCommand>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<NeradniDanDto>> Handle(GetNeradniDaniQuery req, CancellationToken ct) =>
        await db.NeradniDani.AsNoTracking().Where(n => n.Datum.Year == req.Godina).OrderBy(n => n.Datum)
            .Select(n => new NeradniDanDto(n.Id, n.Datum, n.Naziv, n.Vrsta)).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<NeradniDanDto>> Handle(UnesiNeradneDaneCommand req, CancellationToken ct)
    {
        var dani = Enumerable.Range(0, req.Do.DayNumber - req.Od.DayNumber + 1).Select(i => req.Od.AddDays(i)).ToList();
        var postoje = await db.NeradniDani.Where(n => dani.Contains(n.Datum)).Select(n => n.Datum).ToListAsync(ct);
        if (postoje.Count > 0)
            throw new BusinessRuleException($"Već uneto: {string.Join(", ", postoje.OrderBy(d => d).Select(d => d.ToString("dd.MM.yyyy")))}.");

        var novi = dani.Select(d => new NeradniDan { Datum = d, Naziv = req.Naziv.Trim(), Vrsta = req.Vrsta, UneoId = currentUser.UserId }).ToList();
        db.NeradniDani.AddRange(novi);
        await db.SaveChangesAsync(ct);
        if (req.PomeriDp) await RasporedHandler.PomeriDpSaNeradnihAsync(db, clock, req.Od, ct);
        return novi.Select(n => new NeradniDanDto(n.Id, n.Datum, n.Naziv, n.Vrsta)).ToList();
    }

    /// <inheritdoc />
    public async Task Handle(ObrisiNeradniDanCommand req, CancellationToken ct)
    {
        var n = await db.NeradniDani.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Neradni dan ne postoji.");
        db.NeradniDani.Remove(n);
        await db.SaveChangesAsync(ct);
    }
}

// ─── Odsustva ─────────────────────────────────────────────────────────────

/// <summary>Odsustvo ili zahtev.</summary>
/// <param name="Id">ID.</param>
/// <param name="KorisnikId">Inženjer.</param>
/// <param name="Ime">Ime.</param>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Napomena">Napomena.</param>
/// <param name="Status"><c>zahtev</c>, <c>odobreno</c>, <c>odbijeno</c>.</param>
/// <param name="RazlogOdbijanja">Razlog odbijanja.</param>
/// <param name="KreiranoAt">Kada je uneto.</param>
public sealed record OdsustvoDto(int Id, string KorisnikId, string Ime, DateOnly Od, DateOnly Do, string Vrsta, string? Napomena, string Status, string? RazlogOdbijanja, DateTime KreiranoAt);

/// <summary>Prebacivanje jednog pregleda na zamenu.</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="ZamenaId">Novi inženjer.</param>
public sealed record PrebacivanjeInput(int PregledId, string ZamenaId);

/// <summary>Planiran pregled u periodu odsustva sa predlogom zamene (R3.20).</summary>
/// <param name="Pregled">Pregled.</param>
/// <param name="PredlogZameneId">Predlog po rotaciji.</param>
/// <param name="PredlogZamene">Ime.</param>
public sealed record PregledZaPrebacivanjeDto(PregledKalendarDto Pregled, string? PredlogZameneId, string PredlogZamene);

/// <summary>Odsustva (svoja, ili sva uz pravo odobravanja).</summary>
/// <param name="KorisnikId">Filter po inženjeru.</param>
/// <param name="Status">Filter po statusu.</param>
public sealed record GetOdsustvaQuery(string? KorisnikId = null, string? Status = null) : IRequest<IReadOnlyList<OdsustvoDto>>;

/// <summary>Unos odsustva (Supervizor/Direktor; odmah odobreno) ili zahtev (Inženjer za sebe).</summary>
/// <param name="KorisnikId">Inženjer (za zahtev = prijavljeni).</param>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Napomena">Napomena.</param>
/// <param name="JeZahtev">true = zahtev inženjera.</param>
/// <param name="Prebacivanja">Prebacivanja planiranih pregleda (samo uz unos).</param>
public sealed record UnesiOdsustvoCommand(string KorisnikId, DateOnly Od, DateOnly Do, string Vrsta, string? Napomena, bool JeZahtev, IReadOnlyList<PrebacivanjeInput>? Prebacivanja) : IRequest<OdsustvoDto>;

/// <summary>Odluka o zahtevu za odsustvo (R3.19, R3.20).</summary>
/// <param name="Id">Zahtev.</param>
/// <param name="Odobreno">Odobri ili odbij.</param>
/// <param name="Razlog">Razlog odbijanja (opciono).</param>
/// <param name="Prebacivanja">„Odobri i prebaci“.</param>
public sealed record OdluciOOdsustvuCommand(int Id, bool Odobreno, string? Razlog, IReadOnlyList<PrebacivanjeInput>? Prebacivanja) : IRequest<OdsustvoDto>;

/// <summary>Izmena perioda odsustva (Supervizor/Direktor; npr. raniji povratak), odluka 2026-11-10.</summary>
/// <param name="Id">Odsustvo.</param>
/// <param name="Od">Novi početak.</param>
/// <param name="Do">Novi kraj.</param>
public sealed record IzmeniOdsustvoCommand(int Id, DateOnly Od, DateOnly Do) : IRequest<OdsustvoDto>;

/// <summary>Otkazivanje odsustva: Supervizor/Direktor bilo koje, inženjer samo svoj zahtev koji čeka odluku (odluka 2026-11-10).</summary>
/// <param name="Id">Odsustvo.</param>
/// <param name="MozeSva">Prijavljeni ima pravo odobravanja odsustva.</param>
public sealed record OtkaziOdsustvoCommand(int Id, bool MozeSva) : IRequest<OdsustvoDto>;

/// <summary>Oblik ulaza.</summary>
public sealed class IzmeniOdsustvoValidator : AbstractValidator<IzmeniOdsustvoCommand>
{
    /// <summary>Pravila.</summary>
    public IzmeniOdsustvoValidator() =>
        RuleFor(x => x).Must(x => x.Do >= x.Od).WithMessage("Datum „do“ ne može biti pre datuma „od“.");
}

/// <summary>Planirani pregledi inženjera u periodu, sa predlogom zamene.</summary>
/// <param name="KorisnikId">Inženjer.</param>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
public sealed record GetPreglediUOdsustvuQuery(string KorisnikId, DateOnly Od, DateOnly Do) : IRequest<IReadOnlyList<PregledZaPrebacivanjeDto>>;

/// <summary>Oblik ulaza.</summary>
public sealed class UnesiOdsustvoValidator : AbstractValidator<UnesiOdsustvoCommand>
{
    private static readonly string[] Vrste = ["godisnji", "bolovanje", "slobodan_dan", "sluzbeni_put", "ostalo"];

    /// <summary>Pravila.</summary>
    public UnesiOdsustvoValidator()
    {
        RuleFor(x => x.KorisnikId).NotEmpty().WithMessage("Inženjer je obavezan.");
        RuleFor(x => x.Vrsta).Must(v => Vrste.Contains(v)).WithMessage("Nepoznata vrsta odsustva.");
        RuleFor(x => x).Must(x => x.Do >= x.Od).WithMessage("Datum „do“ ne može biti pre datuma „od“.");
        RuleFor(x => x.Napomena).MaximumLength(500).WithMessage("Napomena može imati najviše 500 karaktera.");
    }
}

/// <summary>Odsustva i prebacivanje pregleda odsutnog inženjera.</summary>
public sealed class OdsustvaHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetOdsustvaQuery, IReadOnlyList<OdsustvoDto>>,
    IRequestHandler<UnesiOdsustvoCommand, OdsustvoDto>,
    IRequestHandler<OdluciOOdsustvuCommand, OdsustvoDto>,
    IRequestHandler<GetPreglediUOdsustvuQuery, IReadOnlyList<PregledZaPrebacivanjeDto>>,
    IRequestHandler<IzmeniOdsustvoCommand, OdsustvoDto>,
    IRequestHandler<OtkaziOdsustvoCommand, OdsustvoDto>
{
    /// <summary>Statusi koji ne zauzimaju period (ne preklapaju se, ne preskaču se u rasporedu).</summary>
    private static readonly string[] Neaktivna = ["odbijeno", "otkazano"];

    /// <summary>Poruka kad se preklapa sa postojećim odsustvom.</summary>
    public const string PorukaPreklapanje = "Za taj period već postoji odsustvo.";

    /// <summary>Poruka kad se automatski prebacuje započet pregled (R3.20).</summary>
    public const string PorukaZapocet = "Pregled je već započet; o prebacivanju odlučuje Supervizor ili Direktor.";

    /// <inheritdoc />
    public async Task<IReadOnlyList<OdsustvoDto>> Handle(GetOdsustvaQuery req, CancellationToken ct)
    {
        var upit = db.Odsustva.AsNoTracking().AsQueryable();
        if (req.KorisnikId is not null) upit = upit.Where(o => o.KorisnikId == req.KorisnikId);
        if (req.Status is not null) upit = upit.Where(o => o.Status == req.Status);
        var imena = await ImenaAsync(ct);
        return (await upit.OrderByDescending(o => o.Od).ToListAsync(ct)).Select(o => Dto(o, imena)).ToList();
    }

    /// <inheritdoc />
    public async Task<OdsustvoDto> Handle(UnesiOdsustvoCommand req, CancellationToken ct)
    {
        if (await db.Odsustva.AnyAsync(o => o.KorisnikId == req.KorisnikId && !Neaktivna.Contains(o.Status) && o.Od <= req.Do && o.Do >= req.Od, ct))
            throw new BusinessRuleException(PorukaPreklapanje);

        var sada = clock.UtcNow;
        var o = new Odsustvo
        {
            KorisnikId = req.KorisnikId, Od = req.Od, Do = req.Do, Vrsta = req.Vrsta,
            Napomena = string.IsNullOrWhiteSpace(req.Napomena) ? null : req.Napomena.Trim(),
            Status = req.JeZahtev ? "zahtev" : "odobreno", ZatrazioId = currentUser.UserId, KreiranoAt = sada,
            OdlucioId = req.JeZahtev ? null : currentUser.UserId, OdlucenoAt = req.JeZahtev ? null : sada,
        };
        db.Odsustva.Add(o);
        if (!req.JeZahtev) await PrebaciAsync(req.Prebacivanja, ct);
        await db.SaveChangesAsync(ct);
        return Dto(o, await ImenaAsync(ct));
    }

    /// <inheritdoc />
    public async Task<OdsustvoDto> Handle(OdluciOOdsustvuCommand req, CancellationToken ct)
    {
        var o = await db.Odsustva.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Zahtev za odsustvo ne postoji.");
        if (o.Status != "zahtev") throw new BusinessRuleException("O ovom zahtevu je već odlučeno.");
        o.Status = req.Odobreno ? "odobreno" : "odbijeno";
        o.RazlogOdbijanja = req.Odobreno || string.IsNullOrWhiteSpace(req.Razlog) ? null : req.Razlog.Trim();
        o.OdlucioId = currentUser.UserId;
        o.OdlucenoAt = clock.UtcNow;
        if (req.Odobreno) await PrebaciAsync(req.Prebacivanja, ct);
        await Obavesti.DodajAsync(db, [o.KorisnikId], VrsteObavestenja.IshodZahteva, req.Odobreno ? "Odsustvo odobreno" : "Odsustvo odbijeno",
            $"Zahtev za odsustvo {o.Od:dd.MM.yyyy}–{o.Do:dd.MM.yyyy} je {(req.Odobreno ? "odobren" : "odbijen")}{(o.RazlogOdbijanja is null ? "" : $": {o.RazlogOdbijanja}")}.",
            "/moja-odsustva", null, o.OdlucenoAt.Value, ct);
        await db.SaveChangesAsync(ct);
        return Dto(o, await ImenaAsync(ct));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PregledZaPrebacivanjeDto>> Handle(GetPreglediUOdsustvuQuery req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var pregledi = await db.Pregledi.AsNoTracking().Include(p => p.Ucesnici)
            .Where(p => p.DodeljenId == req.KorisnikId && p.PlaniraniDatum >= req.Od && p.PlaniraniDatum <= req.Do
                        && (p.Status == StatusPregleda.Planiran || p.Status == StatusPregleda.URadu))
            .OrderBy(p => p.PlaniraniDatum).ToListAsync(ct);

        return pregledi.Select(p =>
        {
            // Zamena po rotaciji: prvi dostupan posle odsutnog, ne on sam (R3.20).
            var predlog = RasporedPravila.SledeciInzenjer(k.Inzenjeri, req.KorisnikId,
                i => i != req.KorisnikId && !k.JeOdsutan(i, p.PlaniraniDatum));
            return new PregledZaPrebacivanjeDto(RasporedHandler.Dto(p, k), predlog, k.Ime(predlog));
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<OdsustvoDto> Handle(IzmeniOdsustvoCommand req, CancellationToken ct)
    {
        var o = await db.Odsustva.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Odsustvo ne postoji.");
        if (Neaktivna.Contains(o.Status)) throw new BusinessRuleException("Odbijeno ili otkazano odsustvo se ne menja.");
        if (await db.Odsustva.AnyAsync(x => x.Id != o.Id && x.KorisnikId == o.KorisnikId && !Neaktivna.Contains(x.Status) && x.Od <= req.Do && x.Do >= req.Od, ct))
            throw new BusinessRuleException(PorukaPreklapanje);
        var stari = $"{o.Od:dd.MM.yyyy}–{o.Do:dd.MM.yyyy}";
        (o.Od, o.Do) = (req.Od, req.Do);
        await Obavesti.DodajAsync(db, [o.KorisnikId], VrsteObavestenja.IshodZahteva, "Odsustvo izmenjeno",
            $"Odsustvo {stari} je sada {o.Od:dd.MM.yyyy}–{o.Do:dd.MM.yyyy}.", "/moja-odsustva", null, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return Dto(o, await ImenaAsync(ct));
    }

    /// <summary>
    /// Otkazano odsustvo ostaje zapisano (status „otkazano“) i više se ne računa u rasporedu. Pregledi prebačeni zbog njega
    /// ostaju zamenama — vraća ih Supervizor ili Direktor ručno, da se raspored ne menja bez njihovog znanja.
    /// </summary>
    public async Task<OdsustvoDto> Handle(OtkaziOdsustvoCommand req, CancellationToken ct)
    {
        var o = await db.Odsustva.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Odsustvo ne postoji.");
        if (!req.MozeSva && (o.KorisnikId != currentUser.UserId || o.Status != "zahtev"))
            throw new ForbiddenException("Možete da povučete samo svoj zahtev dok o njemu nije odlučeno.");
        if (Neaktivna.Contains(o.Status)) throw new BusinessRuleException("Odsustvo je već odbijeno ili otkazano.");
        o.Status = "otkazano";
        if (o.KorisnikId != currentUser.UserId)
            await Obavesti.DodajAsync(db, [o.KorisnikId], VrsteObavestenja.IshodZahteva, "Odsustvo otkazano",
                $"Odsustvo {o.Od:dd.MM.yyyy}–{o.Do:dd.MM.yyyy} je otkazano.", "/moja-odsustva", null, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return Dto(o, await ImenaAsync(ct));
    }

    /// <summary>Prebacuje preglede na zamene; započet pregled se ne prebacuje automatski (R3.20).</summary>
    private async Task PrebaciAsync(IReadOnlyList<PrebacivanjeInput>? prebacivanja, CancellationToken ct)
    {
        if (prebacivanja is not { Count: > 0 }) return;
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var ids = prebacivanja.Select(x => x.PregledId).ToList();
        var pregledi = await db.Pregledi.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var sada = clock.UtcNow;
        foreach (var x in prebacivanja)
        {
            var p = pregledi.FirstOrDefault(p => p.Id == x.PregledId) ?? throw new NotFoundException("Pregled ne postoji.");
            if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException(PorukaZapocet);
            k.ProveriInzenjera(x.ZamenaId);
            k.ProveriDostupnost(x.ZamenaId, p.PlaniraniDatum);
            PregledIstorija.Dodaj(db, p, "prebacivanje", new { razlog = "odsustvo", stari = k.Ime(p.DodeljenId), stariId = p.DodeljenId, novi = k.Ime(x.ZamenaId), noviId = x.ZamenaId }, currentUser.UserId, sada);
            await Obavesti.DodajAsync(db, [x.ZamenaId], VrsteObavestenja.Dodela, $"Dodeljen vam je {p.Broj}",
                $"{p.Broj} za {p.PlaniraniDatum:dd.MM.yyyy} je sada vaš (zamena za {k.Ime(p.DodeljenId)}).", "/kalendar", null, sada, ct);
            p.DodeljenId = x.ZamenaId;
        }
    }

    private async Task<Dictionary<string, string>> ImenaAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

    private static OdsustvoDto Dto(Odsustvo o, Dictionary<string, string> imena) =>
        new(o.Id, o.KorisnikId, imena.GetValueOrDefault(o.KorisnikId, string.Empty), o.Od, o.Do, o.Vrsta, o.Napomena, o.Status, o.RazlogOdbijanja, o.KreiranoAt);
}

// ─── Zahtevi za prebacivanje ──────────────────────────────────────────────

/// <summary>Zahtev za prebacivanje.</summary>
/// <param name="Id">ID.</param>
/// <param name="Pregled">Pregled.</param>
/// <param name="PodneoId">Ko traži.</param>
/// <param name="Podneo">Ime.</param>
/// <param name="PredlozenaZamenaId">Predlog.</param>
/// <param name="PredlozenaZamena">Ime predloga.</param>
/// <param name="Razlog">Razlog.</param>
/// <param name="Status">Status.</param>
/// <param name="Zamena">Ime izabrane zamene.</param>
/// <param name="KreiranoAt">Kada.</param>
public sealed record ZahtevPrebacivanjaDto(int Id, string Vrsta, PregledKalendarDto Pregled, string PodneoId, string Podneo, string? PredlozenaZamenaId, string PredlozenaZamena, string Razlog, string Status, string Zamena, DateTime KreiranoAt);

/// <summary>Zahtevi (svoji, ili svi uz pravo rasporeda).</summary>
/// <param name="Status">Filter po statusu.</param>
/// <param name="SamoMoji">Samo zahtevi prijavljenog.</param>
public sealed record GetZahteviPrebacivanjaQuery(string? Status, bool SamoMoji) : IRequest<IReadOnlyList<ZahtevPrebacivanjaDto>>;

/// <summary>Inženjer traži prebacivanje svog planiranog pregleda (R3.11, R3.12).</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="Razlog">Razlog (obavezan).</param>
/// <param name="PredlozenaZamenaId">Predlog zamene (opciono).</param>
public sealed record PodnesiZahtevPrebacivanjaCommand(int PregledId, string Razlog, string? PredlozenaZamenaId) : IRequest<ZahtevPrebacivanjaDto>;

/// <summary>Odluka Supervizora ili Direktora.</summary>
/// <param name="Id">Zahtev.</param>
/// <param name="Odobreno">Odobri ili odbij.</param>
/// <param name="ZamenaId">Izabrana zamena (obavezna uz odobrenje).</param>
public sealed record OdluciOPrebacivanjuCommand(int Id, bool Odobreno, string? ZamenaId) : IRequest<ZahtevPrebacivanjaDto>;

/// <summary>Inženjer otkazuje svoj zahtev za prebacivanje ili raniji početak dok čeka odluku (zamerka 47).</summary>
/// <param name="Id">Zahtev.</param>
public sealed record OtkaziZahtevPrebacivanjaCommand(int Id) : IRequest<ZahtevPrebacivanjaDto>;

/// <summary>Inženjer traži da započne PZ pre zakazanog dana (odluka 2026-11-10); DP se radi samo na svoj dan.</summary>
/// <param name="PregledId">PZ.</param>
/// <param name="Razlog">Razlog (obavezan).</param>
public sealed record PodnesiZahtevRanijegPocetkaCommand(int PregledId, string Razlog) : IRequest<ZahtevPrebacivanjaDto>;

/// <summary>Oblik ulaza.</summary>
public sealed class PodnesiZahtevRanijegPocetkaValidator : AbstractValidator<PodnesiZahtevRanijegPocetkaCommand>
{
    /// <summary>Pravila.</summary>
    public PodnesiZahtevRanijegPocetkaValidator() =>
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(500).WithMessage("Razlog može imati najviše 500 karaktera.");
}

/// <summary>Oblik ulaza.</summary>
public sealed class PodnesiZahtevPrebacivanjaValidator : AbstractValidator<PodnesiZahtevPrebacivanjaCommand>
{
    /// <summary>Pravila.</summary>
    public PodnesiZahtevPrebacivanjaValidator() =>
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(500).WithMessage("Razlog može imati najviše 500 karaktera.");
}

/// <summary>Zahtevi za prebacivanje.</summary>
public sealed class ZahteviPrebacivanjaHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetZahteviPrebacivanjaQuery, IReadOnlyList<ZahtevPrebacivanjaDto>>,
    IRequestHandler<PodnesiZahtevPrebacivanjaCommand, ZahtevPrebacivanjaDto>,
    IRequestHandler<OdluciOPrebacivanjuCommand, ZahtevPrebacivanjaDto>,
    IRequestHandler<PodnesiZahtevRanijegPocetkaCommand, ZahtevPrebacivanjaDto>,
    IRequestHandler<OtkaziZahtevPrebacivanjaCommand, ZahtevPrebacivanjaDto>
{
    /// <inheritdoc />
    public async Task<ZahtevPrebacivanjaDto> Handle(OtkaziZahtevPrebacivanjaCommand req, CancellationToken ct)
    {
        var z = await db.ZahteviPrebacivanja.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Zahtev ne postoji.");
        if (z.PodneoId != currentUser.UserId) throw new ForbiddenException("Zahtev otkazuje samo onaj ko ga je poslao.");
        if (z.Status != "zahtev") throw new BusinessRuleException("Otkazuje se samo zahtev koji čeka odluku.");
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstAsync(x => x.Id == z.PregledId, ct);
        (z.Status, z.OdlucenoAt) = ("otkazano", clock.UtcNow);
        PregledIstorija.Dodaj(db, p, "zahtev_otkazan", new { vrsta = z.Vrsta == "raniji_pocetak" ? "raniji početak" : "prebacivanje" }, currentUser.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Dto(z, p, await RasporedKontekst.UcitajAsync(db, ct));
    }

    /// <summary>Poruka: raniji početak se traži samo za PZ.</summary>
    public const string PorukaRanijiSamoPz = "Raniji početak se traži samo za PZ. DP se radi na svoj dan; ako tada niste tu, zatražite prebacivanje.";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ZahtevPrebacivanjaDto>> Handle(GetZahteviPrebacivanjaQuery req, CancellationToken ct)
    {
        var upit = db.ZahteviPrebacivanja.AsNoTracking().AsQueryable();
        if (req.Status is not null) upit = upit.Where(z => z.Status == req.Status);
        if (req.SamoMoji) upit = upit.Where(z => z.PodneoId == currentUser.UserId);
        var zahtevi = await upit.OrderByDescending(z => z.KreiranoAt).ToListAsync(ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var ids = zahtevi.Select(z => z.PregledId).ToList();
        var pregledi = await db.Pregledi.AsNoTracking().Include(p => p.Ucesnici).Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return zahtevi.Select(z => Dto(z, pregledi[z.PregledId], k)).ToList();
    }

    /// <inheritdoc />
    public async Task<ZahtevPrebacivanjaDto> Handle(PodnesiZahtevPrebacivanjaCommand req, CancellationToken ct)
    {
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == req.PregledId, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        if (p.DodeljenId != currentUser.UserId && p.Ucesnici.All(u => u.KorisnikId != currentUser.UserId))
            throw new ForbiddenException("Prebacivanje se traži samo za svoj pregled.");
        if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException(OdsustvaHandler.PorukaZapocet);
        if (await db.ZahteviPrebacivanja.AnyAsync(z => z.PregledId == p.Id && z.PodneoId == currentUser.UserId && z.Status == "zahtev", ct))
            throw new BusinessRuleException("Zahtev za ovaj pregled već čeka odluku.");

        var z = new ZahtevPrebacivanja
        {
            PregledId = p.Id, PodneoId = currentUser.UserId!, PredlozenaZamenaId = req.PredlozenaZamenaId,
            Razlog = req.Razlog.Trim(), KreiranoAt = clock.UtcNow,
        };
        db.ZahteviPrebacivanja.Add(z);
        await db.SaveChangesAsync(ct);
        return Dto(z, p, await RasporedKontekst.UcitajAsync(db, ct));
    }

    /// <inheritdoc />
    public async Task<ZahtevPrebacivanjaDto> Handle(PodnesiZahtevRanijegPocetkaCommand req, CancellationToken ct)
    {
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == req.PregledId, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        if (p.DodeljenId != currentUser.UserId && p.Ucesnici.All(u => u.KorisnikId != currentUser.UserId))
            throw new ForbiddenException("Raniji početak se traži samo za svoj pregled.");
        if (p.JeDp) throw new BusinessRuleException(PorukaRanijiSamoPz);
        if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException(OdsustvaHandler.PorukaZapocet);
        if (p.PlaniraniDatum <= clock.Today) throw new BusinessRuleException("Pregled već može da se započne.");
        if (await db.ZahteviPrebacivanja.AnyAsync(z => z.PregledId == p.Id && z.PodneoId == currentUser.UserId && z.Status == "zahtev", ct))
            throw new BusinessRuleException("Zahtev za ovaj pregled već čeka odluku.");

        var z = new ZahtevPrebacivanja
        {
            Vrsta = VrsteZahteva.RanijiPocetak, PregledId = p.Id, PodneoId = currentUser.UserId!, Razlog = req.Razlog.Trim(), KreiranoAt = clock.UtcNow,
        };
        db.ZahteviPrebacivanja.Add(z);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        // Odlučuju oni sa pravom rasporeda (Supervizor i Direktor); zahtev im stiže i u „Čeka moju odluku“.
        var saPravom = db.KorisnikPrava.Where(x => x.Pravo == Prava.RasporedUredjivanje).Select(x => x.KorisnikId);
        var odlucuju = await db.Users.Where(u => u.IsActive && saPravom.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        await Obavesti.DodajAsync(db, odlucuju, VrsteObavestenja.IshodZahteva, $"Zahtev za raniji početak {p.Broj}",
            $"{k.Ime(currentUser.UserId)} traži da {p.Broj} ({p.PlaniraniDatum:dd.MM.yyyy}) započne danas: {z.Razlog}", "/raspored", null, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return Dto(z, p, k);
    }

    /// <inheritdoc />
    public async Task<ZahtevPrebacivanjaDto> Handle(OdluciOPrebacivanjuCommand req, CancellationToken ct)
    {
        var z = await db.ZahteviPrebacivanja.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Zahtev ne postoji.");
        if (z.Status != "zahtev") throw new BusinessRuleException("O ovom zahtevu je već odlučeno.");
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstAsync(x => x.Id == z.PregledId, ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var sada = clock.UtcNow;

        if (z.Vrsta == VrsteZahteva.RanijiPocetak)
            return await OdluciORanijemPocetkuAsync(z, p, k, req.Odobreno, sada, ct);

        if (req.Odobreno)
        {
            if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException(OdsustvaHandler.PorukaZapocet);
            k.ProveriInzenjera(req.ZamenaId);
            k.ProveriDostupnost(req.ZamenaId, p.PlaniraniDatum);
            // Kod PZ-a sa dva inženjera prebacuje se samo deo onoga ko traži (R3.12).
            var ucesnik = p.Ucesnici.FirstOrDefault(u => u.KorisnikId == z.PodneoId);
            if (ucesnik is not null)
            {
                p.Ucesnici.Remove(ucesnik);
                p.Ucesnici.Add(new PregledUcesnik { KorisnikId = req.ZamenaId!, Sekcije = ucesnik.Sekcije });
            }
            else p.DodeljenId = req.ZamenaId!;
            PregledIstorija.Dodaj(db, p, "prebacivanje", new { razlog = z.Razlog, stari = k.Ime(z.PodneoId), stariId = z.PodneoId, novi = k.Ime(req.ZamenaId), noviId = req.ZamenaId }, currentUser.UserId, sada);
            z.ZamenaId = req.ZamenaId;
        }
        z.Status = req.Odobreno ? "odobreno" : "odbijeno";
        z.OdlucioId = currentUser.UserId;
        z.OdlucenoAt = sada;
        await Obavesti.DodajAsync(db, [z.PodneoId], VrsteObavestenja.IshodZahteva, req.Odobreno ? "Prebacivanje odobreno" : "Prebacivanje odbijeno",
            req.Odobreno ? $"{p.Broj} ({p.PlaniraniDatum:dd.MM.yyyy}) je prebačen na {k.Ime(req.ZamenaId)}." : $"Zahtev za prebacivanje {p.Broj} je odbijen.", "/kalendar", null, sada, ct);
        if (req.Odobreno)
            await Obavesti.DodajAsync(db, [req.ZamenaId], VrsteObavestenja.Dodela, $"Dodeljen vam je {p.Broj}", $"{p.Broj} za {p.PlaniraniDatum:dd.MM.yyyy} je sada vaš.", "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return Dto(z, p, k);
    }

    /// <summary>
    /// Odobren raniji početak pomera PZ na danas — i planirani i prvobitni dan, jer pregled nije propušten nego urađen ranije
    /// (kalendar ne sme da pokaže „nije urađen – prenet“, a analitika ga broji na dan kad je urađen).
    /// </summary>
    private async Task<ZahtevPrebacivanjaDto> OdluciORanijemPocetkuAsync(ZahtevPrebacivanja z, Pregled p, RasporedKontekst k, bool odobreno, DateTime sada, CancellationToken ct)
    {
        var danas = clock.Today;
        var bio = p.PlaniraniDatum;
        if (odobreno)
        {
            if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException(OdsustvaHandler.PorukaZapocet);
            if (p.PlaniraniDatum > danas)
            {
                PregledIstorija.Dodaj(db, p, "raniji_pocetak", new { razlog = z.Razlog, stari = p.PlaniraniDatum, novi = danas }, currentUser.UserId, sada);
                p.PlaniraniDatum = danas;
                p.PrvobitniDatum = danas;
            }
        }
        z.Status = odobreno ? "odobreno" : "odbijeno";
        z.OdlucioId = currentUser.UserId;
        z.OdlucenoAt = sada;
        await Obavesti.DodajAsync(db, [z.PodneoId], VrsteObavestenja.IshodZahteva, odobreno ? "Raniji početak odobren" : "Raniji početak odbijen",
            odobreno ? $"{p.Broj} možete da započnete danas." : $"Zahtev za raniji početak {p.Broj} je odbijen.",
            odobreno ? $"/pregled/{p.Id}/popuni" : "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);
        // PZ je sada danas: nezapočet DP danas i naredni idu dan napred, kao kod zakazivanja PZ-a (R3.9, zamerka 43).
        if (odobreno) await RasporedHandler.PremestiDpZbogPzAsync(db, clock, bio, danas, ct);
        return Dto(z, p, k);
    }

    private static ZahtevPrebacivanjaDto Dto(ZahtevPrebacivanja z, Pregled p, RasporedKontekst k) =>
        new(z.Id, z.Vrsta, RasporedHandler.Dto(p, k), z.PodneoId, k.Ime(z.PodneoId), z.PredlozenaZamenaId, k.Ime(z.PredlozenaZamenaId),
            z.Razlog, z.Status, k.Ime(z.ZamenaId), z.KreiranoAt);
}
