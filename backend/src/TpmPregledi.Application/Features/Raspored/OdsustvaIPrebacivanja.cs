using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
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
public sealed record UnesiNeradneDaneCommand(DateOnly Od, DateOnly Do, string Naziv, string Vrsta) : IRequest<IReadOnlyList<NeradniDanDto>>;

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
public sealed class NeradniDaniHandler(IAppDbContext db, ICurrentUser currentUser) :
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
    IRequestHandler<GetPreglediUOdsustvuQuery, IReadOnlyList<PregledZaPrebacivanjeDto>>
{
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
        if (await db.Odsustva.AnyAsync(o => o.KorisnikId == req.KorisnikId && o.Status != "odbijeno" && o.Od <= req.Do && o.Do >= req.Od, ct))
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
            PregledIstorija.Dodaj(db, p, "prebacivanje", new { razlog = "odsustvo", stari = k.Ime(p.DodeljenId), novi = k.Ime(x.ZamenaId) }, currentUser.UserId, sada);
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
public sealed record ZahtevPrebacivanjaDto(int Id, PregledKalendarDto Pregled, string PodneoId, string Podneo, string? PredlozenaZamenaId, string PredlozenaZamena, string Razlog, string Status, string Zamena, DateTime KreiranoAt);

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
    IRequestHandler<OdluciOPrebacivanjuCommand, ZahtevPrebacivanjaDto>
{
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
    public async Task<ZahtevPrebacivanjaDto> Handle(OdluciOPrebacivanjuCommand req, CancellationToken ct)
    {
        var z = await db.ZahteviPrebacivanja.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Zahtev ne postoji.");
        if (z.Status != "zahtev") throw new BusinessRuleException("O ovom zahtevu je već odlučeno.");
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstAsync(x => x.Id == z.PregledId, ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var sada = clock.UtcNow;

        if (req.Odobreno)
        {
            if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException(OdsustvaHandler.PorukaZapocet);
            k.ProveriInzenjera(req.ZamenaId);
            // Kod PZ-a sa dva inženjera prebacuje se samo deo onoga ko traži (R3.12).
            var ucesnik = p.Ucesnici.FirstOrDefault(u => u.KorisnikId == z.PodneoId);
            if (ucesnik is not null)
            {
                p.Ucesnici.Remove(ucesnik);
                p.Ucesnici.Add(new PregledUcesnik { KorisnikId = req.ZamenaId!, Sekcije = ucesnik.Sekcije });
            }
            else p.DodeljenId = req.ZamenaId!;
            PregledIstorija.Dodaj(db, p, "prebacivanje", new { razlog = z.Razlog, stari = k.Ime(z.PodneoId), novi = k.Ime(req.ZamenaId) }, currentUser.UserId, sada);
            z.ZamenaId = req.ZamenaId;
        }
        z.Status = req.Odobreno ? "odobreno" : "odbijeno";
        z.OdlucioId = currentUser.UserId;
        z.OdlucenoAt = sada;
        await db.SaveChangesAsync(ct);
        return Dto(z, p, k);
    }

    private static ZahtevPrebacivanjaDto Dto(ZahtevPrebacivanja z, Pregled p, RasporedKontekst k) =>
        new(z.Id, RasporedHandler.Dto(p, k), z.PodneoId, k.Ime(z.PodneoId), z.PredlozenaZamenaId, k.Ime(z.PredlozenaZamenaId),
            z.Razlog, z.Status, k.Ime(z.ZamenaId), z.KreiranoAt);
}
