using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Akcije;

/// <summary>Inženjer traži zatvaranje akcije van pregleda (zamerka 49); šalje se i bez interneta, pa je idempotentan po <c>KlijentId</c>.</summary>
/// <param name="AkcijaId">Akcija.</param>
/// <param name="KlijentId">ID sa telefona.</param>
/// <param name="Uradjeno">Šta je urađeno.</param>
/// <param name="Datum">Kada.</param>
/// <param name="Izvrsilac">Ko je uradio.</param>
/// <param name="Slika">Slika (opciono).</param>
/// <param name="Mime">MIME tip slike.</param>
public sealed record PodnesiZahtevZatvaranjaCommand(int AkcijaId, Guid KlijentId, string Uradjeno, DateOnly Datum, string Izvrsilac, byte[]? Slika = null, string? Mime = null)
    : IRequest<AkcijaDetaljDto>;

/// <summary>Odluka o zahtevu za zatvaranje (Supervizor ili Direktor).</summary>
/// <param name="Id">Zahtev.</param>
/// <param name="Odobreno">Odobri (zatvara akciju) ili odbij.</param>
/// <param name="Razlog">Razlog odbijanja (obavezan kad se odbija).</param>
public sealed record OdluciOZahtevuZatvaranjaCommand(int Id, bool Odobreno, string? Razlog) : IRequest<AkcijaDetaljDto>;

/// <summary>Podnosilac otkazuje svoj zahtev dok čeka odluku (zamerka 47).</summary>
/// <param name="Id">Zahtev.</param>
public sealed record OtkaziZahtevZatvaranjaCommand(int Id) : IRequest<AkcijaDetaljDto>;

/// <summary>Slika uz zatvaranje akcije ili uz zahtev za zatvaranje.</summary>
/// <param name="AkcijaId">Akcija.</param>
/// <param name="ZahtevId">Zahtev (null = slika zatvorene akcije).</param>
public sealed record GetSlikaZatvaranjaQuery(int AkcijaId, int? ZahtevId) : IRequest<(Stream Sadrzaj, string Mime)>;

/// <summary>Oblik zahteva.</summary>
public sealed class PodnesiZahtevZatvaranjaValidator : AbstractValidator<PodnesiZahtevZatvaranjaCommand>
{
    /// <summary>Pravila.</summary>
    public PodnesiZahtevZatvaranjaValidator()
    {
        RuleFor(x => x.Uradjeno).NotEmpty().WithMessage("Upišite šta je urađeno.").MaximumLength(1000).WithMessage("Najviše 1000 karaktera.");
        RuleFor(x => x.Izvrsilac).NotEmpty().WithMessage("Izaberite ko je uradio.").MaximumLength(200).WithMessage("Najviše 200 karaktera.");
    }
}

/// <summary>Odbijanje traži razlog.</summary>
public sealed class OdluciOZahtevuZatvaranjaValidator : AbstractValidator<OdluciOZahtevuZatvaranjaCommand>
{
    /// <summary>Pravila.</summary>
    public OdluciOZahtevuZatvaranjaValidator() =>
        When(x => !x.Odobreno, () => RuleFor(x => x.Razlog).NotEmpty().WithMessage("Upišite razlog odbijanja.").MaximumLength(500).WithMessage("Najviše 500 karaktera."));
}

