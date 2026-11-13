using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Sabloni;

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Jedna razlika nacrta prema poslednjoj objavljenoj (ili zakazanoj) verziji.</summary>
/// <param name="Vrsta"><c>dodato</c>, <c>izbaceno</c>, <c>izmenjeno</c>, <c>pomereno</c>.</param>
/// <param name="Sekcija">Sekcija.</param>
/// <param name="Uredjaj">Uređaj.</param>
/// <param name="Aktivnost">Aktivnost (u nacrtu, ili izbačena).</param>
/// <param name="Bilo">Pre izmene (za izmenjeno).</param>
public sealed record RazlikaDto(string Vrsta, string Sekcija, string Uredjaj, string Aktivnost, string? Bilo);

/// <summary>Predlog izmene.</summary>
/// <param name="PodneoId">Ko je predložio (filter „Samo moji“, zamerka 36).</param>
/// <param name="PregledId">Pregled iz kog je predlog poslat (zamerka 34).</param>
/// <param name="PregledBroj">Broj tog pregleda.</param>
/// <param name="Saglasni">Ko se slaže sa predlogom (zamerka 37): ID i ime.</param>
/// <param name="OriginalId">Za duplikat: predlog koji ponavlja.</param>
public sealed record PredlogDto(int Id, string Sablon, string Vrsta, int? StavkaId, string? Stavka, string Opis, string Razlog, string Podneo, DateTime KreiranoAt,
    string Status, string? Odlucio, string? Odgovor, int? Verzija, string PodneoId = "", int? PregledId = null, string? PregledBroj = null,
    IReadOnlyList<SaglasanDto>? Saglasni = null, int? OriginalId = null);

/// <summary>Korisnik koji se slaže sa predlogom.</summary>
/// <param name="Id">ID.</param>
/// <param name="Ime">Ime.</param>
public sealed record SaglasanDto(string Id, string Ime);

/// <summary>Nacrt: stavke kao šablon, razlike i odobreni predlozi koji čekaju sledeću verziju.</summary>
/// <param name="Verzija">Nacrt (struktura kao šablon).</param>
/// <param name="OsnovnaVerzija">Verzija od koje je nacrt nastao.</param>
/// <param name="Razlike">Razlike.</param>
/// <param name="Predlozi">Odobreni, još nesprovedeni predlozi.</param>
/// <param name="Zakazana">Već zakazana verzija, ako postoji.</param>
public sealed record NacrtDto(SablonVerzijaDto Verzija, int? OsnovnaVerzija, IReadOnlyList<RazlikaDto> Razlike, IReadOnlyList<PredlogDto> Predlozi, ZakazanaDto? Zakazana);

/// <summary>Zakazana verzija.</summary>
/// <param name="Broj">Broj.</param>
/// <param name="VaziOd">Od kog dana važi.</param>
public sealed record ZakazanaDto(int Broj, DateOnly VaziOd);

/// <summary>Događaj u istorijatu (R2.9).</summary>
public sealed record SablonDogadjajDto(long Id, string Sablon, int? Verzija, string Vrsta, string Opis, string? KorisnikId, string Korisnik, DateTime At);

/// <summary>Verzija u listi verzija šablona.</summary>
public sealed record VerzijaKratkoDto(int Broj, string Status, DateOnly? VaziOd, DateTime? ObjavljenoAt, string? Objavio);

// ─── Zahtevi ──────────────────────────────────────────────────────────────

/// <summary>Nacrt šablona (null ako ga nema).</summary>
/// <param name="Tip">Šablon.</param>
public sealed record GetNacrtQuery(string Tip) : IRequest<NacrtDto?>;

/// <summary>Otvara nacrt kao kopiju poslednje objavljene (ili zakazane) verzije; postojeći nacrt se vraća takav kakav je.</summary>
/// <param name="Tip">Šablon.</param>
public sealed record OtvoriNacrtCommand(string Tip) : IRequest<NacrtDto>;

/// <summary>Odbacuje nacrt.</summary>
/// <param name="Tip">Šablon.</param>
public sealed record OdbaciNacrtCommand(string Tip) : IRequest;

/// <summary>Dodaje stavku (uređaj iz registra + aktivnost iz kataloga) u nacrt; ide posle poslednje stavke tog uređaja ili na kraj sekcije.</summary>
public sealed record DodajStavkuCommand(string Tip, int SekcijaId, int UredjajId, int AktivnostId, IReadOnlyList<int>? PoljaIds) : IRequest<NacrtDto>;

/// <summary>Izbacuje stavku iz nacrta.</summary>
/// <param name="Tip">Šablon.</param>
/// <param name="StavkaId">Stavka nacrta.</param>
public sealed record IzbaciStavkuCommand(string Tip, int StavkaId) : IRequest<NacrtDto>;

/// <summary>Menja aktivnost (ispravka teksta = druga aktivnost iz kataloga) i dodatna polja stavke; ključ stavke ostaje.</summary>
public sealed record IzmeniStavkuCommand(string Tip, int StavkaId, int AktivnostId, IReadOnlyList<int>? PoljaIds) : IRequest<NacrtDto>;

/// <summary>Pomera stavku jedno mesto gore ili dole.</summary>
/// <param name="Tip">Šablon.</param>
/// <param name="StavkaId">Stavka.</param>
/// <param name="Gore">Smer.</param>
public sealed record PomeriStavkuCommand(string Tip, int StavkaId, bool Gore) : IRequest<NacrtDto>;

