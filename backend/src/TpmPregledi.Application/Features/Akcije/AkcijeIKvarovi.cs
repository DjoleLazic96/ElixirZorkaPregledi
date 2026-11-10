using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Akcije;

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Akcija u listi i detalju (R7.3, R7.6).</summary>
public sealed record AkcijaDto(
    int Id, string Broj, int PregledId, string Pregled, int UredjajId, string Uredjaj, string Sekcija, string Aktivnost,
    string Vrsta, string Hitnost, string? Opis, DateOnly? Rok, bool RokIstekao, string Status, string? Zaduzen, string Prijavio,
    DateTime KreiranoAt, string? Razlog, bool StavkaSadaIspravna, string? KvarBroj, bool KvarOtvoren);

/// <summary>Događaj akcije.</summary>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Tekst">Tekst.</param>
/// <param name="Korisnik">Ko.</param>
/// <param name="At">Kada.</param>
public sealed record AkcijaDogadjajDto(string Vrsta, string Tekst, string Korisnik, DateTime At);

/// <summary>Akcija sa istorijom.</summary>
/// <param name="Akcija">Podaci.</param>
/// <param name="Istorija">Istorija.</param>
public sealed record AkcijaDetaljDto(AkcijaDto Akcija, IReadOnlyList<AkcijaDogadjajDto> Istorija);

/// <summary>Kvar u listi i detalju (R8.1, R8.16).</summary>
public sealed record KvarDto(
    int Id, string Broj, int UredjajId, string Uredjaj, string Sekcija, string Aktivnost, DateOnly Otvoren, DateOnly? Zatvoren,
    int TrajanjeDana, int Prijava, string? PoslednjaNapomena, int OtvorioPregledId, string OtvorioPregled, int OtvorenihAkcija);

/// <summary>Prijava kvara (pregled koji je stavku označio neispravnom).</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="Broj">Broj pregleda.</param>
/// <param name="Datum">Dan.</param>
/// <param name="Napomena">Napomena.</param>
public sealed record PrijavaKvaraDto(int PregledId, string Broj, DateOnly Datum, string? Napomena);

/// <summary>Kvar sa prijavama i akcijama na stavci.</summary>
/// <param name="Kvar">Podaci.</param>
/// <param name="Prijave">Svi datumi prijave.</param>
/// <param name="Akcije">Akcije na toj stavci.</param>
public sealed record KvarDetaljDto(KvarDto Kvar, IReadOnlyList<PrijavaKvaraDto> Prijave, IReadOnlyList<AkcijaDto> Akcije);

// ─── Zahtevi ──────────────────────────────────────────────────────────────

/// <summary>Filteri liste akcija.</summary>
public sealed record AkcijeFilteri(string? Status = "otvorena", string? Hitnost = null, int? SekcijaId = null, int? UredjajId = null, bool? SadaIspravna = null);

/// <summary>Lista akcija.</summary>
/// <param name="F">Filteri.</param>
public sealed record GetAkcijeQuery(AkcijeFilteri F) : IRequest<IReadOnlyList<AkcijaDto>>;

/// <summary>Akcija sa istorijom.</summary>
/// <param name="Id">ID.</param>
public sealed record GetAkcijaQuery(int Id) : IRequest<AkcijaDetaljDto>;

/// <summary>Zatvaranje ili otkazivanje sa ekrana (R7.5).</summary>
/// <param name="Id">ID.</param>
/// <param name="Otkazi">true = otkazana, false = zatvorena.</param>
/// <param name="Razlog">Razlog (obavezan).</param>
public sealed record ZatvoriAkcijuCommand(int Id, bool Otkazi, string Razlog) : IRequest<AkcijaDetaljDto>;

/// <summary>Zaduženi (R7.12).</summary>
/// <param name="Id">ID.</param>
/// <param name="Zaduzen">Osoba ili služba; prazno = bez zaduženog.</param>
public sealed record PostaviZaduzenogCommand(int Id, string? Zaduzen) : IRequest<AkcijaDetaljDto>;

/// <summary>Filteri liste kvarova.</summary>
public sealed record KvaroviFilteri(bool? Otvoreno = true, int? SekcijaId = null, int? UredjajId = null, string? Pretraga = null);

/// <summary>Lista kvarova.</summary>
/// <param name="F">Filteri.</param>
public sealed record GetKvaroviQuery(KvaroviFilteri F) : IRequest<IReadOnlyList<KvarDto>>;

/// <summary>Kvar sa prijavama i akcijama.</summary>
/// <param name="Id">ID.</param>
public sealed record GetKvarQuery(int Id) : IRequest<KvarDetaljDto>;