/// <summary>Zahtevi za zatvaranje akcije (zamerka 49).</summary>
public sealed class ZatvaranjeAkcijeHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, IFajlSkladiste? skladiste = null) :
    IRequestHandler<PodnesiZahtevZatvaranjaCommand, AkcijaDetaljDto>,
    IRequestHandler<OdluciOZahtevuZatvaranjaCommand, AkcijaDetaljDto>,
    IRequestHandler<OtkaziZahtevZatvaranjaCommand, AkcijaDetaljDto>,
    IRequestHandler<GetSlikaZatvaranjaQuery, (Stream Sadrzaj, string Mime)>
{
    private Task<AkcijaDetaljDto> Detalj(int id, CancellationToken ct) => new AkcijeIKvaroviHandler(db, currentUser, clock).Handle(new GetAkcijaQuery(id), ct);

    /// <summary>Poruka kad je datum u budućnosti.</summary>
    public const string PorukaBuduciDan = "Datum ne može biti u budućnosti.";

    /// <summary>Tekst u istoriji akcije: „Zatvorena: šta (datum, uradio X)“.</summary>
    public static string TekstZatvaranja(Akcija a) => $"Zatvorena: {a.Uradjeno} ({a.UradjenoDatum:dd.MM.yyyy}, uradio {a.Izvrsilac})";

    /// <summary>Akcija zatvorena ili otkazana drugim putem: zahtev koji je čekao odluku se zatvara, uz obaveštenje podnosiocu.</summary>
    public static async Task ZatvoriZahteveAsync(IAppDbContext db, Akcija a, string? ko, DateTime sada, CancellationToken ct)
    {
        foreach (var z in await db.ZahteviZatvaranja.Where(x => x.AkcijaId == a.Id && x.Status == "zahtev").ToListAsync(ct))
        {
            (z.Status, z.OdlucioId, z.OdlucenoAt, z.Razlog) = ("odbijeno", ko, sada, $"Akcija je {(a.Status == "otkazana" ? "otkazana" : "zatvorena")} drugim putem.");
            await Obavesti.DodajAsync(db, [z.PodneoId], VrsteObavestenja.IshodZahteva, $"Zahtev za {a.Broj} je zatvoren",
                $"{a.Broj} je {(a.Status == "otkazana" ? "otkazana" : "zatvorena")} drugim putem, pa je vaš zahtev zatvoren.", $"/akcije/{a.Id}", null, sada, ct);
        }
    }

    /// <inheritdoc />
    public async Task<AkcijaDetaljDto> Handle(PodnesiZahtevZatvaranjaCommand req, CancellationToken ct)
    {
        var postojeci = await db.ZahteviZatvaranja.AsNoTracking().FirstOrDefaultAsync(z => z.KlijentId == req.KlijentId, ct);
        if (postojeci is not null) return await Detalj(postojeci.AkcijaId, ct);   // ponovljeno slanje

        var a = await db.Akcije.FirstOrDefaultAsync(x => x.Id == req.AkcijaId, ct) ?? throw new NotFoundException("Akcija ne postoji.");
        if (a.Status != "otvorena") throw new BusinessRuleException("Akcija je već zatvorena ili otkazana.");
        if (await db.ZahteviZatvaranja.AnyAsync(z => z.AkcijaId == a.Id && z.Status == "zahtev", ct))
            throw new BusinessRuleException("Zahtev za zatvaranje ove akcije već čeka odluku.");
        if (req.Datum > clock.Today) throw new BusinessRuleException(PorukaBuduciDan);

        var sada = clock.UtcNow;
        var z = new ZahtevZatvaranjaAkcije
        {
            AkcijaId = a.Id, KlijentId = req.KlijentId, PodneoId = currentUser.UserId!, Uradjeno = req.Uradjeno.Trim(), Datum = req.Datum,
            Izvrsilac = req.Izvrsilac.Trim(), KreiranoAt = sada,
        };
        if (req.Slika is { Length: > 0 } slika)
        {
            if (!FotografijeHandler.JeSlika(slika) || skladiste is null) throw new BusinessRuleException("Fajl nije slika (dozvoljeni su JPG, PNG i WebP).");
            var fajl = await skladiste.SacuvajAsync(slika, req.Mime ?? "image/jpeg", ct);
            db.Fajlovi.Add(fajl);
            await db.SaveChangesAsync(ct);
            z.FajlId = fajl.Id;
        }
        db.ZahteviZatvaranja.Add(z);
        db.AkcijaDogadjaji.Add(new AkcijaDogadjaj { Akcija = a, Vrsta = "zahtev_zatvaranja", Tekst = $"Zatraženo zatvaranje: {z.Uradjeno} ({z.Datum:dd.MM.yyyy}, uradio {z.Izvrsilac})", KorisnikId = currentUser.UserId, At = sada });

        // Odlučuju oni sa pravom zatvaranja akcija (Supervizor i Direktor).
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var saPravom = db.KorisnikPrava.Where(x => x.Pravo == Prava.AkcijeZatvaranje).Select(x => x.KorisnikId);
        var odlucuju = await db.Users.Where(u => u.IsActive && saPravom.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        await Obavesti.DodajAsync(db, odlucuju, VrsteObavestenja.IshodZahteva, $"Zahtev za zatvaranje {a.Broj}",
            $"{k.Ime(currentUser.UserId)}: {z.Uradjeno} ({z.Datum:dd.MM.yyyy}, uradio {z.Izvrsilac}).", $"/akcije/{a.Id}", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return await Detalj(a.Id, ct);
    }

    /// <inheritdoc />
    public async Task<AkcijaDetaljDto> Handle(OdluciOZahtevuZatvaranjaCommand req, CancellationToken ct)
    {
        var z = await db.ZahteviZatvaranja.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Zahtev ne postoji.");
        if (z.Status != "zahtev") throw new BusinessRuleException("O ovom zahtevu je već odlučeno.");
        var a = await db.Akcije.FirstAsync(x => x.Id == z.AkcijaId, ct);
        var sada = clock.UtcNow;
        (z.Status, z.OdlucioId, z.OdlucenoAt) = (req.Odobreno ? "odobreno" : "odbijeno", currentUser.UserId, sada);
        if (req.Odobreno)
        {
            if (a.Status != "otvorena") throw new BusinessRuleException("Akcija je već zatvorena ili otkazana.");
            (a.Status, a.ZatvorioId, a.ZatvorenoAt) = ("zatvorena", currentUser.UserId, sada);
            (a.Uradjeno, a.UradjenoDatum, a.Izvrsilac, a.ZatvaranjeFajlId) = (z.Uradjeno, z.Datum, z.Izvrsilac, z.FajlId);
            db.AkcijaDogadjaji.Add(new AkcijaDogadjaj { AkcijaId = a.Id, Vrsta = "zatvorena", Tekst = $"{TekstZatvaranja(a)} — zahtev odobren", KorisnikId = currentUser.UserId, At = sada });
            await Obavesti.DodajAsync(db, [z.PodneoId], VrsteObavestenja.IshodZahteva, $"{a.Broj} je zatvorena", $"Vaš zahtev za zatvaranje {a.Broj} je odobren.", $"/akcije/{a.Id}", null, sada, ct);
        }
        else
        {
            z.Razlog = req.Razlog!.Trim();
            db.AkcijaDogadjaji.Add(new AkcijaDogadjaj { AkcijaId = a.Id, Vrsta = "zahtev_odbijen", Tekst = $"Zahtev za zatvaranje odbijen: {z.Razlog}", KorisnikId = currentUser.UserId, At = sada });
            await Obavesti.DodajAsync(db, [z.PodneoId], VrsteObavestenja.IshodZahteva, $"Zahtev za {a.Broj} je odbijen",
                $"Akcija ostaje otvorena. Razlog: {z.Razlog}", $"/akcije/{a.Id}", null, sada, ct);
        }
        await db.SaveChangesAsync(ct);
        return await Detalj(a.Id, ct);
    }

    /// <inheritdoc />
    public async Task<AkcijaDetaljDto> Handle(OtkaziZahtevZatvaranjaCommand req, CancellationToken ct)
    {
        var z = await db.ZahteviZatvaranja.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Zahtev ne postoji.");
        if (z.PodneoId != currentUser.UserId) throw new ForbiddenException("Zahtev otkazuje samo onaj ko ga je poslao.");
        if (z.Status != "zahtev") throw new BusinessRuleException("Otkazuje se samo zahtev koji čeka odluku.");
        (z.Status, z.OdlucenoAt) = ("otkazano", clock.UtcNow);
        db.AkcijaDogadjaji.Add(new AkcijaDogadjaj { AkcijaId = z.AkcijaId, Vrsta = "zahtev_otkazan", Tekst = "Zahtev za zatvaranje otkazao inženjer", KorisnikId = currentUser.UserId, At = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        return await Detalj(z.AkcijaId, ct);
    }

    /// <inheritdoc />
    public async Task<(Stream Sadrzaj, string Mime)> Handle(GetSlikaZatvaranjaQuery req, CancellationToken ct)
    {
        long? fajlId = req.ZahtevId is { } zid
            ? await db.ZahteviZatvaranja.Where(z => z.Id == zid && z.AkcijaId == req.AkcijaId).Select(z => z.FajlId).FirstOrDefaultAsync(ct)
            : await db.Akcije.Where(a => a.Id == req.AkcijaId).Select(a => a.ZatvaranjeFajlId).FirstOrDefaultAsync(ct);
        var fajl = fajlId is null ? null : await db.Fajlovi.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fajlId, ct);
        if (fajl is null || skladiste is null) throw new NotFoundException("Slika ne postoji.");
        return (skladiste.Otvori(fajl), fajl.Mime);
    }
}
