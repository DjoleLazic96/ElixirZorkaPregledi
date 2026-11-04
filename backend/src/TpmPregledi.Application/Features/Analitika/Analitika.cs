using System.Text.Json;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Analitika;

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Naziv i broj (stubić grafikona).</summary>
/// <param name="Naziv">Naziv.</param>
/// <param name="Broj">Broj.</param>
/// <param name="Id">ID za link (uređaj), ako postoji.</param>
public sealed record StavkaBrojDto(string Naziv, int Broj, int? Id = null);

/// <summary>Prosečno trajanje pregleda.</summary>
/// <param name="Naziv">Tip ili inženjer.</param>
/// <param name="Pregleda">Broj predatih pregleda.</param>
/// <param name="ProsekMinuta">Prosečno aktivno vreme (min).</param>
public sealed record TrajanjeDto(string Naziv, int Pregleda, int ProsekMinuta);

/// <summary>Opšta analitika (R8.8).</summary>
public sealed record OpstaAnalitikaDto(
    DateOnly Od, DateOnly Do, int Kvarova, double ProsekTrajanjaKvara,
    IReadOnlyList<StavkaBrojDto> PoSekciji, IReadOnlyList<StavkaBrojDto> PoUredjaju, IReadOnlyList<StavkaBrojDto> PoAktivnosti,
    IReadOnlyList<StavkaBrojDto> PoMesecu, IReadOnlyList<StavkaBrojDto> NajcesciKvarovi,
    IReadOnlyList<TrajanjeDto> TrajanjePoTipu, IReadOnlyList<TrajanjeDto> TrajanjePoInzenjeru, IReadOnlyList<StavkaBrojDto> VanFunkcije);

/// <summary>Red ispunjenja plana (R8.9).</summary>
/// <param name="Naziv">Mesec (<c>2026-11</c>), inženjer ili „Ukupno“.</param>
/// <param name="Planirano">Pregleda planiranih (do danas).</param>
/// <param name="NaDan">Predatih na planirani dan.</param>
/// <param name="Kasnije">Predatih posle planiranog dana.</param>
/// <param name="Propusteno">Nepredatih, a dan je prošao.</param>
/// <param name="Preneto">Prenetih na drugi dan.</param>
/// <param name="ProcenatNaDan">Procenat na dan.</param>
/// <param name="Overeno">Overenih.</param>
/// <param name="OverenoURoku">Overenih u roku.</param>
/// <param name="ProcenatURoku">Procenat overenih u roku (od predatih).</param>
/// <param name="ProsekOvereSati">Prosečno vreme do overe (sati radnih dana).</param>
public sealed record IspunjenjeRedDto(string Naziv, int Planirano, int NaDan, int Kasnije, int Propusteno, int Preneto, double ProcenatNaDan,
    int Overeno, int OverenoURoku, double ProcenatURoku, double ProsekOvereSati);

/// <summary>Ispunjenje plana po mesecu i inženjeru.</summary>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
/// <param name="Ukupno">Zbir.</param>
/// <param name="PoMesecu">Po mesecu.</param>
/// <param name="PoInzenjeru">Po inženjeru.</param>
public sealed record IspunjenjeDto(DateOnly Od, DateOnly Do, IspunjenjeRedDto Ukupno, IReadOnlyList<IspunjenjeRedDto> PoMesecu, IReadOnlyList<IspunjenjeRedDto> PoInzenjeru);

// ─── Zahtevi ──────────────────────────────────────────────────────────────

/// <summary>Opšta analitika za period (podrazumevano poslednjih 90 dana).</summary>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
public sealed record GetOpstaAnalitikaQuery(DateOnly? Od, DateOnly? Do) : IRequest<OpstaAnalitikaDto>;

