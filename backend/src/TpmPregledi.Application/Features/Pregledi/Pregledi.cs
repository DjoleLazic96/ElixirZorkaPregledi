using System.Text.Json;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Common.Pagination;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Pregledi;

// ─── DTO ──────────────────────────────────────────────────────────────────

/// <summary>Red arhive.</summary>
/// <param name="Id">ID.</param>
/// <param name="Broj">Broj.</param>
/// <param name="Tip">Tip.</param>
/// <param name="PlaniraniDatum">Planirani dan.</param>
/// <param name="Status">Status.</param>
/// <param name="Dorada">Broj dorade.</param>
/// <param name="Inzenjer">Dodeljeni inženjer.</param>
/// <param name="PredatAt">Predaja.</param>
/// <param name="Overio">Ko je overio.</param>
/// <param name="Neispravnih">Broj neispravnih stavki.</param>
public sealed record PregledRedDto(int Id, string Broj, string Tip, DateOnly PlaniraniDatum, string Status, int Dorada, string Inzenjer,
    DateTime? PredatAt, string? Overio, int Neispravnih);

/// <summary>Stavka u detalju.</summary>
/// <param name="StavkaId">ID stavke.</param>
/// <param name="Aktivnost">Aktivnost.</param>
/// <param name="Odgovor">Tekst izabranog odgovora ili null.</param>
/// <param name="Neispravan">Da li je neispravan.</param>
/// <param name="Napomena">Napomena.</param>
/// <param name="Polja">Dodatna polja „naziv: vrednost“.</param>
/// <param name="Fotografije">ID-jevi slika.</param>
public sealed record DetaljStavkaDto(int StavkaId, string Aktivnost, string? Odgovor, bool Neispravan, string? Napomena, IReadOnlyList<string> Polja, IReadOnlyList<long> Fotografije);

/// <summary>Uređaj u detalju.</summary>
/// <param name="UredjajId">ID.</param>
/// <param name="Naziv">Naziv sa oznakom.</param>
/// <param name="VanFunkcije">Nije u funkciji na ovom pregledu.</param>
/// <param name="Stavke">Stavke.</param>
public sealed record DetaljUredjajDto(int UredjajId, string Naziv, bool VanFunkcije, IReadOnlyList<DetaljStavkaDto> Stavke);

/// <summary>Sekcija u detalju.</summary>
/// <param name="Sifra">Šifra.</param>
/// <param name="Uredjaji">Uređaji.</param>
public sealed record DetaljSekcijaDto(string Sifra, IReadOnlyList<DetaljUredjajDto> Uredjaji);

/// <summary>Događaj istorije.</summary>
/// <param name="Vrsta">Vrsta.</param>
/// <param name="Opis">Opis za prikaz.</param>
/// <param name="Korisnik">Ko (prazno = sistem).</param>
/// <param name="At">Kada.</param>
public sealed record DogadjajDto(string Vrsta, string Opis, string Korisnik, DateTime At);

/// <summary>Sporan unos za odluku.</summary>
/// <param name="Id">ID.</param>
/// <param name="Korisnik">Ko je poslao.</param>
/// <param name="Opis">Šta je poslato.</param>
/// <param name="Razlog">Zašto nije primenjeno.</param>
/// <param name="PrimljenoAt">Kada.</param>
/// <param name="Odluka">null = čeka; <c>primenjeno</c> / <c>odbaceno</c>.</param>
public sealed record SporanUnosDto(long Id, string Korisnik, string Opis, string Razlog, DateTime PrimljenoAt, string? Odluka);

/// <summary>Zahtev za doradu.</summary>
/// <param name="Id">ID.</param>
/// <param name="Podneo">Ko traži.</param>
/// <param name="Razlog">Razlog.</param>
/// <param name="Status">Status.</param>
/// <param name="KreiranoAt">Kada.</param>
public sealed record ZahtevDoradeDto(int Id, string Podneo, string Razlog, string Status, DateTime KreiranoAt);

