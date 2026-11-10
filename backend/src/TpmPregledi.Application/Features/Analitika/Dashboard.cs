using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Akcije;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Analitika;

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Pregled na Dashboardu.</summary>
/// <param name="Id">ID.</param>
/// <param name="Broj">Broj.</param>
/// <param name="Tip">Tip.</param>
/// <param name="Datum">Planirani dan.</param>
/// <param name="Status">Status.</param>
/// <param name="Inzenjer">Kome je dodeljen.</param>
/// <param name="Overio">Ko je overio.</param>
/// <param name="Moj">Dodeljen meni ili sam učesnik.</param>
public sealed record PregledKratkoDto(int Id, string Broj, string Tip, DateOnly Datum, string Status, string Inzenjer, string? Overio, bool Moj);

/// <summary>Zahtev ili odluka (link vodi na mesto odluke).</summary>
/// <param name="Vrsta"><c>overa</c>, <c>dorada</c>, <c>prebacivanje</c>, <c>odsustvo</c>.</param>
/// <param name="Opis">Tekst.</param>
/// <param name="Link">Ruta u aplikaciji.</param>
/// <param name="Od">Od kada čeka.</param>
/// <param name="RokIstekao">Rok za overu je istekao (R6.3).</param>
public sealed record OdlukaDto(string Vrsta, string Opis, string Link, DateTime? Od, bool RokIstekao);

/// <summary>„Moje aktivnosti“ (R8.11).</summary>
/// <param name="Danas">Današnji pregledi.</param>
/// <param name="Zapoceti">Započeti ili vraćeni na doradu, a nepredati.</param>
/// <param name="Narednih7">Planirani u narednih 7 dana.</param>
/// <param name="Zahtevi">Moji zahtevi na čekanju.</param>
/// <param name="Akcije">Otvorene akcije na uređajima iz mojih pregleda.</param>
public sealed record MojeAktivnostiDto(IReadOnlyList<PregledKratkoDto> Danas, IReadOnlyList<PregledKratkoDto> Zapoceti, IReadOnlyList<PregledKratkoDto> Narednih7,
    IReadOnlyList<OdlukaDto> Zahtevi, IReadOnlyList<AkcijaDto> Akcije);

/// <summary>Uređaj van funkcije na poslednjem pregledu.</summary>
/// <param name="UredjajId">Uređaj.</param>
/// <param name="Uredjaj">Prikaz.</param>
/// <param name="Od">Od kog pregleda je van funkcije bez prekida.</param>
/// <param name="Dana">Koliko dana.</param>
public sealed record VanFunkcijeDto(int UredjajId, string Uredjaj, DateOnly Od, int Dana);

/// <summary>Brojevi na vrhu Dashboarda.</summary>
/// <param name="ProcenatNaDan">Ispunjenje plana ovog meseca (R8.9).</param>
/// <param name="ProcenatURoku">Overeni u roku ovog meseca.</param>
/// <param name="CekaOveru">Pregleda čeka overu.</param>
/// <param name="OtvorenihAkcija">Otvorenih akcija.</param>
/// <param name="OtvorenihKvarova">Otvorenih kvarova.</param>
/// <param name="VanFunkcije">Uređaja van funkcije.</param>
public sealed record KpiDto(double ProcenatNaDan, double ProcenatURoku, int CekaOveru, int OtvorenihAkcija, int OtvorenihKvarova, int VanFunkcije);

/// <summary>Dashboard (R8.10–R8.12): svi vide sve; „Moje aktivnosti“ ima ko popunjava, „Čeka moju odluku“ ko odlučuje.</summary>
public sealed record DashboardDto(
    MojeAktivnostiDto? Moje, IReadOnlyList<OdlukaDto>? CekaOdluku, KpiDto Kpi,
    IReadOnlyList<PregledKratkoDto> Danas, IReadOnlyList<PregledKratkoDto> Sutra, IReadOnlyList<PregledKratkoDto> CekajuOveru,
    IReadOnlyDictionary<string, int> AkcijePoHitnosti, IReadOnlyList<AkcijaDto> Akcije, IReadOnlyList<KvarDto> Kvarovi, IReadOnlyList<VanFunkcijeDto> VanFunkcije);