/// <summary>Objava nacrta (samo Direktor, pravo <c>sabloni.objava</c>) sa datumom od kog važi.</summary>
/// <param name="Tip">Šablon.</param>
/// <param name="VaziOd">Od kog dana važi (danas ili kasnije).</param>
public sealed record ObjaviNacrtCommand(string Tip, DateOnly VaziOd) : IRequest<VerzijaKratkoDto>;

/// <summary>Sve verzije šablona.</summary>
/// <param name="Tip">Šablon.</param>
public sealed record GetVerzijeQuery(string Tip) : IRequest<IReadOnlyList<VerzijaKratkoDto>>;

/// <summary>Predlog izmene (R2.8); <paramref name="PregledId"/> kad je poslat tokom popunjavanja (zamerka 34).</summary>
public sealed record PodnesiPredlogCommand(string Tip, string Vrsta, int? StavkaId, string Opis, string Razlog, int? PregledId = null) : IRequest<PredlogDto>;

/// <summary>Predlozi (filter status, šablon, samo moji).</summary>
/// <param name="Status">Status ili null.</param>
/// <param name="Tip">Šablon ili null.</param>
/// <param name="SamoMoji">Samo predlozi koje je korisnik poslao ili sa kojima se složio (zamerka 36).</param>
public sealed record GetPredloziQuery(string? Status, string? Tip, bool SamoMoji = false) : IRequest<IReadOnlyList<PredlogDto>>;

/// <summary>Odluka o predlogu (Direktor, Admin).</summary>
/// <param name="Id">Predlog.</param>
/// <param name="Odobren">Odluka.</param>
/// <param name="Odgovor">Obrazloženje (opciono).</param>
/// <param name="DuplikatOd">Predlog je isti kao ovaj (zamerka 37): zatvara se kao duplikat, a odluka o originalu stiže i njegovom podnosiocu.</param>
public sealed record OdluciOPredloguCommand(int Id, bool Odobren, string? Odgovor, int? DuplikatOd = null) : IRequest<PredlogDto>;

/// <summary>„Slažem se sa predlogom“ (zamerka 37) umesto slanja istog predloga.</summary>
/// <param name="Id">Predlog.</param>
public sealed record SlazemSeSaPredlogomCommand(int Id) : IRequest<PredlogDto>;

/// <summary>Istorijat šablona (R2.9).</summary>
public sealed record GetIstorijatQuery(string? Tip, string? Vrsta, string? KorisnikId, DateOnly? Od, DateOnly? Do) : IRequest<IReadOnlyList<SablonDogadjajDto>>;

/// <summary>Razlog i opis su obavezni; vrsta je jedna od tri.</summary>
public sealed class PodnesiPredlogValidator : AbstractValidator<PodnesiPredlogCommand>
{
    /// <summary>Pravila.</summary>
    public PodnesiPredlogValidator()
    {
        RuleFor(x => x.Vrsta).Must(v => v is "dodavanje" or "izbacivanje" or "ispravka").WithMessage("Izaberite vrstu predloga.");
        RuleFor(x => x.Opis).NotEmpty().WithMessage("Opišite izmenu.").MaximumLength(1000).WithMessage("Opis može imati najviše 1000 karaktera.");
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(1000).WithMessage("Razlog može imati najviše 1000 karaktera.");
        RuleFor(x => x.StavkaId).NotNull().When(x => x.Vrsta == "izbacivanje").WithMessage("Izaberite stavku koja se izbacuje.");
    }
}

// ─── Pomoćno: aktivacija zakazanih verzija ────────────────────────────────