/// <summary>Ceo pregled (R6.x, R5.3–R5.4).</summary>
public sealed record PregledDetaljDto(
    int Id, string Broj, string Tip, string Status, int Dorada, DateOnly PlaniraniDatum, DateOnly PrvobitniDatum,
    string DodeljenId, string Inzenjer, int? VerzijaBroj, DateTime? ZapocetAt, DateTime? PredatAt, int AktivnoSekundi, int Delova,
    string? Overio, DateTime? OverenoAt, bool OverioDirektor, string? PonistenRazlog,
    IReadOnlyList<string> Upozorenja, IReadOnlyList<DetaljSekcijaDto> Sekcije, IReadOnlyList<DogadjajDto> Istorija,
    IReadOnlyList<SporanUnosDto> SporniUnosi, IReadOnlyList<ZahtevDoradeDto> ZahteviDorade);

// ─── Zahtevi ──────────────────────────────────────────────────────────────

/// <summary>Filteri arhive (R8.13).</summary>
public sealed record PreglediFilteri(DateOnly? Od, DateOnly? Do, string? Tip, string? Status, string? InzenjerId, string? Pretraga, int Stranica = 1, int Velicina = 50);

/// <summary>Arhiva (svi vide sve, R1.7).</summary>
/// <param name="F">Filteri.</param>
public sealed record GetPreglediQuery(PreglediFilteri F) : IRequest<StranicaDto<PregledRedDto>>;

/// <summary>Detalj pregleda.</summary>
/// <param name="Id">ID.</param>
public sealed record GetPregledQuery(int Id) : IRequest<PregledDetaljDto>;

/// <summary>Overa (R6.2).</summary>
/// <param name="Id">ID.</param>
public sealed record OveriCommand(int Id) : IRequest<PregledDetaljDto>;

/// <summary>Vrati na doradu (R6.4).</summary>
/// <param name="Id">ID.</param>
/// <param name="InzenjerId">Ko radi doradu.</param>
/// <param name="Razlog">Razlog (obavezan).</param>
public sealed record VratiNaDoraduCommand(int Id, string InzenjerId, string Razlog) : IRequest<PregledDetaljDto>;

/// <summary>Inženjer traži doradu svog pregleda (R6.6).</summary>
/// <param name="Id">ID.</param>
/// <param name="Razlog">Razlog.</param>
public sealed record ZatraziDoraduCommand(int Id, string Razlog) : IRequest<PregledDetaljDto>;

/// <summary>Odluka o zahtevu za doradu.</summary>
/// <param name="Id">ID pregleda.</param>
/// <param name="Odobreno">Odobri ili odbij.</param>
public sealed record OdluciODoradiCommand(int Id, bool Odobreno) : IRequest<PregledDetaljDto>;

/// <summary>Poništavanje (R6.10).</summary>
/// <param name="Id">ID.</param>
/// <param name="Razlog">Razlog.</param>
public sealed record PonistiCommand(int Id, string Razlog) : IRequest<PregledDetaljDto>;

/// <summary>Odluka o spornom unosu (R6.8).</summary>
/// <param name="PregledId">Pregled.</param>
/// <param name="UnosId">Unos.</param>
/// <param name="Primeni">Primeni ili odbaci.</param>
public sealed record OdluciOSpornomUnosuCommand(int PregledId, long UnosId, bool Primeni) : IRequest<PregledDetaljDto>;

/// <summary>Razlog je obavezan.</summary>
public sealed class VratiNaDoraduValidator : AbstractValidator<VratiNaDoraduCommand>
{
    /// <summary>Pravila.</summary>
    public VratiNaDoraduValidator()
    {
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(500).WithMessage("Razlog može imati najviše 500 karaktera.");
        RuleFor(x => x.InzenjerId).NotEmpty().WithMessage("Izaberite inženjera.");
    }
}

/// <summary>Razlog je obavezan.</summary>
public sealed class ZatraziDoraduValidator : AbstractValidator<ZatraziDoraduCommand>
{
    /// <summary>Pravila.</summary>
    public ZatraziDoraduValidator() =>
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(500).WithMessage("Razlog može imati najviše 500 karaktera.");
}

/// <summary>Razlog je obavezan.</summary>
public sealed class PonistiValidator : AbstractValidator<PonistiCommand>
{
    /// <summary>Pravila.</summary>
    public PonistiValidator() =>
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Razlog je obavezan.").MaximumLength(500).WithMessage("Razlog može imati najviše 500 karaktera.");
}

// ─── Handler ──────────────────────────────────────────────────────────────

