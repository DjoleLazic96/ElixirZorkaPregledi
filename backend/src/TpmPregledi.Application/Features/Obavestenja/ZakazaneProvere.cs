using System.Text.Json;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Analitika;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Obavestenja;

/// <summary>Jedan prolaz zakazanih provera (spec §5.2); vraća broj novih obaveštenja.</summary>
public sealed record PokreniProvereCommand : IRequest<int>;

/// <summary>Jedan prolaz isporuke push-a i mejla za neisporučena obaveštenja; vraća broj isporučenih.</summary>
public sealed record IsporuciObavestenjaCommand : IRequest<int>;

/// <summary>
/// Zakazane provere. Svaka je idempotentna kroz ključ obaveštenja, pa posao sme da se pokreće svakog minuta i posle restarta:
/// isto obaveštenje istom korisniku ne ide dva puta.
/// </summary>
public sealed class ZakazaneProvereHandler(IAppDbContext db, IDateTimeProvider clock) : IRequestHandler<PokreniProvereCommand, int>
{
    /// <inheritdoc />
    public async Task<int> Handle(PokreniProvereCommand req, CancellationToken ct)
    {
        var sada = clock.UtcNow;
        var lokalno = AnalitikaPravila.LokalnoVreme(sada);
        var sat = TimeOnly.FromDateTime(lokalno);
        var danas = clock.Today;
        var p = await new PodesavanjaHandler(db).Handle(new GetPodesavanjaQuery(), ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);

        // Zakazane verzije šablona postaju važeće svog dana (R2.6).
        await AktivacijaVerzija.AktivirajDospeleAsync(db, danas, sada, ct);

        var broj = 0;
        broj += await NijeZapocetAsync(danas, sat, p, k, sada, ct);
        broj += await RokOvereAsync(p, sada, ct);
        broj += await RokAkcijeAsync(danas, sada, ct);
        broj += await VanFunkcijeAsync(danas, p, sada, ct);
        if (sat >= p.KrajRadnogDana)
        {
            broj += await RezimeAsync(danas, sada, ct);
            if (lokalno.DayOfWeek == DayOfWeek.Thursday) broj += await PredlogRasporedaAsync(danas, sada, ct);
        }
        await db.SaveChangesAsync(ct);
        return broj;
    }

    /// <summary>R9.2: današnji DP (PZ) koji nije započet do vremena upozorenja — primaocima i dodeljenom inženjeru.</summary>
    private async Task<int> NijeZapocetAsync(DateOnly danas, TimeOnly sat, PodesavanjaDto p, RasporedKontekst k, DateTime sada, CancellationToken ct)
    {
        var pregledi = await db.Pregledi.AsNoTracking().Where(x => x.PlaniraniDatum == danas && x.Status == StatusPregleda.Planiran).ToListAsync(ct);
        var broj = 0;
        foreach (var x in pregledi.Where(x => sat >= (x.JeDp ? p.UpozorenjeDp : p.UpozorenjePz)))
            broj += await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.NijeZapocet, $"{x.Broj} nije započet",
                $"Pregled {x.Broj} ({k.Ime(x.DodeljenId)}) za danas još nije započet.", $"/pregledi/{x.Id}", $"nije_zapocet:{x.Id}:{danas:yyyy-MM-dd}", sada, ct, x.DodeljenId);
        return broj;
    }

    /// <summary>R6.3: pregled čeka overu duže od roka (sati radnih dana); jednom po doradi.</summary>
    private async Task<int> RokOvereAsync(PodesavanjaDto p, DateTime sada, CancellationToken ct)
    {
        var cekaju = await db.Pregledi.AsNoTracking().Where(x => x.Status == StatusPregleda.CekaOveru && x.PredatAt != null).ToListAsync(ct);
        var broj = 0;
        foreach (var x in cekaju.Where(x => AnalitikaPravila.SatiRadnihDana(x.PredatAt!.Value, sada) > p.RokOvereSati))
            broj += await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.RokOvere, $"Overa {x.Broj} kasni",
                $"Pregled {x.Broj} čeka overu duže od {p.RokOvereSati} h radnih dana.", $"/pregledi/{x.Id}", $"rok_overe:{x.Id}:{x.Dorada}", sada, ct);
        return broj;
    }

    /// <summary>R7.8: otvorena akcija kojoj je istekao rok — jedno obaveštenje.</summary>
    private async Task<int> RokAkcijeAsync(DateOnly danas, DateTime sada, CancellationToken ct)
    {
        var akcije = await (
                from a in db.Akcije.AsNoTracking()
                join u in db.Uredjaji.AsNoTracking() on a.UredjajId equals u.Id
                where a.Status == "otvorena" && a.Rok != null && a.Rok < danas
                select new { a.Id, a.Broj, a.Rok, u.Naziv, u.Oznaka })
            .ToListAsync(ct);
        var broj = 0;
        foreach (var a in akcije)
            broj += await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.RokAkcije, $"Istekao rok akcije {a.Broj}",
                $"Akcija {a.Broj} ({a.Naziv} {a.Oznaka}) imala je rok {a.Rok:dd.MM.yyyy}.".Replace("  ", " "), $"/akcije/{a.Id}", $"rok_akcije:{a.Id}", sada, ct);
        return broj;
    }

    /// <summary>R8.3: uređaj van funkcije duže od granice; ponavlja se na svakih toliko dana dok je van funkcije.</summary>
    private async Task<int> VanFunkcijeAsync(DateOnly danas, PodesavanjaDto p, DateTime sada, CancellationToken ct)
    {
        var broj = 0;
        foreach (var v in (await DashboardHandler.VanFunkcijeAsync(db, danas, ct)).Where(v => v.Dana >= p.VanFunkcijeDana))
            broj += await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.VanFunkcije, $"{v.Uredjaj} van funkcije {v.Dana} dana",
                $"Uređaj {v.Uredjaj} nije u funkciji od {v.Od:dd.MM.yyyy}.", $"/uredjaji/{v.UredjajId}",
                $"van_funkcije:{v.UredjajId}:{v.Od:yyyy-MM-dd}:{v.Dana / p.VanFunkcijeDana}", sada, ct);
        return broj;
    }

    /// <summary>Dnevni rezime posle kraja radnog dana (samo dani kada je bilo pregleda).</summary>
    private async Task<int> RezimeAsync(DateOnly danas, DateTime sada, CancellationToken ct)
    {
        var dana = await db.Pregledi.AsNoTracking().Where(x => x.PrvobitniDatum == danas && x.Status != StatusPregleda.Ponisten)
            .Select(x => new { x.PredatAt }).ToListAsync(ct);
        if (dana.Count == 0) return 0;
        var predato = dana.Count(x => x.PredatAt != null);
        var cekaOveru = await db.Pregledi.CountAsync(x => x.Status == StatusPregleda.CekaOveru, ct);
        var hitne = await db.Akcije.CountAsync(a => a.Status == "otvorena" && a.Hitnost == "hitno", ct);
        var kvarovi = await db.Kvarovi.CountAsync(x => x.Otvoren == danas, ct);
        return await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.Rezime, $"Rezime za {danas:dd.MM.yyyy}",
            $"Predato {predato} od {dana.Count} pregleda, propušteno {dana.Count - predato}. Čeka overu: {cekaOveru}. Otvorene hitne akcije: {hitne}. Novi kvarovi: {kvarovi}.",
            "/", $"rezime:{danas:yyyy-MM-dd}", sada, ct);
    }

    /// <summary>Četvrtkom: ako DP za sledeću nedelju nije potvrđen, podsetnik da je predlog spreman (spec §5.2).</summary>
    private async Task<int> PredlogRasporedaAsync(DateOnly danas, DateTime sada, CancellationToken ct)
    {
        var doPonedeljka = ((int)DayOfWeek.Monday - (int)danas.DayOfWeek + 7) % 7;
        var ponedeljak = danas.AddDays(doPonedeljka == 0 ? 7 : doPonedeljka);
        var potvrdjen = await db.Pregledi.AnyAsync(x => x.Tip.StartsWith("DP") && x.PlaniraniDatum >= ponedeljak && x.PlaniraniDatum <= ponedeljak.AddDays(6), ct);
        if (potvrdjen) return 0;
        return await Obavesti.PrimaocimaAsync(db, VrsteObavestenja.PredlogRasporeda, "Raspored za sledeću nedelju",
            $"DP za nedelju od {ponedeljak:dd.MM.yyyy} još nije potvrđen; predlog po rotaciji je spreman.", "/raspored", $"predlog_rasporeda:{ponedeljak:yyyy-MM-dd}", sada, ct);
    }
}