/// <summary>Zakazana verzija postaje važeća svog dana; prethodna objavljena postaje „zamenjena“. Pozivalac snima.</summary>
public static class AktivacijaVerzija
{
    /// <summary>Aktivira dospele zakazane verzije; vraća broj aktiviranih.</summary>
    public static async Task<int> AktivirajDospeleAsync(IAppDbContext db, DateOnly danas, DateTime sada, CancellationToken ct)
    {
        var dospele = await db.SablonVerzije.Where(v => v.Status == StatusVerzije.Zakazana && v.VaziOd <= danas).ToListAsync(ct);
        foreach (var v in dospele)
        {
            foreach (var stara in await db.SablonVerzije.Where(x => x.SablonId == v.SablonId && x.Status == StatusVerzije.Objavljena).ToListAsync(ct))
                stara.Status = StatusVerzije.Zamenjena;
            v.Status = StatusVerzije.Objavljena;
            db.SablonDogadjaji.Add(new SablonDogadjaj { SablonId = v.SablonId, VerzijaId = v.Id, Vrsta = "vazi", Opis = $"Verzija {v.Broj} je postala važeća.", At = sada });
        }
        return dospele.Count;
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Nacrt, objava, verzije, predlozi i istorijat (R2.6, R2.8, R2.9).</summary>
public sealed class IzmeneSablonaHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetNacrtQuery, NacrtDto?>,
    IRequestHandler<OtvoriNacrtCommand, NacrtDto>,
    IRequestHandler<OdbaciNacrtCommand>,
    IRequestHandler<DodajStavkuCommand, NacrtDto>,
    IRequestHandler<IzbaciStavkuCommand, NacrtDto>,
    IRequestHandler<IzmeniStavkuCommand, NacrtDto>,
    IRequestHandler<PomeriStavkuCommand, NacrtDto>,
    IRequestHandler<ObjaviNacrtCommand, VerzijaKratkoDto>,
    IRequestHandler<GetVerzijeQuery, IReadOnlyList<VerzijaKratkoDto>>,
    IRequestHandler<PodnesiPredlogCommand, PredlogDto>,
    IRequestHandler<GetPredloziQuery, IReadOnlyList<PredlogDto>>,
    IRequestHandler<OdluciOPredloguCommand, PredlogDto>,
    IRequestHandler<SlazemSeSaPredlogomCommand, PredlogDto>,
    IRequestHandler<GetIstorijatQuery, IReadOnlyList<SablonDogadjajDto>>
{
    /// <summary>Vrste predloga za prikaz.</summary>
    public static readonly IReadOnlyDictionary<string, string> VrstePredloga = new Dictionary<string, string>
    {
        ["dodavanje"] = "dodavanje", ["izbacivanje"] = "izbacivanje", ["ispravka"] = "ispravka teksta",
    };

    // ─── Nacrt ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<NacrtDto?> Handle(GetNacrtQuery req, CancellationToken ct)
    {
        var s = await SablonAsync(req.Tip, ct);
        var nacrt = await db.SablonVerzije.AsNoTracking().FirstOrDefaultAsync(v => v.SablonId == s.Id && v.Status == StatusVerzije.Nacrt, ct);
        return nacrt is null ? null : await NacrtDtoAsync(s, nacrt, ct);
    }

    /// <inheritdoc />
    public async Task<NacrtDto> Handle(OtvoriNacrtCommand req, CancellationToken ct)
    {
        var s = await SablonAsync(req.Tip, ct);
        if (await db.SablonVerzije.AnyAsync(v => v.SablonId == s.Id && v.Status == StatusVerzije.Nacrt, ct))
            return (await Handle(new GetNacrtQuery(req.Tip), ct))!;

        var osnova = await OsnovaAsync(s.Id, ct) ?? throw new BusinessRuleException($"Šablon {s.Tip} još nije uvezen.");
        var stavke = await db.SablonStavke.AsNoTracking().Include(x => x.Polja).Where(x => x.VerzijaId == osnova.Id).OrderBy(x => x.Redosled).ToListAsync(ct);
        var broj = await db.SablonVerzije.Where(v => v.SablonId == s.Id).MaxAsync(v => v.Broj, ct) + 1;
        var nacrt = new SablonVerzija { SablonId = s.Id, Broj = broj, Status = StatusVerzije.Nacrt };
        foreach (var x in stavke)
            nacrt.Stavke.Add(new SablonStavka
            {
                SekcijaId = x.SekcijaId, UredjajId = x.UredjajId, AktivnostId = x.AktivnostId, Redosled = x.Redosled, Kljuc = x.Kljuc,
                Polja = x.Polja.Select(p => new SablonStavkaPolje { DodatnoPoljeId = p.DodatnoPoljeId }).ToList(),
            });
        db.SablonVerzije.Add(nacrt);
        await db.SaveChangesAsync(ct);
        await DogadjajAsync(s.Id, nacrt.Id, "nacrt", $"Otvoren nacrt verzije {broj} (od verzije {osnova.Broj}).", ct);
        return (await Handle(new GetNacrtQuery(req.Tip), ct))!;
    }

    /// <inheritdoc />
    public async Task Handle(OdbaciNacrtCommand req, CancellationToken ct)
    {
        var (s, nacrt) = await NacrtAsync(req.Tip, ct);
        db.SablonStavke.RemoveRange(await db.SablonStavke.Where(x => x.VerzijaId == nacrt.Id).ToListAsync(ct));
        foreach (var d in await db.SablonDogadjaji.Where(d => d.VerzijaId == nacrt.Id).ToListAsync(ct)) d.VerzijaId = null;
        db.SablonVerzije.Remove(nacrt);
        await DogadjajAsync(s.Id, null, "nacrt_odbacen", $"Odbačen nacrt verzije {nacrt.Broj}.", ct);
    }

    /// <inheritdoc />
    public async Task<NacrtDto> Handle(DodajStavkuCommand req, CancellationToken ct)
    {
        var (s, nacrt) = await NacrtAsync(req.Tip, ct);
        var u = await db.Uredjaji.FirstOrDefaultAsync(x => x.Id == req.UredjajId, ct) ?? throw new NotFoundException("Uređaj ne postoji.");
        if (u.Status != StatusUredjaja.Aktivan) throw new BusinessRuleException("Uređaj je van upotrebe i ne dodaje se u šablone.");
        var a = await db.Aktivnosti.FirstOrDefaultAsync(x => x.Id == req.AktivnostId, ct) ?? throw new NotFoundException("Aktivnost ne postoji.");
        var sekcija = await db.Sekcije.FirstOrDefaultAsync(x => x.Id == req.SekcijaId, ct) ?? throw new NotFoundException("Sekcija ne postoji.");
        var stavke = await db.SablonStavke.Where(x => x.VerzijaId == nacrt.Id).OrderBy(x => x.Redosled).ToListAsync(ct);
        if (stavke.Any(x => x.UredjajId == u.Id && x.AktivnostId == a.Id)) throw new BusinessRuleException("Ova aktivnost je već na tom uređaju u šablonu.");
        await ProveriPoljaAsync(req.PoljaIds, ct);

        // Posle poslednje stavke istog uređaja u sekciji, inače na kraj sekcije, inače na kraj šablona.
        var posle = stavke.LastOrDefault(x => x.UredjajId == u.Id && x.SekcijaId == sekcija.Id) ?? stavke.LastOrDefault(x => x.SekcijaId == sekcija.Id) ?? stavke.LastOrDefault();
        var redosled = (posle?.Redosled ?? 0) + 1;
        foreach (var x in stavke.Where(x => x.Redosled >= redosled)) x.Redosled++;
        db.SablonStavke.Add(new SablonStavka
        {
            VerzijaId = nacrt.Id, SekcijaId = sekcija.Id, UredjajId = u.Id, AktivnostId = a.Id, Redosled = redosled,
            Polja = (req.PoljaIds ?? []).Distinct().Select(p => new SablonStavkaPolje { DodatnoPoljeId = p }).ToList(),
        });
        await DogadjajAsync(s.Id, nacrt.Id, "nacrt_izmena", $"Dodato: sekcija {sekcija.Sifra} · {u.Prikaz} · {a.Naziv}.", ct);
        return (await Handle(new GetNacrtQuery(req.Tip), ct))!;
    }

    /// <inheritdoc />
    public async Task<NacrtDto> Handle(IzbaciStavkuCommand req, CancellationToken ct)
    {
        var (s, nacrt) = await NacrtAsync(req.Tip, ct);
        var x = await db.SablonStavke.Include(y => y.Uredjaj).Include(y => y.Aktivnost).Include(y => y.Polja)
                    .FirstOrDefaultAsync(y => y.Id == req.StavkaId && y.VerzijaId == nacrt.Id, ct) ?? throw new NotFoundException("Stavka nije u nacrtu.");
        db.SablonStavke.Remove(x);
        await DogadjajAsync(s.Id, nacrt.Id, "nacrt_izmena", $"Izbačeno: {x.Uredjaj!.Prikaz} · {x.Aktivnost!.Naziv}.", ct);
        return (await Handle(new GetNacrtQuery(req.Tip), ct))!;
    }

    /// <inheritdoc />
    public async Task<NacrtDto> Handle(IzmeniStavkuCommand req, CancellationToken ct)
    {
        var (s, nacrt) = await NacrtAsync(req.Tip, ct);
        var x = await db.SablonStavke.Include(y => y.Uredjaj).Include(y => y.Aktivnost).Include(y => y.Polja)
                    .FirstOrDefaultAsync(y => y.Id == req.StavkaId && y.VerzijaId == nacrt.Id, ct) ?? throw new NotFoundException("Stavka nije u nacrtu.");
        var a = await db.Aktivnosti.FirstOrDefaultAsync(y => y.Id == req.AktivnostId, ct) ?? throw new NotFoundException("Aktivnost ne postoji.");
        if (a.Id != x.AktivnostId && await db.SablonStavke.AnyAsync(y => y.VerzijaId == nacrt.Id && y.UredjajId == x.UredjajId && y.AktivnostId == a.Id, ct))
            throw new BusinessRuleException("Ova aktivnost je već na tom uređaju u šablonu.");
        await ProveriPoljaAsync(req.PoljaIds, ct);
        var bilo = x.Aktivnost!.Naziv;
        x.AktivnostId = a.Id;
        var nova = (req.PoljaIds ?? []).Distinct().ToHashSet();
        foreach (var p in x.Polja.Where(p => !nova.Contains(p.DodatnoPoljeId)).ToList()) x.Polja.Remove(p);
        foreach (var p in nova.Where(p => x.Polja.All(y => y.DodatnoPoljeId != p))) x.Polja.Add(new SablonStavkaPolje { StavkaId = x.Id, DodatnoPoljeId = p });
        await DogadjajAsync(s.Id, nacrt.Id, "nacrt_izmena", bilo == a.Naziv ? $"Izmenjena polja: {x.Uredjaj!.Prikaz} · {a.Naziv}." : $"Izmenjeno: {x.Uredjaj!.Prikaz} · „{bilo}“ → „{a.Naziv}“.", ct);
        return (await Handle(new GetNacrtQuery(req.Tip), ct))!;
    }

    /// <inheritdoc />
    public async Task<NacrtDto> Handle(PomeriStavkuCommand req, CancellationToken ct)
    {
        var (_, nacrt) = await NacrtAsync(req.Tip, ct);
        var stavke = await db.SablonStavke.Where(y => y.VerzijaId == nacrt.Id).OrderBy(y => y.Redosled).ToListAsync(ct);
        var i = stavke.FindIndex(y => y.Id == req.StavkaId);
        if (i < 0) throw new NotFoundException("Stavka nije u nacrtu.");
        var j = req.Gore ? i - 1 : i + 1;
        // Stavka se pomera unutar svoje sekcije; preko granice sekcije bi sekcija promenila mesto na obrascu.
        if (j >= 0 && j < stavke.Count && stavke[j].SekcijaId == stavke[i].SekcijaId)
        {
            (stavke[i].Redosled, stavke[j].Redosled) = (stavke[j].Redosled, stavke[i].Redosled);
            await db.SaveChangesAsync(ct);
        }
        return (await Handle(new GetNacrtQuery(req.Tip), ct))!;
    }

    /// <inheritdoc />
    public async Task<VerzijaKratkoDto> Handle(ObjaviNacrtCommand req, CancellationToken ct)
    {
        var (s, nacrt) = await NacrtAsync(req.Tip, ct);
        var danas = clock.Today;
        if (req.VaziOd < danas) throw new BusinessRuleException("Datum od kog važi ne može biti u prošlosti.");
        if (await db.SablonVerzije.AnyAsync(v => v.SablonId == s.Id && v.Status == StatusVerzije.Zakazana, ct))
            throw new BusinessRuleException("Za ovaj šablon već postoji zakazana verzija; nova se objavljuje kad ona počne da važi.");
        var dto = await NacrtDtoAsync(s, nacrt, ct);
        if (dto.Razlike.Count == 0) throw new BusinessRuleException("Nacrt nema izmena u odnosu na važeću verziju.");
        if (!await db.SablonStavke.AnyAsync(x => x.VerzijaId == nacrt.Id, ct)) throw new BusinessRuleException("Šablon mora imati bar jednu stavku.");

        var sada = clock.UtcNow;
        nacrt.Status = StatusVerzije.Zakazana;
        nacrt.VaziOd = req.VaziOd;
        nacrt.ObjavioId = currentUser.UserId;
        nacrt.ObjavljenoAt = sada;
        foreach (var p in await db.PredloziIzmena.Where(p => p.SablonId == s.Id && p.Status == "odobren" && p.VerzijaId == null).ToListAsync(ct)) p.VerzijaId = nacrt.Id;
        db.SablonDogadjaji.Add(new SablonDogadjaj
        {
            SablonId = s.Id, VerzijaId = nacrt.Id, Vrsta = "objava", KorisnikId = currentUser.UserId, At = sada,
            Opis = $"Objavljena verzija {nacrt.Broj}, važi od {req.VaziOd:dd.MM.yyyy} ({dto.Razlike.Count} izmena). Započeti pregledi se završavaju po staroj verziji.",
        });
        await db.SaveChangesAsync(ct);
        // Verzija koja važi od danas odmah postaje važeća (ostale čeka zakazana provera).
        if (await AktivacijaVerzija.AktivirajDospeleAsync(db, danas, sada, ct) > 0) await db.SaveChangesAsync(ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        return new VerzijaKratkoDto(nacrt.Broj, nacrt.Status, nacrt.VaziOd, nacrt.ObjavljenoAt, k.Ime(nacrt.ObjavioId));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VerzijaKratkoDto>> Handle(GetVerzijeQuery req, CancellationToken ct)
    {
        var s = await SablonAsync(req.Tip, ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        return (await db.SablonVerzije.AsNoTracking().Where(v => v.SablonId == s.Id).OrderByDescending(v => v.Broj).ToListAsync(ct))
            .Select(v => new VerzijaKratkoDto(v.Broj, v.Status, v.VaziOd, v.ObjavljenoAt, v.ObjavioId is null ? null : k.Ime(v.ObjavioId))).ToList();
    }

    // ─── Predlozi ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PredlogDto> Handle(PodnesiPredlogCommand req, CancellationToken ct)
    {
        var p = await NoviPredlogAsync(db, req.Tip, req.Vrsta, req.StavkaId, req.Opis, req.Razlog, req.PregledId, currentUser.UserId!, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return (await DtoAsync([p], ct)).Single();
    }

    /// <summary>
    /// Upis predloga sa događajem u istoriji šablona i obaveštenjem Direktoru i Adminu (pravo <c>sabloni.nacrt</c>). Zajedničko za
    /// predlog sa ekrana Šabloni i predlog poslat tokom popunjavanja, i bez interneta kroz sinhronizaciju (zamerka 34).
    /// Pozivalac snima.
    /// </summary>
    public static async Task<PredlogIzmene> NoviPredlogAsync(IAppDbContext db, string tip, string vrsta, int? stavkaId, string opis, string razlog,
        int? pregledId, string podneoId, DateTime sada, CancellationToken ct)
    {
        tip = tip.ToUpperInvariant();
        var s = await db.Sabloni.FirstOrDefaultAsync(x => x.Tip == tip, ct) ?? throw new NotFoundException("Šablon ne postoji.");
        if (stavkaId is { } sid && !await db.SablonStavke.AnyAsync(x => x.Id == sid && x.Verzija!.SablonId == s.Id, ct))
            throw new BusinessRuleException("Stavka nije u ovom šablonu.");
        Pregled? pregled = null;
        if (pregledId is { } pid)
        {
            pregled = await db.Pregledi.FirstOrDefaultAsync(x => x.Id == pid, ct) ?? throw new NotFoundException("Pregled ne postoji.");
            if (pregled.Tip != tip) throw new BusinessRuleException("Predlog se odnosi na šablon tog pregleda.");
        }
        var p = new PredlogIzmene
        {
            SablonId = s.Id, Vrsta = vrsta, StavkaId = stavkaId, Opis = opis.Trim(), Razlog = razlog.Trim(), PodneoId = podneoId, KreiranoAt = sada, PregledId = pregledId,
        };
        db.PredloziIzmena.Add(p);
        db.SablonDogadjaji.Add(new SablonDogadjaj { SablonId = s.Id, Vrsta = "predlog", KorisnikId = podneoId, At = sada, Opis = $"Predlog ({VrstePredloga[vrsta]}): {p.Opis}" });
        if (pregled is not null) PregledIstorija.Dodaj(db, pregled, "predlog_izmene", new { vrsta, opis = p.Opis }, podneoId, sada);
        // Odlučuju Direktor i Admin (pravo sabloni.nacrt); predlog im stiže i u „Čeka moju odluku“.
        var odlucuju = db.KorisnikPrava.Where(x => x.Pravo == Prava.SabloniNacrt).Select(x => x.KorisnikId);
        var ids = await db.Users.Where(u => u.IsActive && odlucuju.Contains(u.Id) && u.Id != podneoId).Select(u => u.Id).ToListAsync(ct);
        await Obavesti.DodajAsync(db, ids, VrsteObavestenja.PredlogSablona, $"Predlog izmene {s.Tip}",
            $"{VrstePredloga[vrsta]}: {p.Opis}{(pregled is null ? "" : $" (iz pregleda {pregled.Broj})")}", "/sabloni/predlozi", null, sada, ct);
        return p;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PredlogDto>> Handle(GetPredloziQuery req, CancellationToken ct)
    {
        var upit = db.PredloziIzmena.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Status)) upit = upit.Where(p => p.Status == req.Status);
        if (!string.IsNullOrWhiteSpace(req.Tip)) upit = upit.Where(p => p.Sablon!.Tip == req.Tip);
        if (req.SamoMoji)
        {
            var ja = currentUser.UserId;
            upit = upit.Where(p => p.PodneoId == ja || p.Saglasnosti.Any(x => x.KorisnikId == ja));
        }
        return await DtoAsync(await upit.OrderByDescending(p => p.KreiranoAt).ToListAsync(ct), ct);
    }

    /// <inheritdoc />
    public async Task<PredlogDto> Handle(SlazemSeSaPredlogomCommand req, CancellationToken ct)
    {
        var p = await db.PredloziIzmena.Include(x => x.Saglasnosti).FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Predlog ne postoji.");
        if (p.Status != "zahtev") throw new BusinessRuleException("O ovom predlogu je već odlučeno.");
        var ja = currentUser.UserId!;
        if (p.PodneoId == ja) throw new BusinessRuleException("Ovo je vaš predlog.");
        if (p.Saglasnosti.All(x => x.KorisnikId != ja)) p.Saglasnosti.Add(new PredlogSaglasnost { PredlogId = p.Id, KorisnikId = ja, At = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        return (await DtoAsync([p], ct)).Single();
    }

    /// <inheritdoc />
    public async Task<PredlogDto> Handle(OdluciOPredloguCommand req, CancellationToken ct)
    {
        var p = await db.PredloziIzmena.Include(x => x.Sablon).FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Predlog ne postoji.");
        if (p.Status != "zahtev") throw new BusinessRuleException("O ovom predlogu je već odlučeno.");
        var sada = clock.UtcNow;
        if (req.DuplikatOd is { } oid) return await OznaciDuplikatAsync(p, oid, sada, ct);
        p.Status = req.Odobren ? "odobren" : "odbijen";
        p.OdlucioId = currentUser.UserId;
        p.OdlucenoAt = sada;
        p.Odgovor = string.IsNullOrWhiteSpace(req.Odgovor) ? null : req.Odgovor.Trim();
        db.SablonDogadjaji.Add(new SablonDogadjaj
        {
            SablonId = p.SablonId, Vrsta = req.Odobren ? "predlog_odobren" : "predlog_odbijen", KorisnikId = currentUser.UserId, At = sada,
            Opis = $"Predlog {(req.Odobren ? "odobren" : "odbijen")}: {p.Opis}{(p.Odgovor is null ? "" : $" ({p.Odgovor})")}",
        });
        // Odluka stiže podnosiocu, onima koji su se složili i podnosiocima predloga zatvorenih kao duplikat ovog (zamerka 37).
        var primaoci = new List<string> { p.PodneoId };
        primaoci.AddRange(await db.PredlogSaglasnosti.Where(x => x.PredlogId == p.Id).Select(x => x.KorisnikId).ToListAsync(ct));
        primaoci.AddRange(await db.PredloziIzmena.Where(x => x.OriginalId == p.Id).Select(x => x.PodneoId).ToListAsync(ct));
        await Obavesti.DodajAsync(db, primaoci.Distinct(), VrsteObavestenja.IshodZahteva, req.Odobren ? $"Predlog izmene {p.Sablon!.Tip} odobren" : $"Predlog izmene {p.Sablon!.Tip} odbijen",
            req.Odobren ? $"„{p.Opis}“ ulazi u sledeću verziju šablona." : $"„{p.Opis}“ je odbijen{(p.Odgovor is null ? "" : $": {p.Odgovor}")}.", $"/sabloni/predlozi?predlog={p.Id}", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return (await DtoAsync([p], ct)).Single();
    }

    /// <summary>Duplikat (zamerka 37): zatvara se sa vezom na original; podnosilac saznaje odluku kad se odluči o originalu.</summary>
    private async Task<PredlogDto> OznaciDuplikatAsync(PredlogIzmene p, int originalId, DateTime sada, CancellationToken ct)
    {
        if (originalId == p.Id) throw new BusinessRuleException("Predlog ne može biti duplikat samog sebe.");
        var o = await db.PredloziIzmena.FirstOrDefaultAsync(x => x.Id == originalId, ct) ?? throw new NotFoundException("Originalni predlog ne postoji.");
        if (o.SablonId != p.SablonId) throw new BusinessRuleException("Original mora biti predlog za isti šablon.");
        if (o.Status == "duplikat") throw new BusinessRuleException("Izaberite originalni predlog, ne drugi duplikat.");
        p.Status = "duplikat";
        p.OriginalId = o.Id;
        p.OdlucioId = currentUser.UserId;
        p.OdlucenoAt = sada;
        var tekst = o.Status == "zahtev"
            ? $"„{p.Opis}“ je isto što i ranije poslat predlog „{o.Opis}“. Obavestićemo vas o odluci."
            : $"„{p.Opis}“ je isto što i predlog „{o.Opis}“, koji je već {(o.Status == "odobren" ? "odobren" : "odbijen")}.";
        await Obavesti.DodajAsync(db, [p.PodneoId], VrsteObavestenja.IshodZahteva, "Predlog izmene je duplikat", tekst, $"/sabloni/predlozi?predlog={o.Id}", null, sada, ct);
        await db.SaveChangesAsync(ct);
        return (await DtoAsync([p], ct)).Single();
    }

    // ─── Istorijat ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SablonDogadjajDto>> Handle(GetIstorijatQuery req, CancellationToken ct)
    {
        var upit = from d in db.SablonDogadjaji.AsNoTracking()
                   join s in db.Sabloni.AsNoTracking() on d.SablonId equals s.Id
                   select new { D = d, s.Tip, Verzija = db.SablonVerzije.Where(v => v.Id == d.VerzijaId).Select(v => (int?)v.Broj).FirstOrDefault() };
        if (!string.IsNullOrWhiteSpace(req.Tip)) upit = upit.Where(x => x.Tip == req.Tip);
        if (!string.IsNullOrWhiteSpace(req.Vrsta)) upit = upit.Where(x => x.D.Vrsta == req.Vrsta);
        if (!string.IsNullOrWhiteSpace(req.KorisnikId)) upit = upit.Where(x => x.D.KorisnikId == req.KorisnikId);
        var lista = await upit.OrderByDescending(x => x.D.At).ThenByDescending(x => x.D.Id).Take(1000).ToListAsync(ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        return lista.Where(x => (req.Od is null || AnalitikaDan(x.D.At) >= req.Od) && (req.Do is null || AnalitikaDan(x.D.At) <= req.Do))
            .Select(x => new SablonDogadjajDto(x.D.Id, x.Tip, x.Verzija, x.D.Vrsta, x.D.Opis, x.D.KorisnikId, x.D.KorisnikId is null ? "sistem" : k.Ime(x.D.KorisnikId), x.D.At)).ToList();
    }

    // ─── Pomoćno ──────────────────────────────────────────────────────────

    private static DateOnly AnalitikaDan(DateTime utc) => AnalitikaPravila.LokalniDan(utc);

    private async Task<Sablon> SablonAsync(string tip, CancellationToken ct) =>
        await db.Sabloni.FirstOrDefaultAsync(s => s.Tip == tip.ToUpperInvariant(), ct) ?? throw new NotFoundException("Šablon ne postoji.");

    private async Task<(Sablon, SablonVerzija)> NacrtAsync(string tip, CancellationToken ct)
    {
        var s = await SablonAsync(tip, ct);
        var n = await db.SablonVerzije.FirstOrDefaultAsync(v => v.SablonId == s.Id && v.Status == StatusVerzije.Nacrt, ct)
                ?? throw new BusinessRuleException($"Šablon {s.Tip} nema otvoren nacrt.");
        return (s, n);
    }

    /// <summary>Osnova nacrta: zakazana verzija ako postoji (ona je najnovija), inače važeća.</summary>
    private Task<SablonVerzija?> OsnovaAsync(int sablonId, CancellationToken ct) =>
        db.SablonVerzije.AsNoTracking().Where(v => v.SablonId == sablonId && (v.Status == StatusVerzije.Zakazana || v.Status == StatusVerzije.Objavljena))
            .OrderByDescending(v => v.Broj).FirstOrDefaultAsync(ct);

    private async Task ProveriPoljaAsync(IReadOnlyList<int>? polja, CancellationToken ct)
    {
        if (polja is not { Count: > 0 }) return;
        var postoje = await db.DodatnaPolja.CountAsync(p => polja.Contains(p.Id), ct);
        if (postoje != polja.Distinct().Count()) throw new BusinessRuleException("Dodatno polje ne postoji.");
    }

    private async Task DogadjajAsync(int sablonId, int? verzijaId, string vrsta, string opis, CancellationToken ct, bool sacuvaj = true)
    {
        db.SablonDogadjaji.Add(new SablonDogadjaj { SablonId = sablonId, VerzijaId = verzijaId, Vrsta = vrsta, Opis = opis, KorisnikId = currentUser.UserId, At = clock.UtcNow });
        if (sacuvaj) await db.SaveChangesAsync(ct);
    }

    private async Task<NacrtDto> NacrtDtoAsync(Sablon s, SablonVerzija nacrt, CancellationToken ct)
    {
        var verzija = await new SabloniQueriesHandler(db).Handle(new GetSablonVerzijaQuery(s.Tip, nacrt.Broj), ct);
        var osnova = await db.SablonVerzije.AsNoTracking()
            .Where(v => v.SablonId == s.Id && (v.Status == StatusVerzije.Zakazana || v.Status == StatusVerzije.Objavljena) && v.Broj < nacrt.Broj)
            .OrderByDescending(v => v.Broj).FirstOrDefaultAsync(ct);
        var razlike = osnova is null ? [] : Razlike(await new SabloniQueriesHandler(db).Handle(new GetSablonVerzijaQuery(s.Tip, osnova.Broj), ct), verzija);
        var predlozi = await DtoAsync(await db.PredloziIzmena.AsNoTracking().Where(p => p.SablonId == s.Id && p.Status == "odobren" && p.VerzijaId == null).ToListAsync(ct), ct);
        var zakazana = await db.SablonVerzije.AsNoTracking().Where(v => v.SablonId == s.Id && v.Status == StatusVerzije.Zakazana)
            .Select(v => new { v.Broj, v.VaziOd }).FirstOrDefaultAsync(ct);
        return new NacrtDto(verzija, osnova?.Broj, razlike, predlozi, zakazana is null ? null : new ZakazanaDto(zakazana.Broj, zakazana.VaziOd!.Value));
    }

    /// <summary>Razlike po stalnom ključu stavke (R2.5): nova, izbačena, druga aktivnost ili polja, i promena redosleda unutar uređaja.</summary>
    internal static IReadOnlyList<RazlikaDto> Razlike(SablonVerzijaDto stara, SablonVerzijaDto nova)
    {
        static IEnumerable<(string Sek, UredjajUSablonuDto U, StavkaDto S, int Red)> Ravno(SablonVerzijaDto v)
        {
            var red = 0;
            foreach (var sek in v.Sekcije)
                foreach (var u in sek.Uredjaji)
                    foreach (var s in u.Stavke) yield return (sek.Sifra, u, s, red++);
        }
        static string Prikaz(UredjajUSablonuDto u) => string.IsNullOrEmpty(u.Oznaka) ? u.Naziv : $"{u.Naziv} {u.Oznaka}";

        var a = Ravno(stara).ToDictionary(x => x.S.Kljuc);
        var b = Ravno(nova).ToList();
        var rez = new List<RazlikaDto>();
        foreach (var x in b)
        {
            if (!a.TryGetValue(x.S.Kljuc, out var y)) { rez.Add(new RazlikaDto("dodato", x.Sek, Prikaz(x.U), x.S.Aktivnost, null)); continue; }
            if (y.S.AktivnostId != x.S.AktivnostId || !y.S.Polja.OrderBy(p => p).SequenceEqual(x.S.Polja.OrderBy(p => p)))
                rez.Add(new RazlikaDto("izmenjeno", x.Sek, Prikaz(x.U), x.S.Aktivnost + (x.S.Polja.Count > 0 ? $" [{string.Join(", ", x.S.Polja)}]" : ""),
                    y.S.Aktivnost + (y.S.Polja.Count > 0 ? $" [{string.Join(", ", y.S.Polja)}]" : "")));
        }
        var uNovoj = b.Select(x => x.S.Kljuc).ToHashSet();
        rez.AddRange(a.Values.Where(y => !uNovoj.Contains(y.S.Kljuc)).Select(y => new RazlikaDto("izbaceno", y.Sek, Prikaz(y.U), y.S.Aktivnost, null)));

        // Redosled: poredi se redosled zajedničkih stavki; pomerena je ona koja menja relativni položaj.
        var zajednicke = b.Where(x => a.ContainsKey(x.S.Kljuc)).Select(x => x.S.Kljuc).ToList();
        var poStarom = zajednicke.OrderBy(k => a[k].Red).ToList();
        for (var i = 0; i < zajednicke.Count; i++)
            if (zajednicke[i] != poStarom[i])
            {
                var x = b.First(z => z.S.Kljuc == zajednicke[i]);
                rez.Add(new RazlikaDto("pomereno", x.Sek, Prikaz(x.U), x.S.Aktivnost, null));
            }
        return rez;
    }

    private async Task<IReadOnlyList<PredlogDto>> DtoAsync(List<PredlogIzmene> predlozi, CancellationToken ct)
    {
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var sIds = predlozi.Select(p => p.SablonId).Distinct().ToList();
        var tipovi = await db.Sabloni.Where(s => sIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Tip, ct);
        var stIds = predlozi.Where(p => p.StavkaId != null).Select(p => p.StavkaId!.Value).Distinct().ToList();
        var stavke = (await db.SablonStavke.AsNoTracking().Where(s => stIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Uredjaj!.Naziv, s.Uredjaj.Oznaka, Aktivnost = s.Aktivnost!.Naziv }).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => $"{s.Naziv} {s.Oznaka} · {s.Aktivnost}".Replace("  ", " "));
        var vIds = predlozi.Where(p => p.VerzijaId != null).Select(p => p.VerzijaId!.Value).Distinct().ToList();
        var verzije = await db.SablonVerzije.Where(v => vIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Broj, ct);
        var pIds = predlozi.Where(p => p.PregledId != null).Select(p => p.PregledId!.Value).Distinct().ToList();
        var brojevi = await db.Pregledi.AsNoTracking().Where(x => pIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Broj, ct);
        var ids = predlozi.Select(p => p.Id).ToList();
        var saglasni = (await db.PredlogSaglasnosti.AsNoTracking().Where(x => ids.Contains(x.PredlogId)).OrderBy(x => x.At).ToListAsync(ct))
            .GroupBy(x => x.PredlogId).ToDictionary(g => g.Key, g => (IReadOnlyList<SaglasanDto>)g.Select(x => new SaglasanDto(x.KorisnikId, k.Ime(x.KorisnikId))).ToList());
        return predlozi.Select(p => new PredlogDto(p.Id, tipovi.GetValueOrDefault(p.SablonId, ""), p.Vrsta, p.StavkaId, p.StavkaId is { } s ? stavke.GetValueOrDefault(s) : null,
            p.Opis, p.Razlog, k.Ime(p.PodneoId), p.KreiranoAt, p.Status, p.OdlucioId is null ? null : k.Ime(p.OdlucioId), p.Odgovor,
            p.VerzijaId is { } v ? verzije.GetValueOrDefault(v) : null, p.PodneoId, p.PregledId, p.PregledId is { } pr ? brojevi.GetValueOrDefault(pr) : null,
            saglasni.GetValueOrDefault(p.Id, []), p.OriginalId)).ToList();
    }
}