/// <summary>Razlog je obavezan.</summary>
public sealed class ZatvoriAkcijuValidator : AbstractValidator<ZatvoriAkcijuCommand>
{
    /// <summary>Pravila.</summary>
    public ZatvoriAkcijuValidator() =>
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(500).WithMessage("Razlog može imati najviše 500 karaktera.");
}

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Akcije i kvarovi.</summary>
public sealed class AkcijeIKvaroviHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetAkcijeQuery, IReadOnlyList<AkcijaDto>>,
    IRequestHandler<GetAkcijaQuery, AkcijaDetaljDto>,
    IRequestHandler<ZatvoriAkcijuCommand, AkcijaDetaljDto>,
    IRequestHandler<PostaviZaduzenogCommand, AkcijaDetaljDto>,
    IRequestHandler<GetKvaroviQuery, IReadOnlyList<KvarDto>>,
    IRequestHandler<GetKvarQuery, KvarDetaljDto>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AkcijaDto>> Handle(GetAkcijeQuery req, CancellationToken ct)
    {
        var f = req.F;
        var upit = db.Akcije.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Status)) upit = upit.Where(a => a.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Hitnost)) upit = upit.Where(a => a.Hitnost == f.Hitnost);
        if (f.UredjajId is { } u) upit = upit.Where(a => a.UredjajId == u);
        if (f.SekcijaId is { } s) upit = upit.Where(a => db.Uredjaji.Any(x => x.Id == a.UredjajId && x.SekcijaId == s));
        if (f.SadaIspravna is { } si) upit = upit.Where(a => a.StavkaSadaIspravna == si);
        return await DtoAsync(await upit.ToListAsync(ct), ct);
    }

    /// <inheritdoc />
    public async Task<AkcijaDetaljDto> Handle(GetAkcijaQuery req, CancellationToken ct)
    {
        var a = await db.Akcije.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Akcija ne postoji.");
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var ist = await db.AkcijaDogadjaji.AsNoTracking().Where(d => d.AkcijaId == a.Id).OrderBy(d => d.At).ToListAsync(ct);
        return new AkcijaDetaljDto((await DtoAsync([a], ct)).Single(), ist.Select(d => new AkcijaDogadjajDto(d.Vrsta, d.Tekst, k.Ime(d.KorisnikId), d.At)).ToList());
    }

    /// <inheritdoc />
    public async Task<AkcijaDetaljDto> Handle(ZatvoriAkcijuCommand req, CancellationToken ct)
    {
        var a = await db.Akcije.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Akcija ne postoji.");
        if (a.Status != "otvorena") throw new BusinessRuleException("Akcija je već zatvorena ili otkazana.");
        a.Status = req.Otkazi ? "otkazana" : "zatvorena";
        a.ZatvorioId = currentUser.UserId;
        a.ZatvorenoAt = clock.UtcNow;
        a.Razlog = req.Razlog.Trim();
        db.AkcijaDogadjaji.Add(new AkcijaDogadjaj { AkcijaId = a.Id, Vrsta = a.Status, Tekst = $"{(req.Otkazi ? "Otkazana" : "Zatvorena")}: {a.Razlog}", KorisnikId = currentUser.UserId, At = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        return await Handle(new GetAkcijaQuery(a.Id), ct);
    }

    /// <inheritdoc />
    public async Task<AkcijaDetaljDto> Handle(PostaviZaduzenogCommand req, CancellationToken ct)
    {
        var a = await db.Akcije.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Akcija ne postoji.");
        a.Zaduzen = string.IsNullOrWhiteSpace(req.Zaduzen) ? null : req.Zaduzen.Trim();
        db.AkcijaDogadjaji.Add(new AkcijaDogadjaj { AkcijaId = a.Id, Vrsta = "zaduzen", Tekst = a.Zaduzen is null ? "Uklonjen zaduženi" : $"Zadužen: {a.Zaduzen}", KorisnikId = currentUser.UserId, At = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        return await Handle(new GetAkcijaQuery(a.Id), ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KvarDto>> Handle(GetKvaroviQuery req, CancellationToken ct)
    {
        var f = req.F;
        var upit = db.Kvarovi.AsNoTracking().AsQueryable();
        if (f.Otvoreno is true) upit = upit.Where(x => x.Zatvoren == null);
        if (f.Otvoreno is false) upit = upit.Where(x => x.Zatvoren != null);
        if (f.UredjajId is { } u) upit = upit.Where(x => x.UredjajId == u);
        if (f.SekcijaId is { } s) upit = upit.Where(x => db.Uredjaji.Any(d => d.Id == x.UredjajId && d.SekcijaId == s));
        if (!string.IsNullOrWhiteSpace(f.Pretraga))
        {
            var t = f.Pretraga.Trim().ToLower();
            upit = upit.Where(x => x.Broj.ToLower().Contains(t) || db.Uredjaji.Any(d => d.Id == x.UredjajId && (d.Naziv.ToLower().Contains(t) || d.Oznaka.ToLower().Contains(t))));
        }
        return await KvarDtoAsync(await upit.OrderByDescending(x => x.Otvoren).ThenByDescending(x => x.Id).ToListAsync(ct), ct);
    }

    /// <inheritdoc />
    public async Task<KvarDetaljDto> Handle(GetKvarQuery req, CancellationToken ct)
    {
        var kv = await db.Kvarovi.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Kvar ne postoji.");
        var doDana = kv.Zatvoren ?? DateOnly.MaxValue;
        var prijave = await (
                from o in db.Odgovori.AsNoTracking()
                join p in db.Pregledi.AsNoTracking() on o.PregledId equals p.Id
                join s in db.SablonStavke.AsNoTracking() on o.StavkaId equals s.Id
                where o.Vrednost == 1 && o.Dorada == p.Dorada && p.PredatAt != null && s.UredjajId == kv.UredjajId && s.AktivnostId == kv.AktivnostId
                      && p.PlaniraniDatum >= kv.Otvoren && p.PlaniraniDatum <= doDana
                orderby p.PlaniraniDatum
                select new PrijavaKvaraDto(p.Id, p.Broj, p.PlaniraniDatum, o.Napomena))
            .ToListAsync(ct);
        var akcije = await db.Akcije.AsNoTracking().Where(a => a.UredjajId == kv.UredjajId && a.AktivnostId == kv.AktivnostId).ToListAsync(ct);
        return new KvarDetaljDto((await KvarDtoAsync([kv], ct)).Single(), prijave, await DtoAsync(akcije, ct));
    }

    // ─── Pomoćno ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, int> RedHitnosti = new() { ["hitno"] = 0, ["7_dana"] = 1, ["sledeci_pz"] = 2, ["pratiti"] = 3 };

    private async Task<IReadOnlyList<AkcijaDto>> DtoAsync(List<Akcija> akcije, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var uIds = akcije.Select(a => a.UredjajId).Distinct().ToList();
        var uredjaji = await db.Uredjaji.AsNoTracking().Include(u => u.Sekcija).Where(u => uIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var aIds = akcije.Select(a => a.AktivnostId).Distinct().ToList();
        var aktivnosti = await db.Aktivnosti.AsNoTracking().Where(x => aIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Naziv, ct);
        var pIds = akcije.Select(a => a.PregledId).Distinct().ToList();
        var pregledi = await db.Pregledi.AsNoTracking().Where(p => pIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Broj, ct);
        var kvarovi = await db.Kvarovi.AsNoTracking().Where(x => uIds.Contains(x.UredjajId)).ToListAsync(ct);
        var danas = clock.Today;

        return akcije
            .OrderBy(a => a.Status == "otvorena" ? 0 : 1).ThenBy(a => RedHitnosti.GetValueOrDefault(a.Hitnost, 9)).ThenBy(a => a.Rok ?? DateOnly.MaxValue)
            .Select(a =>
            {
                var u = uredjaji[a.UredjajId];
                var kvar = kvarovi.Where(x => x.UredjajId == a.UredjajId && x.AktivnostId == a.AktivnostId).OrderByDescending(x => x.Otvoren).FirstOrDefault();
                return new AkcijaDto(a.Id, a.Broj, a.PregledId, pregledi.GetValueOrDefault(a.PregledId, ""), a.UredjajId, u.Prikaz, u.Sekcija?.Sifra ?? "",
                    aktivnosti.GetValueOrDefault(a.AktivnostId, ""), a.Vrsta, a.Hitnost, a.Opis, a.Rok, a.Status == "otvorena" && a.Rok < danas, a.Status,
                    a.Zaduzen, k.Ime(a.KreiraoId), a.KreiranoAt, a.Razlog, a.StavkaSadaIspravna, kvar?.Broj, kvar is { Zatvoren: null });
            }).ToList();
    }

    private async Task<IReadOnlyList<KvarDto>> KvarDtoAsync(List<Kvar> kvarovi, CancellationToken ct)
    {
        var uIds = kvarovi.Select(x => x.UredjajId).Distinct().ToList();
        var uredjaji = await db.Uredjaji.AsNoTracking().Include(u => u.Sekcija).Where(u => uIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var aIds = kvarovi.Select(x => x.AktivnostId).Distinct().ToList();
        var aktivnosti = await db.Aktivnosti.AsNoTracking().Where(x => aIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Naziv, ct);
        var pIds = kvarovi.Select(x => x.OtvorioPregledId).Distinct().ToList();
        var pregledi = await db.Pregledi.AsNoTracking().Where(p => pIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Broj, ct);
        var otvoreneAkcije = await db.Akcije.AsNoTracking().Where(a => a.Status == "otvorena" && uIds.Contains(a.UredjajId))
            .Select(a => new { a.UredjajId, a.AktivnostId }).ToListAsync(ct);
        var danas = clock.Today;

        return kvarovi.Select(x =>
        {
            var u = uredjaji[x.UredjajId];
            return new KvarDto(x.Id, x.Broj, x.UredjajId, u.Prikaz, u.Sekcija?.Sifra ?? "", aktivnosti.GetValueOrDefault(x.AktivnostId, ""),
                x.Otvoren, x.Zatvoren, Math.Max(0, (x.Zatvoren ?? danas).DayNumber - x.Otvoren.DayNumber), x.Prijava, x.PoslednjaNapomena, x.OtvorioPregledId,
                pregledi.GetValueOrDefault(x.OtvorioPregledId, ""), otvoreneAkcije.Count(a => a.UredjajId == x.UredjajId && a.AktivnostId == x.AktivnostId));
        }).ToList();
    }
}
