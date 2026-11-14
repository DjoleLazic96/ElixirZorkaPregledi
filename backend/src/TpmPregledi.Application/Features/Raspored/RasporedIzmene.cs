using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Raspored;

/// <summary>
/// Otkazivanje planiranog pregleda (zamerka 27). Za DP: <paramref name="Pomeri"/> = „Pomeri raspored“ — pregled se ne gubi,
/// nego on i ceo niz DP-ova idu dan napred (kao prenos, R3.2–R3.4); inače „Otkaži, zadrži raspored“ — pregled se poništava,
/// a naredni ostaju gde su. PZ se samo poništava.
/// </summary>
/// <param name="Id">Pregled.</param>
/// <param name="Razlog">Razlog (obavezan).</param>
/// <param name="Pomeri">Samo za DP: pomeri raspored umesto poništavanja.</param>
public sealed record OtkaziPregledCommand(int Id, string Razlog, bool Pomeri) : IRequest<PregledKalendarDto>;

/// <summary>„Povuci raspored“ (zamerka 29): oslobođen dan usred niza preuzima prvi kasniji planirani DP, ostali idu dan unazad.</summary>
/// <param name="Datum">Oslobođen dan.</param>
public sealed record PovuciRasporedCommand(DateOnly Datum) : IRequest<int>;

/// <summary>„Promeni DP“ (zamerka 31): drugi tip planiranog DP-a, uz usklađivanje narednih planiranih DP-ova.</summary>
/// <param name="Id">Pregled.</param>
/// <param name="Tip">DP1 ili DP2.</param>
/// <param name="Uskladi">Naredni planirani DP-ovi se preokrenu da smena DP1/DP2 ostane ispravna.</param>
public sealed record PromeniTipDpCommand(int Id, string Tip, bool Uskladi) : IRequest<PregledKalendarDto>;

/// <summary>Oblik ulaza za otkazivanje.</summary>
public sealed class OtkaziPregledValidator : AbstractValidator<OtkaziPregledCommand>
{
    /// <summary>Pravila.</summary>
    public OtkaziPregledValidator()
    {
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(500).WithMessage("Razlog može imati najviše 500 karaktera.");
    }
}

/// <summary>Oblik ulaza za promenu tipa DP-a.</summary>
public sealed class PromeniTipDpValidator : AbstractValidator<PromeniTipDpCommand>
{
    /// <summary>Pravila.</summary>
    public PromeniTipDpValidator()
    {
        RuleFor(x => x.Tip).Must(t => t is "DP1" or "DP2").WithMessage("Tip mora biti DP1 ili DP2.");
    }
}

