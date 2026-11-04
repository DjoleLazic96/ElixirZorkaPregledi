using System.Text.Json;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
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
public sealed record StavkaPopunjavanjaDto(int StavkaId, string Aktivnost, string OdgovorIspravan, string OdgovorNeispravan,
    IReadOnlyList<PoljeDto> Polja, OdgovorDto? Odgovor, IReadOnlyList<long> Fotografije);

/// <summary>Uređaj za popunjavanje.</summary>
/// <param name="UredjajId">ID.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Oznaka">Oznaka.</param>
/// <param name="VanFunkcije">Označen „Nije u funkciji“ na ovom pregledu.</param>
/// <param name="ProsliPutVanFunkcije">Bio van funkcije na poslednjem predatom pregledu (R4.6).</param>
/// <param name="Stavke">Aktivnosti.</param>
public sealed record UredjajPopunjavanjaDto(int UredjajId, string Naziv, string Oznaka, bool VanFunkcije, bool ProsliPutVanFunkcije,
    IReadOnlyList<StavkaPopunjavanjaDto> Stavke);

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
public sealed class PopunjavanjeHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<ZapocniPregledCommand, PopunjavanjeDto>,
    IRequestHandler<GetPopunjavanjeQuery, PopunjavanjeDto>,
    IRequestHandler<SinhronizacijaCommand, SinhronizacijaRezultat>,
    IRequestHandler<GetMojPaketQuery, PaketDto>
{
    /// <summary>Poruka kad pregled nastavlja drugi telefon (R10.5).</summary>
    public const string PorukaDrugiTelefon = "Pregled je započet na drugom telefonu. Nastaviće se kad taj telefon pošalje podatke.";

    /// <inheritdoc />
    public async Task<PaketDto> Handle(GetMojPaketQuery _, CancellationToken ct)
    {
        var granica = clock.Today.AddDays(7);
        var ja = currentUser.UserId;
        var pregledi = await db.Pregledi.Include(p => p.Ucesnici)
            .Where(p => (p.DodeljenId == ja || p.Ucesnici.Any(u => u.KorisnikId == ja))
                        && ((p.Status == StatusPregleda.Planiran && p.PlaniraniDatum <= granica) || p.Status == StatusPregleda.URadu))
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
        var slike = (await db.Fotografije.AsNoTracking().Where(f => f.PregledId == p.Id).Select(f => new { f.StavkaId, f.Id }).ToListAsync(ct))
            .ToLookup(f => f.StavkaId, f => f.Id);
        var prosli = await ProsliPutVanFunkcijeAsync(p, stavke.Select(s => s.UredjajId).Distinct().ToList(), ct);

        var sekcije = stavke.GroupBy(s => s.Sekcija).Select(gs =>
        {
            var (moja, radi) = KoRadiSekciju(p, gs.Key, k);
            return new SekcijaPopunjavanjaDto(gs.Key, moja, radi, gs.GroupBy(s => s.UredjajId).Select(gu => new UredjajPopunjavanjaDto(
                gu.Key, gu.First().Naziv, gu.First().Oznaka, van.Contains(gu.Key), prosli.Contains(gu.Key),
                gu.Select(s => new StavkaPopunjavanjaDto(s.StavkaId, s.Aktivnost, s.Ok, s.Nok, s.Polja,
                    odgovori.TryGetValue(s.StavkaId, out var o) ? new OdgovorDto(o.Vrednost, o.Napomena, JsonDocument.Parse(o.Polja).RootElement.Clone()) : null,
                    slike[s.StavkaId].ToList())).ToList())).ToList());
        }).ToList();

        return new PopunjavanjeDto(p.Id, p.Broj, p.Tip, p.Status, p.PlaniraniDatum, p.ZapocetAt, verzijaId, sekcije);
    }

    /// <inheritdoc />
    public async Task<SinhronizacijaRezultat> Handle(SinhronizacijaCommand req, CancellationToken ct)
    {
        _uredjajKlijenta = string.IsNullOrWhiteSpace(req.UredjajKlijenta) ? null : req.UredjajKlijenta;
        var prihvacene = new List<Guid>();
        var odbijene = new List<OdbijenaOperacija>();
        foreach (var op in req.Operacije)
        {
            if (await db.SinhronizacijaOperacije.AnyAsync(o => o.OpId == op.OpId, ct))
            {
                prihvacene.Add(op.OpId);   // već primenjeno — ponovljeno slanje posle prekida veze
                continue;
            }
            try
            {
                await PrimeniAsync(op, ct);
                db.SinhronizacijaOperacije.Add(new SinhronizacijaOperacija
                {
                    OpId = op.OpId, PregledId = op.PregledId, Vrsta = op.Vrsta, KlijentAt = op.KlijentAt.ToUniversalTime(), PrimljenoAt = clock.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                prihvacene.Add(op.OpId);
            }
            catch (AppException ex)
            {
                // Odbijena operacija ne zaustavlja ostale; klijent dobija razlog za korisnika.
                db.ChangeTracker.Clear();
                var razlog = ex is BusinessRuleException b ? string.Join(" ", b.Greske) : ex.Message;
                odbijene.Add(new OdbijenaOperacija(op.OpId, razlog));
                // Pregled je u međuvremenu prebačen ili poništen: urađeno se čuva kao sporan unos (R6.8).
                if ((ex is ForbiddenException || razlog == "Pregled je poništen.") && await db.Pregledi.AnyAsync(p => p.Id == op.PregledId, ct))
                {
                    db.SporniUnosi.Add(new SporanUnos
                    {
                        PregledId = op.PregledId, KorisnikId = currentUser.UserId!, Razlog = razlog, PrimljenoAt = clock.UtcNow,
                        Operacija = JsonSerializer.Serialize(op, Json),
                    });
                    await db.SaveChangesAsync(ct);
                }
            }
        }

        await ZabeleziOdstupanjeSataAsync(req, ct);
        return new SinhronizacijaRezultat(prihvacene, odbijene, clock.UtcNow);
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
        if (p.Status != StatusPregleda.URadu) throw new BusinessRuleException("Predat pregled se menja samo kroz doradu.");
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
                // Svaka upotreba se beleži (R4.3) — kontrolor vidi sekciju i vreme.
                PregledIstorija.Dodaj(db, p, "sve_ispravno", new { sekcija, stavki = neoznacene.Count, klijentAt }, currentUser.UserId, clock.UtcNow);
                break;
            }
            case "predaja":
                await PredajAsync(p, stavke, k, klijentAt, ct);
                break;
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

    private Task<int?> VazecaVerzijaAsync(string tip, CancellationToken ct) =>
        db.SablonVerzije.Where(v => v.Sablon!.Tip == tip && v.Status == StatusVerzije.Objavljena).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);

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
    private sealed record StavkaInfo(int StavkaId, string Sekcija, int UredjajId, string Naziv, string Oznaka, string Aktivnost, string Ok, string Nok, IReadOnlyList<PoljeDto> Polja);

    private async Task<IReadOnlyList<StavkaInfo>> StavkeAsync(int verzijaId, CancellationToken ct)
    {
        var redovi = await db.SablonStavke.AsNoTracking().Where(s => s.VerzijaId == verzijaId).OrderBy(s => s.Redosled)
            .Select(s => new
            {
                s.Id, Sekcija = db.Sekcije.Where(x => x.Id == s.SekcijaId).Select(x => x.Sifra).First(), s.UredjajId,
                s.Uredjaj!.Naziv, s.Uredjaj.Oznaka, Aktivnost = s.Aktivnost!.Naziv, s.Aktivnost.OdgovorIspravan, s.Aktivnost.OdgovorNeispravan,
                Polja = s.Polja.Select(p => p.DodatnoPolje!).ToList(),
            })
            .ToListAsync(ct);
        return redovi.Select(r => new StavkaInfo(r.Id, r.Sekcija, r.UredjajId, r.Naziv, r.Oznaka, r.Aktivnost, r.OdgovorIspravan, r.OdgovorNeispravan,
            r.Polja.Select(p => new PoljeDto(p.Id, p.Naziv, p.Tip, p.Jedinica, p.Oznake, p.Prikaz, p.Obavezno)).ToList())).ToList();
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
                where pr.PredatAt != null && pr.Id != p.Id
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
