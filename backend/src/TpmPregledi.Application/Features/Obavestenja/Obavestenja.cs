using System.Text.Json;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Obavestenja;

// ─── Kanali (Infrastructure) ──────────────────────────────────────────────

/// <summary>Ishod slanja jednog push-a.</summary>
public enum PushIshod
{
    /// <summary>Push servis je primio poruku.</summary>
    Poslato,

    /// <summary>Pretplata više ne važi (410/404) — briše se.</summary>
    Istekla,
}

/// <summary>Web Push (VAPID).</summary>
public interface IPushSlanje
{
    /// <summary>Javni VAPID ključ za <c>pushManager.subscribe</c>; null = push nije podešen.</summary>
    string? JavniKljuc { get; }

    /// <summary>Šalje JSON poruku na pretplatu; greška mreže ili servisa baca izuzetak (posao isporuke pokušava ponovo).</summary>
    Task<PushIshod> PosaljiAsync(PushPretplata pretplata, string poruka, CancellationToken ct);
}

/// <summary>Mejl (Microsoft Graph; bez naloga samo upis u log).</summary>
public interface IMejlSlanje
{
    /// <summary>Šalje mejl; <paramref name="link"/> je ruta u aplikaciji (kanal dodaje javnu adresu).</summary>
    Task PosaljiAsync(string adresa, string naslov, string tekst, string? link, CancellationToken ct);
}

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Obaveštenje u zvonu.</summary>
/// <param name="Id">ID.</param>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Naslov">Naslov.</param>
/// <param name="Tekst">Tekst.</param>
/// <param name="Link">Ruta.</param>
/// <param name="KreiranoAt">Kada.</param>
/// <param name="Procitano">Pročitano.</param>
public sealed record ObavestenjeDto(long Id, string Vrsta, string Naslov, string Tekst, string? Link, DateTime KreiranoAt, bool Procitano);

/// <summary>Zvono: poslednja obaveštenja, broj nepročitanih i broj stvari koje čekaju moju odluku (R8.12).</summary>
/// <param name="Neprocitanih">Nepročitanih.</param>
/// <param name="CekaOdluku">Čeka moju odluku (Supervizor, Direktor).</param>
/// <param name="Stavke">Poslednjih 50.</param>
public sealed record ZvonoDto(int Neprocitanih, int CekaOdluku, IReadOnlyList<ObavestenjeDto> Stavke);

/// <summary>Vrsta sa primaocima.</summary>
/// <param name="Vrsta">Kod.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Opis">Kada stiže.</param>
/// <param name="Podeseno">Admin je sačuvao izbor (inače podrazumevano: Supervizor i Direktor).</param>
/// <param name="KorisnikIds">Primaoci.</param>
public sealed record VrstaPrimalacaDto(string Vrsta, string Naziv, string Opis, bool Podeseno, IReadOnlyList<string> KorisnikIds);

/// <summary>Korisnik za štikliranje.</summary>
/// <param name="Id">ID.</param>
/// <param name="Ime">Ime.</param>
public sealed record PrimalacKorisnikDto(string Id, string Ime);

/// <summary>Primaoci obaveštenja (R9.4).</summary>
/// <param name="Vrste">Vrste.</param>
/// <param name="Korisnici">Aktivni korisnici.</param>
public sealed record PrimaociDto(IReadOnlyList<VrstaPrimalacaDto> Vrste, IReadOnlyList<PrimalacKorisnikDto> Korisnici);

// ─── Zahtevi ──────────────────────────────────────────────────────────────

/// <summary>Zvono trenutnog korisnika.</summary>
public sealed record GetZvonoQuery : IRequest<ZvonoDto>;

/// <summary>Označi pročitano; bez ID-jeva — sva.</summary>
/// <param name="Ids">ID-jevi ili null.</param>
public sealed record OznaciProcitanoCommand(IReadOnlyList<long>? Ids) : IRequest<ZvonoDto>;

/// <summary>Javni VAPID ključ (null = push nije podešen).</summary>
public sealed record GetPushKljucQuery : IRequest<string?>;

/// <summary>Prijava uređaja za push.</summary>
/// <param name="Endpoint">Endpoint.</param>
/// <param name="P256dh">Ključ.</param>
/// <param name="Auth">Tajna.</param>
/// <param name="UredjajKlijenta">User-Agent.</param>
public sealed record PrijaviPushCommand(string Endpoint, string P256dh, string Auth, string? UredjajKlijenta) : IRequest;

/// <summary>Odjava uređaja.</summary>
/// <param name="Endpoint">Endpoint.</param>
public sealed record OdjaviPushCommand(string Endpoint) : IRequest;

/// <summary>Probno obaveštenje sebi (provera push-a na telefonu).</summary>
public sealed record PosaljiProbnoCommand : IRequest;

/// <summary>Primaoci po vrsti.</summary>
public sealed record GetPrimaociQuery : IRequest<PrimaociDto>;

/// <summary>Čuva primaoce za navedene vrste (vrste koje nisu navedene ostaju kako jesu).</summary>
/// <param name="Primaoci">Vrsta → korisnici.</param>
public sealed record PostaviPrimaoceCommand(Dictionary<string, List<string>> Primaoci) : IRequest<PrimaociDto>;