public sealed partial class RasporedHandler :
    IRequestHandler<OtkaziPregledCommand, PregledKalendarDto>,
    IRequestHandler<PovuciRasporedCommand, int>,
    IRequestHandler<PromeniTipDpCommand, PregledKalendarDto>
{
    /// <summary>Otkazuje se samo planiran pregled; započet ili predat se poništava na stranici pregleda (pravo „overa“).</summary>
    public const string PorukaSamoPlaniran = "Otkazuje se samo planiran pregled; započet pregled se poništava na stranici pregleda.";

    /// <inheritdoc />
    public async Task<PregledKalendarDto> Handle(OtkaziPregledCommand req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == req.Id, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException(PorukaSamoPlaniran);
        var razlog = req.Razlog.Trim();
        var sada = clock.UtcNow;

        if (req.Pomeri)
        {
            if (!p.JeDp) throw new BusinessRuleException("Raspored se pomera samo za DP; PZ se otkazuje.");
            if (p.PlaniraniDatum < clock.Today) throw new BusinessRuleException(PorukaProsliDan);
            PregledIstorija.Dodaj(db, p, "otkazivanje_pomeri", new { razlog, datum = p.PlaniraniDatum }, currentUser.UserId, sada);
            await db.SaveChangesAsync(ct);
            await PomeriOdAsync(p.PlaniraniDatum, "otkazan", ct);
            return Dto(p, await RasporedKontekst.UcitajAsync(db, ct));
        }

        p.Status = StatusPregleda.Ponisten;
        p.PonistenRazlog = razlog;
        p.PonistioId = currentUser.UserId;
        PregledIstorija.Dodaj(db, p, "otkazivanje", new { razlog }, currentUser.UserId, sada);

        // Zahtev koji je čekao odluku za ovaj pregled više nema smisla — zatvara se, uz obaveštenje podnosiocu.
        foreach (var z in await db.ZahteviPrebacivanja.Where(z => z.PregledId == p.Id && z.Status == "zahtev").ToListAsync(ct))
        {
            z.Status = "odbijeno";
            z.OdlucioId = currentUser.UserId;
            z.OdlucenoAt = sada;
            await Obavesti.DodajAsync(db, [z.PodneoId], VrsteObavestenja.IshodZahteva, "Zahtev je zatvoren",
                $"{p.Broj} je otkazan, pa je vaš zahtev zatvoren.", "/kalendar", null, sada, ct);
        }
        await Obavesti.DodajAsync(db, [p.DodeljenId, .. p.Ucesnici.Select(u => u.KorisnikId)], VrsteObavestenja.Dodela, $"{p.Broj} je otkazan",
            $"{p.Broj} ({p.PlaniraniDatum:dd.MM.yyyy}) je otkazan. Razlog: {razlog}", "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);

        // Otkazan PZ oslobađa dan za DP — nudi se povlačenje niza (DP sa „zadrži raspored“ je svesno ostavljen prazan).
        return Dto(p, k) with { SlobodanDan = p.JeDp ? null : await SlobodanZaPovlacenjeAsync(p.PlaniraniDatum, ct) };
    }

    /// <inheritdoc />
    public async Task<int> Handle(PovuciRasporedCommand req, CancellationToken ct)
    {
        if (await SlobodanZaPovlacenjeAsync(req.Datum, ct) is null)
            throw new BusinessRuleException($"Za {req.Datum:dd.MM.yyyy} nema šta da se povuče (dan nije slobodan za DP ili posle njega nema planiranih DP-ova).");
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var pre = await PoslednjiDpAsync(req.Datum, ct);
        var prvi = await db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("DP") && p.Status == StatusPregleda.Planiran && p.PlaniraniDatum > req.Datum)
            .OrderBy(p => p.PlaniraniDatum).Select(p => p.DodeljenId).FirstAsync(ct);
        var inzenjer = k.SledeciInzenjer(req.Datum, pre?.DodeljenId) ?? prvi;
        var pomereno = await db.Pregledi.CountAsync(p => p.Tip.StartsWith("DP") && p.Status == StatusPregleda.Planiran && p.PlaniraniDatum > req.Datum, ct);
        await PovuciAsync(req.Datum, inzenjer, "povlacenje", k, ct);
        return pomereno;
    }

    /// <inheritdoc />
    public async Task<PregledKalendarDto> Handle(PromeniTipDpCommand req, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == req.Id, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        if (!p.JeDp) throw new BusinessRuleException("Tip se ovde menja samo DP-u; za PZ izaberite drugi tip pri zakazivanju.");
        if (p.Status != StatusPregleda.Planiran) throw new BusinessRuleException("Tip se menja samo pregledu koji nije započet.");
        if (p.Tip == req.Tip) throw new BusinessRuleException($"{p.Broj} je već {req.Tip}.");

        var sada = clock.UtcNow;
        var promene = new List<(string Kome, string Tekst)>();
        await PromeniTipAsync(p, req.Tip, promene, sada, ct);
        if (req.Uskladi)
        {
            var prethodni = req.Tip;
            foreach (var x in await db.Pregledi.Where(x => x.Tip.StartsWith("DP") && x.Status == StatusPregleda.Planiran && x.PlaniraniDatum > p.PlaniraniDatum)
                         .OrderBy(x => x.PlaniraniDatum).ToListAsync(ct))
            {
                var treba = RasporedPravila.SledeciDp(prethodni);
                if (x.Tip != treba) await PromeniTipAsync(x, treba, promene, sada, ct);
                prethodni = treba;
            }
        }
        foreach (var g in promene.GroupBy(x => x.Kome))
            await Obavesti.DodajAsync(db, [g.Key], VrsteObavestenja.Dodela, "Promenjen tip pregleda", $"{string.Join("; ", g.Select(x => x.Tekst))}.", "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return Dto(p, k);
    }

    /// <summary>Nov tip znači i nov broj (broj sadrži tip); stari broj se ne koristi ponovo.</summary>
    private async Task PromeniTipAsync(Pregled p, string tip, List<(string, string)> promene, DateTime sada, CancellationToken ct)
    {
        var stariBroj = p.Broj;
        p.Tip = tip;
        p.Broj = await brojac.SledeciAsync(tip, p.PlaniraniDatum.Year, ct);
        PregledIstorija.Dodaj(db, p, "promena_tipa", new { stariBroj, noviBroj = p.Broj }, currentUser.UserId, sada);
        promene.Add((p.DodeljenId, $"{stariBroj} je sada {p.Broj} ({p.PlaniraniDatum:dd.MM.yyyy})"));
    }

    /// <summary>
    /// Povlači sve planirane DP-ove posle <paramref name="dan"/> za jedno mesto unazad (<see cref="RasporedPravila.Povuci"/>) i snima.
    /// Vraća dan koji je ostao slobodan na kraju niza i inženjera koji je na njemu bio.
    /// </summary>
    private async Task<(DateOnly? Oslobodjen, string? Inzenjer)> PovuciAsync(DateOnly dan, string inzenjerZaDan, string razlog, RasporedKontekst k, CancellationToken ct)
    {
        var posle = await db.Pregledi.Where(p => p.Tip.StartsWith("DP") && p.Status == StatusPregleda.Planiran && p.PlaniraniDatum > dan)
            .OrderBy(p => p.PlaniraniDatum).ToListAsync(ct);
        var (pomeranja, oslobodjen, inzenjer) = RasporedPravila.Povuci(dan, inzenjerZaDan, posle);
        var sada = clock.UtcNow;
        var obavestenja = new List<(string Kome, string Tekst)>();
        foreach (var m in pomeranja)
        {
            obavestenja.Add((m.NoviDodeljenId, $"{m.Pregled.Broj} → {m.NoviDatum:dd.MM.yyyy}"));
            if (m.NoviDodeljenId != m.Pregled.DodeljenId) obavestenja.Add((m.Pregled.DodeljenId, $"{m.Pregled.Broj} više nije vaš (ide {k.Ime(m.NoviDodeljenId)})"));
            var stari = new { datum = m.Pregled.PlaniraniDatum, inzenjer = k.Ime(m.Pregled.DodeljenId) };
            m.Pregled.PlaniraniDatum = m.NoviDatum;
            m.Pregled.DodeljenId = m.NoviDodeljenId;
            PregledIstorija.Dodaj(db, m.Pregled, "povlacenje", new { razlog, stari, novi = new { datum = m.NoviDatum, inzenjer = k.Ime(m.NoviDodeljenId) } }, currentUser.UserId, sada);
        }
        foreach (var g in obavestenja.GroupBy(x => x.Kome))
            await Obavesti.DodajAsync(db, [g.Key], VrsteObavestenja.Dodela, "Raspored je povučen", $"{string.Join("; ", g.Select(x => x.Tekst))}.", "/kalendar", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return (oslobodjen, inzenjer);
    }

    /// <summary>
    /// Dan je „rupa usred niza“ (zamerka 29): danas ili kasnije, dan za DP (posle izmene — npr. PZ je otišao), bez DP-a,
    /// a posle njega postoje planirani DP-ovi. Inače null.
    /// </summary>
    private Task<DateOnly?> SlobodanZaPovlacenjeAsync(DateOnly d, CancellationToken ct) => SlobodanZaPovlacenjeAsync(db, clock, d, ct);

    /// <summary>Isto, za druge handlere (brisanje neradnog dana, zamerka 56).</summary>
    internal static async Task<DateOnly?> SlobodanZaPovlacenjeAsync(IAppDbContext db, IDateTimeProvider clock, DateOnly d, CancellationToken ct)
    {
        if (d < clock.Today) return null;
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        if (!k.JeRedovanDanZaDp(d)) return null;   // radni vikend dan bez DP-a nije rupa — tu se DP dodaje samo ručno (zamerka 68)
        if (await db.Pregledi.AnyAsync(p => p.Tip.StartsWith("DP") && p.Status != StatusPregleda.Ponisten && p.PlaniraniDatum == d, ct)) return null;
        return await db.Pregledi.AnyAsync(p => p.Tip.StartsWith("DP") && p.Status == StatusPregleda.Planiran && p.PlaniraniDatum > d, ct) ? d : null;
    }
}