/// <summary>Arhiva, detalj, overa, dorada, poništavanje, sporni unosi.</summary>
public sealed class PreglediHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetPreglediQuery, StranicaDto<PregledRedDto>>,
    IRequestHandler<GetPregledQuery, PregledDetaljDto>,
    IRequestHandler<OveriCommand, PregledDetaljDto>,
    IRequestHandler<VratiNaDoraduCommand, PregledDetaljDto>,
    IRequestHandler<ZatraziDoraduCommand, PregledDetaljDto>,
    IRequestHandler<OdluciODoradiCommand, PregledDetaljDto>,
    IRequestHandler<PonistiCommand, PregledDetaljDto>,
    IRequestHandler<OdluciOSpornomUnosuCommand, PregledDetaljDto>
{
    /// <summary>Poruka kad Supervizor dira overu Direktora (R6.5).</summary>
    public const string PorukaOveraDirektora = "Pregled je overio Direktor; samo Direktor može da poništi tu overu.";

    private bool JeDirektor => currentUser.Roles.Contains(Roles.Direktor);

    /// <inheritdoc />
    public async Task<StranicaDto<PregledRedDto>> Handle(GetPreglediQuery req, CancellationToken ct)
    {
        var f = req.F;
        var upit = db.Pregledi.AsNoTracking().AsQueryable();
        if (f.Od is { } od) upit = upit.Where(p => p.PlaniraniDatum >= od);
        if (f.Do is { } d) upit = upit.Where(p => p.PlaniraniDatum <= d);
        if (!string.IsNullOrWhiteSpace(f.Tip)) upit = upit.Where(p => p.Tip == f.Tip);
        if (!string.IsNullOrWhiteSpace(f.Status)) upit = upit.Where(p => p.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.InzenjerId)) upit = upit.Where(p => p.DodeljenId == f.InzenjerId);
        if (!string.IsNullOrWhiteSpace(f.Pretraga)) upit = upit.Where(p => p.Broj.ToLower().Contains(f.Pretraga.Trim().ToLower()));

        var ukupno = await upit.CountAsync(ct);
        var velicina = Math.Clamp(f.Velicina, 1, 500);
        var strana = await upit.OrderByDescending(p => p.PlaniraniDatum).ThenByDescending(p => p.Id)
            .Skip((Math.Max(f.Stranica, 1) - 1) * velicina).Take(velicina)
            .Select(p => new
            {
                p.Id, p.Broj, p.Tip, p.PlaniraniDatum, p.Status, p.Dorada, p.DodeljenId, p.PredatAt, p.OverioId,
                Neispravnih = db.Odgovori.Count(o => o.PregledId == p.Id && o.Dorada == p.Dorada && o.Vrednost == 1),
            }).ToListAsync(ct);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        var stavke = strana.Select(p => new PregledRedDto(p.Id, p.Broj, p.Tip, p.PlaniraniDatum, p.Status, p.Dorada, k.Ime(p.DodeljenId),
            p.PredatAt, p.OverioId is null ? null : k.Ime(p.OverioId), p.Neispravnih)).ToList();
        return new StranicaDto<PregledRedDto>(stavke, ukupno, Math.Max(f.Stranica, 1), velicina);
    }

    /// <inheritdoc />
    public async Task<PregledDetaljDto> Handle(GetPregledQuery req, CancellationToken ct)
    {
        var p = await db.Pregledi.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Pregled ne postoji.");
        var k = await RasporedKontekst.UcitajAsync(db, ct);

        var sekcije = new List<DetaljSekcijaDto>();
        int? verzijaBroj = null;
        if (p.SablonVerzijaId is { } vid)
        {
            verzijaBroj = await db.SablonVerzije.Where(v => v.Id == vid).Select(v => (int?)v.Broj).FirstOrDefaultAsync(ct);
            var stavke = await db.SablonStavke.AsNoTracking().Where(s => s.VerzijaId == vid).OrderBy(s => s.Redosled)
                .Select(s => new
                {
                    s.Id, s.UredjajId, Sekcija = db.Sekcije.Where(x => x.Id == s.SekcijaId).Select(x => x.Sifra).First(),
                    s.Uredjaj!.Naziv, s.Uredjaj.Oznaka, Aktivnost = s.Aktivnost!.Naziv, s.Aktivnost.OdgovorIspravan, s.Aktivnost.OdgovorNeispravan,
                    Polja = s.Polja.Select(x => new { x.DodatnoPolje!.Id, x.DodatnoPolje.Naziv, x.DodatnoPolje.Jedinica }).ToList(),
                }).ToListAsync(ct);
            var odgovori = await db.Odgovori.AsNoTracking().Where(o => o.PregledId == p.Id && o.Dorada == p.Dorada).ToDictionaryAsync(o => o.StavkaId, ct);
            var van = (await db.UredjajiVanFunkcije.AsNoTracking().Where(v => v.PregledId == p.Id).Select(v => v.UredjajId).ToListAsync(ct)).ToHashSet();
            var slike = (await db.Fotografije.AsNoTracking().Where(f => f.PregledId == p.Id).Select(f => new { f.StavkaId, f.Id }).ToListAsync(ct))
                .ToLookup(f => f.StavkaId, f => f.Id);

            sekcije = stavke.GroupBy(s => s.Sekcija).Select(gs => new DetaljSekcijaDto(gs.Key, gs.GroupBy(s => s.UredjajId).Select(gu =>
                new DetaljUredjajDto(gu.Key, $"{gu.First().Naziv} {gu.First().Oznaka}".Trim(), van.Contains(gu.Key), gu.Select(s =>
                {
                    odgovori.TryGetValue(s.Id, out var o);
                    var polja = new List<string>();
                    if (o is not null)
                    {
                        using var doc = JsonDocument.Parse(o.Polja);
                        foreach (var pd in s.Polja)
                            if (doc.RootElement.TryGetProperty(pd.Id.ToString(), out var v))
                                polja.Add($"{pd.Naziv}: {(v.ValueKind == JsonValueKind.Array ? string.Join(" / ", v.EnumerateArray().Select(x => x.ToString())) : v.ToString())}{(pd.Jedinica is null ? "" : " " + pd.Jedinica)}");
                    }
                    return new DetaljStavkaDto(s.Id, s.Aktivnost, o is null ? null : o.Vrednost == 0 ? s.OdgovorIspravan : s.OdgovorNeispravan,
                        o?.Vrednost == 1, o?.Napomena, polja, slike[s.Id].ToList());
                }).ToList())).ToList())).ToList();
        }

        var dogadjaji = await db.PregledDogadjaji.AsNoTracking().Where(d => d.PregledId == p.Id).OrderBy(d => d.At).ToListAsync(ct);
        var upozorenja = new List<string>();
        var sat = dogadjaji.Where(d => d.Vrsta == "odstupanje_sata").ToList();
        if (sat.Count > 0) upozorenja.Add($"Sat telefona je odstupao od servera ({sat.Count} puta).");
        var sve = dogadjaji.Where(d => d.Vrsta == "sve_ispravno").Select(d => JsonDocument.Parse(d.Podaci).RootElement).ToList();
        if (sve.Count > 0)
            upozorenja.Add($"„Sve neoznačeno je ispravno“ upotrebljeno {sve.Count} puta (sekcije {string.Join(", ", sve.Select(x => x.GetProperty("sekcija").GetString()).Distinct())}).");
        if (p.Delova > 1) upozorenja.Add($"Pregled je rađen u {p.Delova} dela.");

        var sporni = await db.SporniUnosi.AsNoTracking().Where(s => s.PregledId == p.Id).OrderBy(s => s.PrimljenoAt).ToListAsync(ct);
        var zahtevi = await db.ZahteviDorade.AsNoTracking().Where(z => z.PregledId == p.Id).OrderBy(z => z.KreiranoAt).ToListAsync(ct);

        return new PregledDetaljDto(p.Id, p.Broj, p.Tip, p.Status, p.Dorada, p.PlaniraniDatum, p.PrvobitniDatum, p.DodeljenId, k.Ime(p.DodeljenId),
            verzijaBroj, p.ZapocetAt, p.PredatAt, p.AktivnoSekundi, p.Delova, p.OverioId is null ? null : k.Ime(p.OverioId), p.OverenoAt,
            p.OverioDirektor, p.PonistenRazlog, upozorenja, sekcije,
            dogadjaji.Select(d => new DogadjajDto(d.Vrsta, OpisDogadjaja(d), k.Ime(d.KorisnikId), d.At)).ToList(),
            sporni.Select(s => new SporanUnosDto(s.Id, k.Ime(s.KorisnikId), OpisOperacije(s.Operacija), s.Razlog, s.PrimljenoAt, s.Odluka)).ToList(),
            zahtevi.Select(z => new ZahtevDoradeDto(z.Id, k.Ime(z.PodneoId), z.Razlog, z.Status, z.KreiranoAt)).ToList());
    }

    /// <inheritdoc />
    public async Task<PregledDetaljDto> Handle(OveriCommand req, CancellationToken ct)
    {
        var p = await UcitajAsync(req.Id, ct);
        if (p.Status != StatusPregleda.CekaOveru) throw new BusinessRuleException("Overava se samo pregled koji čeka overu.");
        p.Status = StatusPregleda.Overen;
        p.OverioId = currentUser.UserId;
        p.OverenoAt = clock.UtcNow;
        p.OverioDirektor = JeDirektor;
        PregledIstorija.Dodaj(db, p, "overa", new { }, currentUser.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await Handle(new GetPregledQuery(p.Id), ct);
    }

    /// <inheritdoc />
    public async Task<PregledDetaljDto> Handle(VratiNaDoraduCommand req, CancellationToken ct)
    {
        var p = await UcitajAsync(req.Id, ct);
        if (p.Status is not (StatusPregleda.CekaOveru or StatusPregleda.Overen))
            throw new BusinessRuleException("Na doradu se vraća samo predat ili overen pregled.");
        ZastitiOveruDirektora(p);
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        k.ProveriInzenjera(req.InzenjerId);
        await VratiAsync(p, req.InzenjerId, req.Razlog.Trim(), "vracen_na_doradu", ct);
        await db.SaveChangesAsync(ct);
        return await Handle(new GetPregledQuery(p.Id), ct);
    }

    /// <inheritdoc />
    public async Task<PregledDetaljDto> Handle(ZatraziDoraduCommand req, CancellationToken ct)
    {
        var p = await UcitajAsync(req.Id, ct);
        if (p.DodeljenId != currentUser.UserId) throw new ForbiddenException("Doradu traži samo inženjer koji je radio pregled.");
        if (p.Status is not (StatusPregleda.CekaOveru or StatusPregleda.Overen))
            throw new BusinessRuleException("Dorada se traži za predat ili overen pregled; dok pregled nije poslat na server ne menja se.");
        if (await db.ZahteviDorade.AnyAsync(z => z.PregledId == p.Id && z.Status == "zahtev", ct))
            throw new BusinessRuleException("Zahtev za doradu već čeka odluku.");
        db.ZahteviDorade.Add(new ZahtevDorade { PregledId = p.Id, PodneoId = currentUser.UserId!, Razlog = req.Razlog.Trim(), KreiranoAt = clock.UtcNow });
        PregledIstorija.Dodaj(db, p, "zahtev_za_doradu", new { razlog = req.Razlog.Trim() }, currentUser.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await Handle(new GetPregledQuery(p.Id), ct);
    }

    /// <inheritdoc />
    public async Task<PregledDetaljDto> Handle(OdluciODoradiCommand req, CancellationToken ct)
    {
        var p = await UcitajAsync(req.Id, ct);
        var z = await db.ZahteviDorade.FirstOrDefaultAsync(x => x.PregledId == p.Id && x.Status == "zahtev", ct)
                ?? throw new BusinessRuleException("Nema zahteva za doradu koji čeka odluku.");
        // Posle overe odlučuje onaj ko je overio ili Direktor (R6.6).
        if (p.Status == StatusPregleda.Overen && p.OverioId != currentUser.UserId && !JeDirektor)
            throw new BusinessRuleException("O doradi overenog pregleda odlučuje onaj ko ga je overio ili Direktor.");
        z.Status = req.Odobreno ? "odobreno" : "odbijeno";
        z.OdlucioId = currentUser.UserId;
        z.OdlucenoAt = clock.UtcNow;
        if (req.Odobreno) await VratiAsync(p, p.DodeljenId, z.Razlog, "dorada_odobrena", ct);
        else
        {
            PregledIstorija.Dodaj(db, p, "dorada_odbijena", new { razlog = z.Razlog }, currentUser.UserId, clock.UtcNow);
            await Obavesti.DodajAsync(db, [z.PodneoId], VrsteObavestenja.IshodZahteva, $"Dorada {p.Broj} odbijena",
                $"Zahtev za doradu {p.Broj} je odbijen; pregled ostaje kakav je predat.", $"/pregledi/{p.Id}", null, clock.UtcNow, ct);
        }
        await db.SaveChangesAsync(ct);
        return await Handle(new GetPregledQuery(p.Id), ct);
    }

    /// <inheritdoc />
    public async Task<PregledDetaljDto> Handle(PonistiCommand req, CancellationToken ct)
    {
        var p = await UcitajAsync(req.Id, ct);
        if (p.Status is not (StatusPregleda.URadu or StatusPregleda.CekaOveru or StatusPregleda.Overen or StatusPregleda.VracenNaDoradu))
            throw new BusinessRuleException("Poništava se pregled koji je u radu, čeka overu, overen ili vraćen na doradu.");
        ZastitiOveruDirektora(p);
        p.Status = StatusPregleda.Ponisten;
        p.PonistenRazlog = req.Razlog.Trim();
        p.PonistioId = currentUser.UserId;
        PregledIstorija.Dodaj(db, p, "ponistavanje", new { razlog = p.PonistenRazlog }, currentUser.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await Handle(new GetPregledQuery(p.Id), ct);
    }

    /// <inheritdoc />
    public async Task<PregledDetaljDto> Handle(OdluciOSpornomUnosuCommand req, CancellationToken ct)
    {
        var p = await UcitajAsync(req.PregledId, ct);
        var s = await db.SporniUnosi.FirstOrDefaultAsync(x => x.Id == req.UnosId && x.PregledId == p.Id, ct)
                ?? throw new NotFoundException("Sporan unos ne postoji.");
        if (s.Odluka is not null) throw new BusinessRuleException("O ovom unosu je već odlučeno.");

        if (req.Primeni)
        {
            using var doc = JsonDocument.Parse(s.Operacija);
            var op = doc.RootElement;
            if (op.GetProperty("vrsta").GetString() != "odgovor")
                throw new BusinessRuleException("Primenjuje se samo sporan odgovor; ostale radnje se odbacuju.");
            if (p.Status is StatusPregleda.Ponisten) throw new BusinessRuleException("Pregled je poništen.");
            var podaci = op.GetProperty("podaci");
            var stavkaId = podaci.GetProperty("stavkaId").GetInt32();
            var vrednost = (short)podaci.GetProperty("vrednost").GetInt32();
            var napomena = podaci.TryGetProperty("napomena", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            var o = await db.Odgovori.FirstOrDefaultAsync(x => x.PregledId == p.Id && x.StavkaId == stavkaId && x.Dorada == p.Dorada, ct);
            if (o is null)
                db.Odgovori.Add(new Odgovor
                {
                    PregledId = p.Id, StavkaId = stavkaId, Vrednost = vrednost, Napomena = napomena, KorisnikId = s.KorisnikId,
                    KlijentAt = op.GetProperty("klijentAt").GetDateTime().ToUniversalTime(), ServerAt = clock.UtcNow, OpId = Guid.NewGuid(), Dorada = p.Dorada,
                });
            else (o.Vrednost, o.Napomena, o.KorisnikId, o.ServerAt) = (vrednost, napomena, s.KorisnikId, clock.UtcNow);
        }
        s.Odluka = req.Primeni ? "primenjeno" : "odbaceno";
        PregledIstorija.Dodaj(db, p, "sporan_unos", new { unosId = s.Id, odluka = s.Odluka }, currentUser.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await Handle(new GetPregledQuery(p.Id), ct);
    }

    // ─── Pomoćno ──────────────────────────────────────────────────────────

    private async Task<Pregled> UcitajAsync(int id, CancellationToken ct) =>
        await db.Pregledi.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Pregled ne postoji.");

    private void ZastitiOveruDirektora(Pregled p)
    {
        if (p.Status == StatusPregleda.Overen && p.OverioDirektor && !JeDirektor)
            throw new BusinessRuleException(PorukaOveraDirektora);
    }

    /// <summary>Nova dorada: broj se ne menja, odgovori prethodne dorade se kopiraju (prethodna ostaje u istoriji, R6.4).</summary>
    private async Task VratiAsync(Pregled p, string inzenjerId, string razlog, string vrsta, CancellationToken ct)
    {
        var stari = await db.Odgovori.AsNoTracking().Where(o => o.PregledId == p.Id && o.Dorada == p.Dorada).ToListAsync(ct);
        p.Dorada++;
        foreach (var o in stari)
            db.Odgovori.Add(new Odgovor
            {
                PregledId = p.Id, StavkaId = o.StavkaId, Vrednost = o.Vrednost, Napomena = o.Napomena, Polja = o.Polja, KorisnikId = o.KorisnikId,
                KlijentAt = o.KlijentAt, ServerAt = o.ServerAt, OpId = Guid.NewGuid(), Dorada = p.Dorada,
            });
        var bio = p.OverioId;
        p.Status = StatusPregleda.VracenNaDoradu;
        p.DodeljenId = inzenjerId;
        p.UredjajKlijenta = null;   // doradu može da radi drugi inženjer na drugom telefonu
        p.OverioId = null;
        p.OverenoAt = null;
        p.OverioDirektor = false;
        PregledIstorija.Dodaj(db, p, vrsta, new { razlog, dorada = p.Dorada, overio = bio }, currentUser.UserId, clock.UtcNow);
        await Obavesti.DodajAsync(db, [inzenjerId], VrsteObavestenja.Dorada, $"{p.Broj} vraćen na doradu",
            $"{(vrsta == "dorada_odobrena" ? "Zahtev za doradu je odobren" : "Pregled je vraćen na doradu")}: {razlog}", $"/pregled/{p.Id}/popuni", null, clock.UtcNow, ct);
    }

    /// <summary>Kratak opis događaja za istoriju (R6.7).</summary>
    private static string OpisDogadjaja(PregledDogadjaj d)
    {
        using var doc = JsonDocument.Parse(d.Podaci);
        var r = doc.RootElement;
        string S(string k) => r.TryGetProperty(k, out var v) ? v.ToString() : string.Empty;
        return d.Vrsta switch
        {
            "dodela" => $"Dodeljen: {S("inzenjer")}",
            "prenos" => "Prenet na sledeći radni dan",
            "promena_pz" => $"Promena PZ-a (po redu je bio {S("sledeciPoRedu")}). {S("napomena")}".Trim(),
            "izmena_rasporeda" => "Izmena rasporeda",
            "prebacivanje" => $"Prebačen: {S("stari")} → {S("novi")}",
            "ucesnik" => $"Drugi inženjer: {S("inzenjer")}",
            "pocetak" => "Započet",
            "izmena_odgovora" => $"Izmenjen odgovor: {S("uredjaj")} — {S("aktivnost")}",
            "sve_ispravno" => $"„Sve neoznačeno je ispravno“ u sekciji {S("sekcija")} ({S("stavki")} stavki)",
            "predaja" => "Predat",
            "overa" => "Overen",
            "vracen_na_doradu" => $"Vraćen na doradu {S("dorada")}: {S("razlog")}",
            "zahtev_za_doradu" => $"Zatražena dorada: {S("razlog")}",
            "dorada_odobrena" => $"Odobrena dorada {S("dorada")}: {S("razlog")}",
            "dorada_odbijena" => "Odbijen zahtev za doradu",
            "ponistavanje" => $"Poništen: {S("razlog")}",
            "odstupanje_sata" => $"Sat telefona odstupa {S("sekundi")} s",
            "sporan_unos" => $"Sporan unos: {S("odluka")}",
            _ => d.Vrsta,
        };
    }

    private static string OpisOperacije(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var vrsta = r.TryGetProperty("vrsta", out var v) ? v.GetString() : "?";
        return vrsta switch
        {
            "odgovor" => $"Odgovor na stavku {r.GetProperty("podaci").GetProperty("stavkaId")}: {(r.GetProperty("podaci").GetProperty("vrednost").GetInt32() == 0 ? "ispravan" : "neispravan")}",
            "zapocni" => "Početak pregleda",
            "predaja" => "Predaja",
            _ => vrsta ?? "?",
        };
    }
}

/// <summary>Izveštaji (implementacija u Infrastructure: QuestPDF, OpenXML, ClosedXML).</summary>
public interface IIzvestajPregleda
{
    /// <summary>Ceo pregled sa rezimeom problematičnih stavki (R6.1).</summary>
    byte[] Pdf(PregledDetaljDto p);

    /// <summary>Isti sadržaj kao PDF, u Word-u (R11.2).</summary>
    byte[] Docx(PregledDetaljDto p);

    /// <summary>Arhiva po filterima (R8.13: izvoz uzima filtrirano).</summary>
    byte[] Excel(IReadOnlyList<PregledRedDto> redovi);

    /// <summary>Bilo koja lista u Excel (akcije, neispravnosti — R11.2): zaglavlje i redovi.</summary>
    byte[] ExcelTabela(string list, IReadOnlyList<string> zaglavlje, IEnumerable<IReadOnlyList<object?>> redovi);
}