/// <summary>Ispunjenje plana za period (podrazumevano tekuća godina).</summary>
/// <param name="Od">Od.</param>
/// <param name="Do">Do.</param>
public sealed record GetIspunjenjeQuery(DateOnly? Od, DateOnly? Do) : IRequest<IspunjenjeDto>;

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Opšta analitika i ispunjenje plana.</summary>
public sealed class AnalitikaHandler(IAppDbContext db, IDateTimeProvider clock) :
    IRequestHandler<GetOpstaAnalitikaQuery, OpstaAnalitikaDto>,
    IRequestHandler<GetIspunjenjeQuery, IspunjenjeDto>
{
    /// <inheritdoc />
    public async Task<OpstaAnalitikaDto> Handle(GetOpstaAnalitikaQuery req, CancellationToken ct)
    {
        var doDana = req.Do ?? clock.Today;
        var od = req.Od ?? doDana.AddDays(-89);
        var k = await RasporedKontekst.UcitajAsync(db, ct);

        // Kvar pripada periodu ako je u njemu otvoren.
        var kvarovi = await (
                from x in db.Kvarovi.AsNoTracking()
                join u in db.Uredjaji.AsNoTracking() on x.UredjajId equals u.Id
                join a in db.Aktivnosti.AsNoTracking() on x.AktivnostId equals a.Id
                where x.Otvoren >= od && x.Otvoren <= doDana
                select new { x.Otvoren, x.Zatvoren, x.UredjajId, u.Naziv, u.Oznaka, Sekcija = u.Sekcija!.Sifra, Aktivnost = a.Naziv })
            .ToListAsync(ct);
        string Prikaz(string naziv, string oznaka) => string.IsNullOrEmpty(oznaka) ? naziv : $"{naziv} {oznaka}";

        var zatvoreni = kvarovi.Where(x => x.Zatvoren is not null).Select(x => x.Zatvoren!.Value.DayNumber - x.Otvoren.DayNumber).ToList();
        var poSekciji = kvarovi.GroupBy(x => x.Sekcija).Select(g => new StavkaBrojDto(g.Key, g.Count())).OrderByDescending(x => x.Broj).ThenBy(x => x.Naziv).ToList();
        var poUredjaju = kvarovi.GroupBy(x => x.UredjajId).Select(g => new StavkaBrojDto(Prikaz(g.First().Naziv, g.First().Oznaka), g.Count(), g.Key))
            .OrderByDescending(x => x.Broj).ThenBy(x => x.Naziv).Take(10).ToList();
        var poAktivnosti = kvarovi.GroupBy(x => x.Aktivnost).Select(g => new StavkaBrojDto(g.Key, g.Count())).OrderByDescending(x => x.Broj).ThenBy(x => x.Naziv).Take(10).ToList();
        var poMesecu = kvarovi.GroupBy(x => $"{x.Otvoren.Year}-{x.Otvoren.Month:00}").Select(g => new StavkaBrojDto(g.Key, g.Count())).OrderBy(x => x.Naziv).ToList();
        var najcesci = kvarovi.GroupBy(x => new { x.UredjajId, x.Aktivnost })
            .Select(g => new StavkaBrojDto($"{Prikaz(g.First().Naziv, g.First().Oznaka)} — {g.Key.Aktivnost}", g.Count(), g.Key.UredjajId))
            .OrderByDescending(x => x.Broj).ThenBy(x => x.Naziv).Take(10).ToList();

        var predati = await db.Pregledi.AsNoTracking()
            .Where(p => p.PredatAt != null && p.Status != StatusPregleda.Ponisten && p.PlaniraniDatum >= od && p.PlaniraniDatum <= doDana && p.AktivnoSekundi > 0)
            .Select(p => new { p.Tip, p.DodeljenId, p.AktivnoSekundi }).ToListAsync(ct);
        var poTipu = predati.GroupBy(p => p.Tip).OrderBy(g => g.Key)
            .Select(g => new TrajanjeDto(g.Key, g.Count(), (int)Math.Round(g.Average(p => p.AktivnoSekundi) / 60))).ToList();
        var poInzenjeru = predati.GroupBy(p => k.Ime(p.DodeljenId)).OrderBy(g => g.Key)
            .Select(g => new TrajanjeDto(g.Key, g.Count(), (int)Math.Round(g.Average(p => p.AktivnoSekundi) / 60))).ToList();

        var van = await (
                from v in db.UredjajiVanFunkcije.AsNoTracking()
                join p in db.Pregledi.AsNoTracking() on v.PregledId equals p.Id
                join u in db.Uredjaji.AsNoTracking() on v.UredjajId equals u.Id
                where p.PredatAt != null && p.Status != StatusPregleda.Ponisten && p.PlaniraniDatum >= od && p.PlaniraniDatum <= doDana
                select new { u.Id, u.Naziv, u.Oznaka })
            .ToListAsync(ct);
        var vanFunkcije = van.GroupBy(x => x.Id).Select(g => new StavkaBrojDto(Prikaz(g.First().Naziv, g.First().Oznaka), g.Count(), g.Key))
            .OrderByDescending(x => x.Broj).ThenBy(x => x.Naziv).Take(10).ToList();

        return new OpstaAnalitikaDto(od, doDana, kvarovi.Count, zatvoreni.Count == 0 ? 0 : Math.Round(zatvoreni.Average(), 1),
            poSekciji, poUredjaju, poAktivnosti, poMesecu, najcesci, poTipu, poInzenjeru, vanFunkcije);
    }

    /// <inheritdoc />
    public async Task<IspunjenjeDto> Handle(GetIspunjenjeQuery req, CancellationToken ct)
    {
        var danas = clock.Today;
        var doDana = req.Do ?? danas;
        var od = req.Od ?? new DateOnly(doDana.Year, 1, 1);
        var redovi = await IzracunajAsync(db, od, doDana, danas, ct);
        return new IspunjenjeDto(od, doDana, Zbir("Ukupno", redovi),
            redovi.GroupBy(r => $"{r.PrvobitniDatum.Year}-{r.PrvobitniDatum.Month:00}").OrderBy(g => g.Key).Select(g => Zbir(g.Key, g)).ToList(),
            redovi.Where(r => !r.Odsutan).GroupBy(r => r.Inzenjer).OrderBy(g => g.Key).Select(g => Zbir(g.Key, g)).ToList());
    }

    /// <summary>Jedan planirani pregled u ispunjenju plana.</summary>
    internal sealed record IspunjenjeStavka(DateOnly PrvobitniDatum, string Inzenjer, bool Odsutan, bool Predat, bool NaDan, bool Preneto,
        bool Overen, bool URoku, double? SatiDoOvere);

    /// <summary>
    /// Pregledi planirani u periodu (zaključno sa jučerašnjim danom — današnji još traje), bez poništenih. Preneti pregled se računa onom kome je bio dodeljen pre prvog prenosa;
    /// pregled na dan odsustva tog inženjera ne ulazi u njegov red (R8.9).
    /// </summary>
    internal static async Task<List<IspunjenjeStavka>> IzracunajAsync(IAppDbContext db, DateOnly od, DateOnly doDana, DateOnly danas, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var rok = (await new PodesavanjaHandler(db).Handle(new GetPodesavanjaQuery(), ct)).RokOvereSati;
        var gornja = doDana < danas ? doDana : danas.AddDays(-1);
        var pregledi = await db.Pregledi.AsNoTracking()
            .Where(p => p.Status != StatusPregleda.Ponisten && p.PrvobitniDatum >= od && p.PrvobitniDatum <= gornja)
            .Select(p => new { p.Id, p.PrvobitniDatum, p.PlaniraniDatum, p.DodeljenId, p.PredatAt, p.OverenoAt })
            .ToListAsync(ct);
        var ids = pregledi.Select(p => p.Id).ToList();
        var prenosi = (await db.PregledDogadjaji.AsNoTracking().Where(d => d.Vrsta == "prenos" && ids.Contains(d.PregledId))
                .OrderBy(d => d.At).Select(d => new { d.PregledId, d.Podaci }).ToListAsync(ct))
            .GroupBy(d => d.PregledId).ToDictionary(g => g.Key, g => PrviInzenjer(g.First().Podaci));
        var idPoImenu = k.Imena.GroupBy(x => x.Value).ToDictionary(g => g.Key, g => g.First().Key);

        return pregledi.Select(p =>
        {
            var ime = prenosi.GetValueOrDefault(p.Id) ?? k.Ime(p.DodeljenId);
            var id = idPoImenu.GetValueOrDefault(ime) ?? p.DodeljenId;
            var predat = p.PredatAt is not null;
            double? sati = predat && p.OverenoAt is { } o ? AnalitikaPravila.SatiRadnihDana(p.PredatAt!.Value, o) : null;
            var uRoku = sati is { } s && s <= rok;
            return new IspunjenjeStavka(p.PrvobitniDatum, ime, k.JeOdsutan(id, p.PrvobitniDatum), predat,
                predat && AnalitikaPravila.LokalniDan(p.PredatAt!.Value) <= p.PrvobitniDatum, p.PlaniraniDatum != p.PrvobitniDatum,
                p.OverenoAt is not null, uRoku, sati);
        }).ToList();
    }

    /// <summary>Zbir grupe u red tabele.</summary>
    internal static IspunjenjeRedDto Zbir(string naziv, IEnumerable<IspunjenjeStavka> grupa)
    {
        var g = grupa.ToList();
        var predatih = g.Count(x => x.Predat);
        var naDan = g.Count(x => x.NaDan);
        var overeni = g.Where(x => x.SatiDoOvere is not null).ToList();
        var uRoku = g.Count(x => x.URoku);
        return new IspunjenjeRedDto(naziv, g.Count, naDan, predatih - naDan, g.Count - predatih, g.Count(x => x.Preneto),
            g.Count == 0 ? 0 : Math.Round(100.0 * naDan / g.Count, 1), overeni.Count, uRoku,
            predatih == 0 ? 0 : Math.Round(100.0 * uRoku / predatih, 1), overeni.Count == 0 ? 0 : Math.Round(overeni.Average(x => x.SatiDoOvere!.Value), 1));
    }

    private static string? PrviInzenjer(string podaci)
    {
        using var doc = JsonDocument.Parse(podaci);
        return doc.RootElement.TryGetProperty("stari", out var s) && s.TryGetProperty("inzenjer", out var i) ? i.GetString() : null;
    }
}