/// <summary>Dashboard za trenutnog korisnika.</summary>
public sealed record GetDashboardQuery : IRequest<DashboardDto>;

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Dashboard.</summary>
public sealed class DashboardHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    /// <summary>Koliko unazad se traži „van funkcije“ (dalje od toga uređaj je ionako „nije pregledan“).</summary>
    private const int VanFunkcijeUnazad = 90;

    /// <inheritdoc />
    public async Task<DashboardDto> Handle(GetDashboardQuery req, CancellationToken ct)
    {
        var ja = currentUser.UserId ?? string.Empty;
        var danas = clock.Today;
        var sutra = danas.AddDays(1);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var prava = (await db.KorisnikPrava.AsNoTracking().Where(p => p.KorisnikId == ja).Select(p => p.Pravo).ToListAsync(ct)).ToHashSet();
        var podesavanja = await new Podesavanja.PodesavanjaHandler(db).Handle(new Podesavanja.GetPodesavanjaQuery(), ct);

        var mojiIds = db.PregledUcesnici.Where(u => u.KorisnikId == ja).Select(u => u.PregledId);
        var pregledi = await db.Pregledi.AsNoTracking()
            .Where(p => p.Status != StatusPregleda.Ponisten && (
                (p.PlaniraniDatum >= danas && p.PlaniraniDatum <= danas.AddDays(7))
                || p.Status == StatusPregleda.CekaOveru || p.Status == StatusPregleda.URadu || p.Status == StatusPregleda.VracenNaDoradu))
            .OrderBy(p => p.PlaniraniDatum).ThenBy(p => p.Tip)
            .Select(p => new { P = p, Moj = p.DodeljenId == ja || mojiIds.Contains(p.Id) })
            .ToListAsync(ct);
        PregledKratkoDto Dto(Pregled p, bool moj) =>
            new(p.Id, p.Broj, p.Tip, p.PlaniraniDatum, p.Status, k.Ime(p.DodeljenId), p.OverioId is null ? null : k.Ime(p.OverioId), moj);
        var svi = pregledi.Select(x => Dto(x.P, x.Moj)).ToList();
        var predatAt = pregledi.ToDictionary(x => x.P.Id, x => x.P.PredatAt);

        var akcijeH = new AkcijeIKvaroviHandler(db, currentUser, clock);
        var akcije = await akcijeH.Handle(new GetAkcijeQuery(new AkcijeFilteri()), ct);
        var kvarovi = await akcijeH.Handle(new GetKvaroviQuery(new KvaroviFilteri()), ct);
        var van = await VanFunkcijeAsync(db, danas, ct);

        var cekajuOveru = svi.Where(p => p.Status == StatusPregleda.CekaOveru).OrderBy(p => p.Datum).ToList();
        bool RokIstekao(int id) => predatAt.GetValueOrDefault(id) is { } t && AnalitikaPravila.SatiRadnihDana(t, clock.UtcNow) > podesavanja.RokOvereSati;

        MojeAktivnostiDto? moje = null;
        if (prava.Contains(Prava.PreglediPopunjavanje))
        {
            var narednih = svi.Where(p => p.Moj && p.Datum > danas && p.Datum <= danas.AddDays(7) && p.Status == StatusPregleda.Planiran).ToList();
            var tipovi = svi.Where(p => p.Moj && p.Datum >= danas).Select(p => p.Tip).Distinct().ToList();
            var mojiUredjaji = (await db.SablonStavke.AsNoTracking()
                .Where(s => s.Verzija!.Status == StatusVerzije.Objavljena && tipovi.Contains(s.Verzija.Sablon!.Tip))
                .Select(s => s.UredjajId).Distinct().ToListAsync(ct)).ToHashSet();
            var mojeAkcije = (await db.Akcije.AsNoTracking().Where(a => a.KreiraoId == ja && a.Status == "otvorena").Select(a => a.Id).ToListAsync(ct)).ToHashSet();
            moje = new MojeAktivnostiDto(
                svi.Where(p => p.Moj && p.Datum == danas).ToList(),
                svi.Where(p => p.Moj && p.Status is StatusPregleda.URadu or StatusPregleda.VracenNaDoradu).ToList(),
                narednih, await MojiZahteviAsync(ja, ct),
                akcije.Where(a => mojiUredjaji.Contains(a.UredjajId) || mojeAkcije.Contains(a.Id)).ToList());
        }

        List<OdlukaDto>? odluke = null;
        // Supervizor i Direktor; Admin ima karticu samo zbog predloga izmena šablona (R2.8).
        if (currentUser.Roles.Contains(Roles.Supervizor) || currentUser.Roles.Contains(Roles.Direktor) || prava.Contains(Prava.SabloniNacrt))
        {
            odluke = [];
            if (prava.Contains(Prava.Overa))
            {
                odluke.AddRange(cekajuOveru.Select(p => new OdlukaDto("overa", $"Overa {p.Broj} ({p.Inzenjer})", $"/pregledi/{p.Id}", predatAt.GetValueOrDefault(p.Id), RokIstekao(p.Id))));
                odluke.AddRange((await (from z in db.ZahteviDorade.AsNoTracking()
                        join p in db.Pregledi.AsNoTracking() on z.PregledId equals p.Id
                        where z.Status == "zahtev"
                        select new { p.Id, p.Broj, z.PodneoId, z.Razlog, z.KreiranoAt }).ToListAsync(ct))
                    .Select(z => new OdlukaDto("dorada", $"Zahtev za doradu {z.Broj} ({k.Ime(z.PodneoId)}): {z.Razlog}", $"/pregledi/{z.Id}", z.KreiranoAt, false)));
            }
            if (prava.Contains(Prava.RasporedUredjivanje))
                odluke.AddRange((await (from z in db.ZahteviPrebacivanja.AsNoTracking()
                        join p in db.Pregledi.AsNoTracking() on z.PregledId equals p.Id
                        where z.Status == "zahtev"
                        select new { p.Broj, p.PlaniraniDatum, z.PodneoId, z.Razlog, z.KreiranoAt }).ToListAsync(ct))
                    .Select(z => new OdlukaDto("prebacivanje", $"Prebacivanje {z.Broj} ({z.PlaniraniDatum:dd.MM.yyyy}, {k.Ime(z.PodneoId)}): {z.Razlog}", "/raspored", z.KreiranoAt, false)));
            if (prava.Contains(Prava.OdsustvaOdobravanje))
                odluke.AddRange((await db.Odsustva.AsNoTracking().Where(o => o.Status == "zahtev").ToListAsync(ct))
                    .Select(o => new OdlukaDto("odsustvo", $"Odsustvo {k.Ime(o.KorisnikId)} {o.Od:dd.MM.yyyy}–{o.Do:dd.MM.yyyy}", "/raspored", o.KreiranoAt, false)));
            if (prava.Contains(Prava.SabloniNacrt))
                odluke.AddRange((await db.PredloziIzmena.AsNoTracking().Where(p => p.Status == "zahtev").Select(p => new { p.Sablon!.Tip, p.Opis, p.PodneoId, p.KreiranoAt }).ToListAsync(ct))
                    .Select(p => new OdlukaDto("predlog", $"Predlog izmene {p.Tip} ({k.Ime(p.PodneoId)}): {p.Opis}", "/sabloni/predlozi", p.KreiranoAt, false)));
            odluke = odluke.OrderByDescending(o => o.RokIstekao).ThenBy(o => o.Od).ToList();
        }

        var mesec = AnalitikaHandler.Zbir("", await AnalitikaHandler.IzracunajAsync(db, new DateOnly(danas.Year, danas.Month, 1), danas, danas, ct));
        var hitnosti = akcije.GroupBy(a => a.Hitnost).ToDictionary(g => g.Key, g => g.Count());

        return new DashboardDto(moje, odluke,
            new KpiDto(mesec.ProcenatNaDan, mesec.ProcenatURoku, cekajuOveru.Count, akcije.Count, kvarovi.Count, van.Count),
            svi.Where(p => p.Datum == danas).ToList(), svi.Where(p => p.Datum == sutra).ToList(), cekajuOveru,
            hitnosti, akcije.Take(10).ToList(), kvarovi.Take(10).ToList(), van);
    }

    private async Task<List<OdlukaDto>> MojiZahteviAsync(string ja, CancellationToken ct)
    {
        var rez = new List<OdlukaDto>();
        rez.AddRange((await (from z in db.ZahteviPrebacivanja.AsNoTracking()
                join p in db.Pregledi.AsNoTracking() on z.PregledId equals p.Id
                where z.PodneoId == ja && z.Status == "zahtev"
                select new { p.Id, p.Broj, z.KreiranoAt }).ToListAsync(ct))
            .Select(z => new OdlukaDto("prebacivanje", $"Prebacivanje {z.Broj}", "/kalendar", z.KreiranoAt, false)));
        rez.AddRange((await db.Odsustva.AsNoTracking().Where(o => o.KorisnikId == ja && o.Status == "zahtev").ToListAsync(ct))
            .Select(o => new OdlukaDto("odsustvo", $"Odsustvo {o.Od:dd.MM.yyyy}–{o.Do:dd.MM.yyyy}", "/moja-odsustva", o.KreiranoAt, false)));
        rez.AddRange((await (from z in db.ZahteviDorade.AsNoTracking()
                join p in db.Pregledi.AsNoTracking() on z.PregledId equals p.Id
                where z.PodneoId == ja && z.Status == "zahtev"
                select new { p.Id, p.Broj, z.KreiranoAt }).ToListAsync(ct))
            .Select(z => new OdlukaDto("dorada", $"Dorada {z.Broj}", $"/pregledi/{z.Id}", z.KreiranoAt, false)));
        return rez.OrderBy(z => z.Od).ToList();
    }

    /// <summary>Uređaji čiji poslednji predat pregled kaže „nije u funkciji“, sa danom od kog to traje bez prekida.</summary>
    internal static async Task<List<VanFunkcijeDto>> VanFunkcijeAsync(IAppDbContext db, DateOnly danas, CancellationToken ct)
    {
        var stanja = (await UredjajEkranHandler.PreglediUredjajaAsync(db, null, ct, od: danas.AddDays(-VanFunkcijeUnazad)))
            .Where(p => p.Status != StatusPregleda.Ponisten).ToList();
        var rez = new List<(int Id, DateOnly Od)>();
        foreach (var g in stanja.GroupBy(p => p.UredjajId))
        {
            var niz = g.OrderByDescending(p => p.Datum).ToList();
            if (niz[0].Stanje != StanjeUredjaja.VanFunkcije) continue;
            var od = niz.TakeWhile(p => p.Stanje == StanjeUredjaja.VanFunkcije).Last().Datum;
            rez.Add((g.Key, od));
        }
        var ids = rez.Select(r => r.Id).ToList();
        var nazivi = await db.Uredjaji.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Prikaz, ct);
        return rez.Select(r => new VanFunkcijeDto(r.Id, nazivi.GetValueOrDefault(r.Id, ""), r.Od, Math.Max(0, danas.DayNumber - r.Od.DayNumber)))
            .OrderByDescending(v => v.Dana).ToList();
    }
}