/// <summary>
/// Isporuka: za svako neisporučeno obaveštenje push na sve uređaje korisnika i mejl, pa <c>isporuceno_at</c>. Istekle pretplate se brišu;
/// posle <see cref="MaxPokusaja"/> neuspeha obaveštenje ostaje samo u zvonu.
/// </summary>
public sealed class IsporukaHandler(IAppDbContext db, IDateTimeProvider clock, IPushSlanje push, IMejlSlanje mejl) : IRequestHandler<IsporuciObavestenjaCommand, int>
{
    /// <summary>Granica pokušaja.</summary>
    public const int MaxPokusaja = 5;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<int> Handle(IsporuciObavestenjaCommand req, CancellationToken ct)
    {
        var red = await db.Obavestenja.Where(o => o.IsporucenoAt == null && o.Pokusaja < MaxPokusaja).OrderBy(o => o.Id).Take(100).ToListAsync(ct);
        if (red.Count == 0) return 0;
        var korisnici = red.Select(o => o.KorisnikId).Distinct().ToList();
        var pretplate = (await db.PushPretplate.Where(p => korisnici.Contains(p.KorisnikId)).ToListAsync(ct)).ToLookup(p => p.KorisnikId);
        var adrese = await db.Users.Where(u => korisnici.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        var istekle = new HashSet<int>();
        var isporuceno = 0;
        foreach (var o in red)
        {
            try
            {
                if (push.JavniKljuc is not null)
                {
                    var poruka = JsonSerializer.Serialize(new { title = o.Naslov, body = o.Tekst, url = o.Link ?? "/", tag = o.Kljuc ?? $"o{o.Id}" }, Json);
                    foreach (var p in pretplate[o.KorisnikId].Where(p => !istekle.Contains(p.Id)))
                        if (await push.PosaljiAsync(p, poruka, ct) == PushIshod.Istekla && istekle.Add(p.Id)) db.PushPretplate.Remove(p);
                }
                if (adrese.GetValueOrDefault(o.KorisnikId) is { Length: > 0 } adresa) await mejl.PosaljiAsync(adresa, o.Naslov, o.Tekst, o.Link, ct);
                o.IsporucenoAt = clock.UtcNow;
                isporuceno++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                o.Pokusaja++;
            }
        }
        await db.SaveChangesAsync(ct);
        return isporuceno;
    }
}
