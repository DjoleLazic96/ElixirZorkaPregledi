using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Akcije;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Analitika;

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Pregled u kom je uređaj bio, sa stanjem uređaja na tom pregledu (R8.5).</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="Broj">Broj.</param>
/// <param name="Tip">Tip.</param>
/// <param name="Datum">Planirani dan.</param>
/// <param name="Inzenjer">Dodeljeni inženjer.</param>
/// <param name="Stanje"><c>ispravan</c>, <c>neispravan</c>, <c>van_funkcije</c>.</param>
/// <param name="Neispravnih">Broj neispravnih stavki uređaja.</param>
/// <param name="Status">Status pregleda.</param>
public sealed record PregledUredjajaDto(int PregledId, string Broj, string Tip, DateOnly Datum, string Inzenjer, string Stanje, int Neispravnih, string Status);

/// <summary>Dan vremenske linije.</summary>
/// <param name="Dan">Dan.</param>
/// <param name="Stanje">Stanje (null = još nije bilo pregleda).</param>
public sealed record DanIstorijeDto(DateOnly Dan, string? Stanje);

/// <summary>Stavka dosijea: napomena ili slike sa pregleda (R8.4).</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="Broj">Broj pregleda.</param>
/// <param name="Datum">Dan.</param>
/// <param name="Aktivnost">Aktivnost.</param>
/// <param name="Neispravan">Neispravan odgovor.</param>
/// <param name="Napomena">Napomena.</param>
/// <param name="Fotografije">ID-jevi slika.</param>
public sealed record DosijeStavkaDto(int PregledId, string Broj, DateOnly Datum, string Aktivnost, bool Neispravan, string? Napomena, IReadOnlyList<long> Fotografije);

/// <summary>Ekran uređaja (R8.4–R8.6).</summary>
public sealed record UredjajEkranDto(
    int Id, string Naziv, string Oznaka, string Prikaz, string Sekcija, string Vrsta, string StatusRegistra,
    string Status, int? DanaOdPregleda, PregledUredjajaDto? Poslednji, IReadOnlyList<string> Sabloni,
    DateOnly Od, DateOnly Do, IReadOnlyList<DanIstorijeDto> Istorija, AnalitikaUredjaja Analitika, string? NajcescaStavka,
    IReadOnlyList<PregledUredjajaDto> Pregledi, IReadOnlyList<DosijeStavkaDto> Dosije, IReadOnlyList<AkcijaDto> Akcije, IReadOnlyList<KvarDto> Kvarovi,
    Guid QrToken);

// ─── Zahtevi ──────────────────────────────────────────────────────────────

/// <summary>Ekran uređaja; <paramref name="Dana"/> je klizač perioda (30–365).</summary>
/// <param name="Id">Uređaj.</param>
/// <param name="Dana">Period unazad.</param>
public sealed record GetUredjajEkranQuery(int Id, int Dana = 90) : IRequest<UredjajEkranDto>;