/// <summary>Push pretplata mora imati sva tri dela.</summary>
public sealed class PrijaviPushValidator : AbstractValidator<PrijaviPushCommand>
{
    /// <summary>Pravila.</summary>
    public PrijaviPushValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(1000).Must(e => e.StartsWith("https://", StringComparison.Ordinal))
            .WithMessage("Neispravna adresa push servisa.");
        RuleFor(x => x.P256dh).NotEmpty().MaximumLength(200).WithMessage("Neispravan ključ pretplate.");
        RuleFor(x => x.Auth).NotEmpty().MaximumLength(100).WithMessage("Neispravna tajna pretplate.");
    }
}

/// <summary>Samo poznate vrste.</summary>
public sealed class PostaviPrimaoceValidator : AbstractValidator<PostaviPrimaoceCommand>
{
    /// <summary>Pravila.</summary>
    public PostaviPrimaoceValidator() =>
        RuleFor(x => x.Primaoci).NotNull().Must(p => p.Keys.All(VrsteObavestenja.Podesive.Contains)).WithMessage("Nepoznata vrsta obaveštenja.");
}

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Zvono, push pretplate i primaoci.</summary>
public sealed class ObavestenjaHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, IPushSlanje push) :
    IRequestHandler<GetZvonoQuery, ZvonoDto>,
    IRequestHandler<OznaciProcitanoCommand, ZvonoDto>,
    IRequestHandler<GetPushKljucQuery, string?>,
    IRequestHandler<PrijaviPushCommand>,
    IRequestHandler<OdjaviPushCommand>,
    IRequestHandler<PosaljiProbnoCommand>,
    IRequestHandler<GetPrimaociQuery, PrimaociDto>,
    IRequestHandler<PostaviPrimaoceCommand, PrimaociDto>
{
    /// <summary>Nazivi i opisi vrsta sa primaocima (za ekran Podešavanja).</summary>
    public static readonly IReadOnlyDictionary<string, (string Naziv, string Opis)> Opisi = new Dictionary<string, (string, string)>
    {
        [VrsteObavestenja.NijeZapocet] = ("Pregled nije započet", "DP ili PZ nije započet do vremena upozorenja (dodeljeni inženjer ga dobija uvek)."),
        [VrsteObavestenja.Rezime] = ("Dnevni rezime", "Posle kraja radnog dana: predato, propušteno, čeka overu, hitne akcije, novi kvarovi."),
        [VrsteObavestenja.RokOvere] = ("Podsetnik za overu", "Pregled čeka overu duže od roka (sati radnih dana)."),
        [VrsteObavestenja.HitnaAkcija] = ("Hitna akcija", "Na pregledu je otvorena akcija sa hitnošću „Hitno“."),
        [VrsteObavestenja.RokAkcije] = ("Istekao rok akcije", "Jednom, kad akciji istekne rok."),
        [VrsteObavestenja.PonovljeniKvar] = ("Ponovljeni kvar", "Ista stavka neispravna onoliko puta u periodu koliko je podešeno."),
        [VrsteObavestenja.VanFunkcije] = ("Dugo van funkcije", "Uređaj van funkcije duže od granice; ponavlja se na isti broj dana."),
        [VrsteObavestenja.PredlogRasporeda] = ("Raspored za sledeću nedelju", "Četvrtkom, ako DP za sledeću nedelju nije potvrđen."),
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string Ja => currentUser.UserId ?? throw new ForbiddenException();

    /// <inheritdoc />
    public async Task<ZvonoDto> Handle(GetZvonoQuery req, CancellationToken ct)
    {
        var ja = Ja;
        var stavke = await db.Obavestenja.AsNoTracking().Where(o => o.KorisnikId == ja).OrderByDescending(o => o.KreiranoAt).ThenByDescending(o => o.Id).Take(50)
            .Select(o => new ObavestenjeDto(o.Id, o.Vrsta, o.Naslov, o.Tekst, o.Link, o.KreiranoAt, o.ProcitanoAt != null)).ToListAsync(ct);
        var neprocitanih = await db.Obavestenja.CountAsync(o => o.KorisnikId == ja && o.ProcitanoAt == null, ct);
        return new ZvonoDto(neprocitanih, await CekaOdlukuAsync(ja, ct), stavke);
    }

    /// <inheritdoc />
    public async Task<ZvonoDto> Handle(OznaciProcitanoCommand req, CancellationToken ct)
    {
        var ja = Ja;
        var upit = db.Obavestenja.Where(o => o.KorisnikId == ja && o.ProcitanoAt == null);
        if (req.Ids is { Count: > 0 } ids) upit = upit.Where(o => ids.Contains(o.Id));
        var sada = clock.UtcNow;
        foreach (var o in await upit.ToListAsync(ct)) o.ProcitanoAt = sada;
        await db.SaveChangesAsync(ct);
        return await Handle(new GetZvonoQuery(), ct);
    }

    /// <inheritdoc />
    public Task<string?> Handle(GetPushKljucQuery req, CancellationToken ct) => Task.FromResult(push.JavniKljuc);

    /// <inheritdoc />
    public async Task Handle(PrijaviPushCommand req, CancellationToken ct)
    {
        // Isti uređaj posle odjave i prijave drugog korisnika: pretplata prelazi na novog korisnika.
        var p = await db.PushPretplate.FirstOrDefaultAsync(x => x.Endpoint == req.Endpoint, ct);
        if (p is null) db.PushPretplate.Add(p = new PushPretplata { Endpoint = req.Endpoint });
        p.KorisnikId = Ja;
        p.P256dh = req.P256dh;
        p.Auth = req.Auth;
        p.UredjajKlijenta = req.UredjajKlijenta?.Length > 300 ? req.UredjajKlijenta[..300] : req.UredjajKlijenta;
        p.KreiranoAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task Handle(OdjaviPushCommand req, CancellationToken ct)
    {
        var ja = Ja;
        var p = await db.PushPretplate.FirstOrDefaultAsync(x => x.Endpoint == req.Endpoint && x.KorisnikId == ja, ct);
        if (p is null) return;
        db.PushPretplate.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task Handle(PosaljiProbnoCommand req, CancellationToken ct)
    {
        await Obavesti.DodajAsync(db, [Ja], VrsteObavestenja.Probno, "Probno obaveštenje", "Obaveštenja na ovom uređaju rade.", "/", null, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<PrimaociDto> Handle(GetPrimaociQuery req, CancellationToken ct)
    {
        var podeseni = await Obavesti.PodeseniAsync(db, ct);
        var vrste = new List<VrstaPrimalacaDto>();
        foreach (var v in VrsteObavestenja.Podesive)
            vrste.Add(new VrstaPrimalacaDto(v, Opisi[v].Naziv, Opisi[v].Opis, podeseni.ContainsKey(v), await Obavesti.PrimaociAsync(db, v, ct)));
        var korisnici = await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName)
            .Select(u => new PrimalacKorisnikDto(u.Id, u.FullName)).ToListAsync(ct);
        return new PrimaociDto(vrste, korisnici);
    }

    /// <inheritdoc />
    public async Task<PrimaociDto> Handle(PostaviPrimaoceCommand req, CancellationToken ct)
    {
        var postoje = (await db.Users.Select(u => u.Id).ToListAsync(ct)).ToHashSet();
        if (req.Primaoci.Values.SelectMany(x => x).Any(id => !postoje.Contains(id))) throw new BusinessRuleException("Izabrani korisnik ne postoji.");

        var podeseni = await Obavesti.PodeseniAsync(db, ct);
        foreach (var (vrsta, ids) in req.Primaoci) podeseni[vrsta] = ids.Distinct().ToList();
        var vrednost = JsonSerializer.Serialize(podeseni, Json);
        var red = await db.Podesavanja.FirstOrDefaultAsync(p => p.Kljuc == Obavesti.KljucPrimaoci, ct);
        if (red is null) db.Podesavanja.Add(new Podesavanje { Kljuc = Obavesti.KljucPrimaoci, Vrednost = vrednost });
        else red.Vrednost = vrednost;
        await db.SaveChangesAsync(ct);
        return await Handle(new GetPrimaociQuery(), ct);
    }

    /// <summary>Broj na zvoncu za „Čeka moju odluku“: iste stavke kao kartica na Početnoj, po pravima, samo za Supervizora i Direktora.</summary>
    private async Task<int> CekaOdlukuAsync(string ja, CancellationToken ct)
    {
        var prava = (await db.KorisnikPrava.Where(p => p.KorisnikId == ja).Select(p => p.Pravo).ToListAsync(ct)).ToHashSet();
        if (!currentUser.Roles.Contains(Roles.Supervizor) && !currentUser.Roles.Contains(Roles.Direktor) && !prava.Contains(Prava.SabloniNacrt)) return 0;
        var broj = 0;
        if (prava.Contains(Prava.Overa))
            broj += await db.Pregledi.CountAsync(p => p.Status == StatusPregleda.CekaOveru, ct) + await db.ZahteviDorade.CountAsync(z => z.Status == "zahtev", ct);
        if (prava.Contains(Prava.RasporedUredjivanje))
        {
            broj += await db.ZahteviPrebacivanja.CountAsync(z => z.Status == "zahtev", ct);
            var danas = clock.Today;
            broj += await db.Pregledi.CountAsync(p => p.Status == StatusPregleda.Planiran && p.PlaniraniDatum >= danas
                && db.Odsustva.Any(o => o.Status == "odobreno" && o.KorisnikId == p.DodeljenId && o.Od <= p.PlaniraniDatum && o.Do >= p.PlaniraniDatum), ct);
        }
        if (prava.Contains(Prava.OdsustvaOdobravanje)) broj += await db.Odsustva.CountAsync(o => o.Status == "zahtev", ct);
        if (prava.Contains(Prava.SabloniNacrt)) broj += await db.PredloziIzmena.CountAsync(p => p.Status == "zahtev", ct);
        return broj;
    }
}