/// <summary>Uređaj po tokenu sa QR nalepnice (R5.6).</summary>
/// <param name="Token">Token.</param>
public sealed record GetUredjajPoQrQuery(Guid Token) : IRequest<int>;

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Ekran uređaja: status, istorija, pregledi, dosije, akcije, kvarovi i analitika.</summary>
public sealed class UredjajEkranHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetUredjajEkranQuery, UredjajEkranDto>,
    IRequestHandler<GetUredjajPoQrQuery, int>
{
    /// <summary>Posle ovoliko dana bez pregleda status je „nije pregledan“ (DP se radi svakog radnog dana, pa nedelja znači da je ispao iz šablona ili rasporeda).</summary>
    public const int NijePregledanDana = 7;

    /// <summary>Stanje kao tekst za API.</summary>
    public static string Tekst(StanjeUredjaja s) => s switch
    {
        StanjeUredjaja.Ispravan => "ispravan",
        StanjeUredjaja.Neispravan => "neispravan",
        _ => "van_funkcije",
    };

    /// <inheritdoc />
    public async Task<int> Handle(GetUredjajPoQrQuery req, CancellationToken ct) =>
        await db.Uredjaji.Where(u => u.QrToken == req.Token).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Uređaj sa ovim QR kodom ne postoji.");

    /// <inheritdoc />
    public async Task<UredjajEkranDto> Handle(GetUredjajEkranQuery req, CancellationToken ct)
    {
        var u = await db.Uredjaji.AsNoTracking().Include(x => x.Sekcija).FirstOrDefaultAsync(x => x.Id == req.Id, ct)
                ?? throw new NotFoundException("Uređaj ne postoji.");
        var danas = clock.Today;
        var dana = Math.Clamp(req.Dana, 30, 365);
        var od = danas.AddDays(-dana + 1);
        var k = await RasporedKontekst.UcitajAsync(db, ct);

        var pregledi = await PreglediUredjajaAsync(db, u.Id, ct);
        var lista = pregledi.Select(p => new PregledUredjajaDto(p.Id, p.Broj, p.Tip, p.Datum, k.Ime(p.DodeljenId), Tekst(p.Stanje), p.Neispravnih, p.Status))
            .OrderByDescending(p => p.Datum).ThenByDescending(p => p.PregledId).ToList();
        var vazeci = pregledi.Where(p => p.Status != StatusPregleda.Ponisten).ToList();

        var istorija = AnalitikaPravila.Istorija(vazeci.Select(p => new StanjeNaDan(p.Datum, p.Stanje)), od, danas);
        var analitika = AnalitikaPravila.Izracunaj(istorija);

        var akcije = await new AkcijeIKvaroviHandler(db, currentUser, clock).Handle(new GetAkcijeQuery(new AkcijeFilteri(null, UredjajId: u.Id)), ct);
        var kvarovi = await new AkcijeIKvaroviHandler(db, currentUser, clock).Handle(new GetKvaroviQuery(new KvaroviFilteri(null, UredjajId: u.Id)), ct);
        var najcesca = kvarovi.Where(x => (x.Zatvoren ?? danas) >= od).GroupBy(x => x.Aktivnost)
            .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Sum(x => x.Prijava)).Select(g => g.Key).FirstOrDefault();

        var poslednji = lista.FirstOrDefault(p => p.Status != StatusPregleda.Ponisten);
        int? danaOd = poslednji is null ? null : Math.Max(0, danas.DayNumber - poslednji.Datum.DayNumber);
        var status = poslednji is null || danaOd > NijePregledanDana ? "nije_pregledan"
            : poslednji.Stanje == "van_funkcije" ? "van_funkcije"
            : kvarovi.Any(x => x.Zatvoren is null) ? "kvar"
            : "ok";

        var sabloni = await db.SablonStavke.AsNoTracking()
            .Where(s => s.UredjajId == u.Id && s.Verzija!.Status == StatusVerzije.Objavljena)
            .Select(s => s.Verzija!.Sablon!.Tip).Distinct().OrderBy(t => t).ToListAsync(ct);

        return new UredjajEkranDto(u.Id, u.Naziv, u.Oznaka, u.Prikaz, u.Sekcija?.Sifra ?? "", u.Vrsta, u.Status, status, danaOd, poslednji, sabloni,
            od, danas, istorija.Select(d => new DanIstorijeDto(d.Dan, d.Stanje is { } s ? Tekst(s) : null)).ToList(), analitika, najcesca,
            lista, await DosijeAsync(u.Id, ct), akcije, kvarovi, u.QrToken);
    }

    /// <summary>Predat pregled sa stanjem uređaja.</summary>
    internal sealed record PregledStanje(int Id, int UredjajId, string Broj, string Tip, DateOnly Datum, string DodeljenId, string Status, StanjeUredjaja Stanje, int Neispravnih);

    /// <summary>
    /// Predati pregledi (i poništeni, da se vide u listi) u kojima je uređaj bio: stanje je „van funkcije“ ako je tako označen,
    /// „neispravan“ ako je bar jedna stavka važeće dorade neispravna, inače „ispravan“.
    /// </summary>
    internal static async Task<List<PregledStanje>> PreglediUredjajaAsync(IAppDbContext db, int? uredjajId, CancellationToken ct, DateOnly? od = null, DateOnly? doDana = null)
    {
        var predati = db.Pregledi.AsNoTracking().Where(p => p.PredatAt != null);
        if (od is { } o) predati = predati.Where(p => p.PlaniraniDatum >= o);
        if (doDana is { } d) predati = predati.Where(p => p.PlaniraniDatum <= d);

        var odgovori = await (
                from x in db.Odgovori.AsNoTracking()
                join s in db.SablonStavke.AsNoTracking() on x.StavkaId equals s.Id
                join p in predati on x.PregledId equals p.Id
                where x.Dorada == p.Dorada && (uredjajId == null || s.UredjajId == uredjajId)
                group x by new { p.Id, s.UredjajId } into g
                select new { g.Key.Id, g.Key.UredjajId, Neispravnih = g.Count(x => x.Vrednost == 1) })
            .ToListAsync(ct);
        var van = await (
                from v in db.UredjajiVanFunkcije.AsNoTracking()
                join p in predati on v.PregledId equals p.Id
                where uredjajId == null || v.UredjajId == uredjajId
                select new { p.Id, v.UredjajId })
            .ToListAsync(ct);
        var ids = odgovori.Select(x => x.Id).Concat(van.Select(x => x.Id)).Distinct().ToList();
        var podaci = await predati.Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Broj, p.Tip, p.PlaniraniDatum, p.DodeljenId, p.Status }).ToDictionaryAsync(p => p.Id, ct);
        var vanSkup = van.Select(x => (x.Id, x.UredjajId)).ToHashSet();

        var parovi = odgovori.Select(x => (x.Id, x.UredjajId)).Union(vanSkup);
        var neisp = odgovori.ToDictionary(x => (x.Id, x.UredjajId), x => x.Neispravnih);
        return parovi.Select(par =>
        {
            var p = podaci[par.Id];
            var n = neisp.GetValueOrDefault(par);
            var stanje = vanSkup.Contains(par) ? StanjeUredjaja.VanFunkcije : n > 0 ? StanjeUredjaja.Neispravan : StanjeUredjaja.Ispravan;
            return new PregledStanje(p.Id, par.UredjajId, p.Broj, p.Tip, p.PlaniraniDatum, p.DodeljenId, p.Status, stanje, n);
        }).ToList();
    }

    private async Task<IReadOnlyList<DosijeStavkaDto>> DosijeAsync(int uredjajId, CancellationToken ct)
    {
        var redovi = await (
                from x in db.Odgovori.AsNoTracking()
                join s in db.SablonStavke.AsNoTracking() on x.StavkaId equals s.Id
                join p in db.Pregledi.AsNoTracking() on x.PregledId equals p.Id
                where s.UredjajId == uredjajId && x.Dorada == p.Dorada && p.PredatAt != null && p.Status != StatusPregleda.Ponisten
                      && (x.Napomena != null || db.Fotografije.Any(f => f.PregledId == p.Id && f.StavkaId == s.Id))
                orderby p.PlaniraniDatum descending
                select new { PregledId = p.Id, p.Broj, p.PlaniraniDatum, StavkaId = s.Id, Aktivnost = s.Aktivnost!.Naziv, x.Vrednost, x.Napomena })
            .ToListAsync(ct);
        var pIds = redovi.Select(r => r.PregledId).Distinct().ToList();
        var slike = (await db.Fotografije.AsNoTracking().Where(f => pIds.Contains(f.PregledId)).Select(f => new { f.PregledId, f.StavkaId, f.Id }).ToListAsync(ct))
            .ToLookup(f => (f.PregledId, f.StavkaId), f => f.Id);
        return redovi.Select(r => new DosijeStavkaDto(r.PregledId, r.Broj, r.PlaniraniDatum, r.Aktivnost, r.Vrednost == 1, r.Napomena, slike[(r.PregledId, r.StavkaId)].ToList())).ToList();
    }
}
