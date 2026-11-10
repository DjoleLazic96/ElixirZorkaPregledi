using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Aktivnosti;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Sabloni;

/// <summary>Izmene šablona (R2.4–R2.10): nacrt, razlike, objava sa datumom, zakazana verzija, predlozi, istorijat i uputstva.</summary>
public class IzmeneSablonaTests
{
    private sealed class Citac : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) =>
        [
            new(2, "DP1", "10", 1, "Transporter", "10-C-08", "Pregled rolni", "Okreće", "Ne okreće", ""),
            new(3, "DP1", "10", 2, "Transporter", "10-C-08", "Podmazivanje", "Ispravno", "Neispravno", ""),
            new(4, "DP1", "10", 3, "Elevator", "10-C-13", "Pregled rada zatezne sekcije", "Ispravno", "Neispravno", ""),
        ];
    }

    private sealed class FakeSkladiste : IFajlSkladiste
    {
        public Task<Fajl> SacuvajAsync(byte[] sadrzaj, string mime, CancellationToken ct) =>
            Task.FromResult(new Fajl { Putanja = Guid.NewGuid() + ".jpg", Mime = mime, Velicina = sadrzaj.Length, Sha256 = "x", KreiranoAt = DateTime.UtcNow });

        public Stream Otvori(Fajl fajl) => new MemoryStream([1]);
    }

    private static readonly FakeClock Pon = new(new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc));
    private static readonly DateOnly Danas = new(2026, 11, 9);

    private sealed record Okr(IdentityHarness H, string Dir, string Ana);

    private static async Task<Okr> PripremiAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var dir = await h.SeedUserAsync("dir", "Direktor2026!", Roles.Direktor);
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        h.Db.KorisnikPrava.AddRange(new KorisnikPravo { KorisnikId = dir.Id, Pravo = Prava.SabloniNacrt }, new KorisnikPravo { KorisnikId = dir.Id, Pravo = Prava.SabloniObjava });
        await h.Db.SaveChangesAsync();
        await new UvoziSabloneHandler(h.Db, new Citac(), new FakeCurrentUser(), new FakeClock()).Handle(new UvoziSabloneCommand(Stream.Null), default);
        return new Okr(h, dir.Id, ana.Id);
    }

    private static IzmeneSablonaHandler Sut(Okr o, string? ko = null, FakeClock? sat = null) =>
        new(o.H.Db, new FakeCurrentUser { UserId = ko ?? o.Dir, Roles = [Roles.Direktor] }, sat ?? Pon);

    private static int Stavka(NacrtDto n, string aktivnost) => n.Verzija.Sekcije.SelectMany(s => s.Uredjaji).SelectMany(u => u.Stavke).Single(s => s.Aktivnost == aktivnost).StavkaId;

    [Fact]
    public async Task Nacrt_je_kopija_sa_istim_kljucevima_i_bez_izmena_se_ne_objavljuje()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var n = await Sut(o).Handle(new OtvoriNacrtCommand("dp1"), default);
        n.Verzija.Broj.Should().Be(2);
        n.Verzija.Status.Should().Be(StatusVerzije.Nacrt);
        n.OsnovnaVerzija.Should().Be(1);
        n.Razlike.Should().BeEmpty();
        var kljucevi = await o.H.Db.SablonStavke.GroupBy(s => s.Kljuc).Select(g => g.Count()).ToListAsync();
        kljucevi.Should().OnlyContain(c => c == 2);   // svaka stavka postoji u v1 i u nacrtu sa istim ključem

        (await Sut(o).Handle(new OtvoriNacrtCommand("DP1"), default)).Verzija.Broj.Should().Be(2);   // drugi put vraća isti nacrt
        await Sut(o).Invoking(s => s.Handle(new ObjaviNacrtCommand("DP1", Danas), default)).Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Greske.Single() == "Nacrt nema izmena u odnosu na važeću verziju.");
    }

    [Fact]
    public async Task Izmene_u_nacrtu_daju_razlike_po_kljucu_a_pravila_registra_vaze()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var n = await Sut(o).Handle(new OtvoriNacrtCommand("DP1"), default);
        var sekcija = await o.H.Db.Sekcije.Where(s => s.Sifra == "10").Select(s => s.Id).SingleAsync();
        var elevator = await o.H.Db.Uredjaji.Where(u => u.Oznaka == "10-C-13").Select(u => u.Id).SingleAsync();
        var podmazivanje = await o.H.Db.Aktivnosti.Where(a => a.Naziv == "Podmazivanje").Select(a => a.Id).SingleAsync();

        n = await Sut(o).Handle(new DodajStavkuCommand("DP1", sekcija, elevator, podmazivanje, null), default);
        n = await Sut(o).Handle(new IzbaciStavkuCommand("DP1", Stavka(n, "Pregled rolni")), default);
        n = await Sut(o).Handle(new PomeriStavkuCommand("DP1", Stavka(n, "Pregled rada zatezne sekcije"), true), default);

        n.Razlike.Select(r => r.Vrsta).Should().Contain(["dodato", "izbaceno", "pomereno"]);
        n.Razlike.Single(r => r.Vrsta == "dodato").Uredjaj.Should().Be("Elevator 10-C-13");
        n.Razlike.Single(r => r.Vrsta == "izbaceno").Aktivnost.Should().Be("Pregled rolni");
        // Novo dodata stavka ide uz svoj uređaj, ne na kraj šablona.
        n.Verzija.Sekcije.Single().Uredjaji.Single(u => u.Oznaka == "10-C-13").Stavke.Should().HaveCount(2);

        await Sut(o).Invoking(s => s.Handle(new DodajStavkuCommand("DP1", sekcija, elevator, podmazivanje, null), default)).Should().ThrowAsync<BusinessRuleException>();
        (await o.H.Db.Uredjaji.SingleAsync(u => u.Id == elevator)).Status = StatusUredjaja.VanUpotrebe;
        await o.H.Db.SaveChangesAsync();
        var rolni = await o.H.Db.Aktivnosti.Where(a => a.Naziv == "Pregled rolni").Select(a => a.Id).SingleAsync();
        await Sut(o).Invoking(s => s.Handle(new DodajStavkuCommand("DP1", sekcija, elevator, rolni, null), default)).Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Greske.Single().Contains("van upotrebe"));

        // Ispravka teksta = druga aktivnost iz kataloga; ključ stavke ostaje.
        var podmTransportera = n.Verzija.Sekcije.Single().Uredjaji.First(u => u.Oznaka == "10-C-08").Stavke.Single();
        n = await Sut(o).Handle(new IzmeniStavkuCommand("DP1", podmTransportera.StavkaId, rolni, [1]), default);
        var izmenjena = n.Razlike.Single(r => r.Vrsta == "izmenjeno");
        izmenjena.Bilo.Should().Be("Podmazivanje");
        izmenjena.Aktivnost.Should().StartWith("Pregled rolni [");
        n.Verzija.Sekcije.Single().Uredjaji.First(u => u.Oznaka == "10-C-08").Stavke.Single().Kljuc.Should().Be(podmTransportera.Kljuc);
    }

    [Fact]
    public async Task Objava_od_danas_vazi_odmah_a_zapocet_pregled_ostaje_na_staroj_verziji()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var v1 = await o.H.Db.SablonVerzije.Where(v => v.Broj == 1).Select(v => v.Id).SingleAsync();
        o.H.Db.Pregledi.Add(new Pregled { Broj = "DP1-2026-0001", Tip = "DP1", PlaniraniDatum = Danas, PrvobitniDatum = Danas, DodeljenId = o.Ana, Status = StatusPregleda.URadu, SablonVerzijaId = v1 });
        await o.H.Db.SaveChangesAsync();

        var n = await Sut(o).Handle(new OtvoriNacrtCommand("DP1"), default);
        await Sut(o).Handle(new IzbaciStavkuCommand("DP1", Stavka(n, "Pregled rolni")), default);
        var r = await Sut(o).Handle(new ObjaviNacrtCommand("DP1", Danas), default);
        r.Should().Match<VerzijaKratkoDto>(x => x.Broj == 2 && x.Status == StatusVerzije.Objavljena && x.Objavio == "dir");

        var verzije = await Sut(o).Handle(new GetVerzijeQuery("DP1"), default);
        verzije.Select(v => (v.Broj, v.Status)).Should().Equal((2, StatusVerzije.Objavljena), (1, StatusVerzije.Zamenjena));
        (await o.H.Db.Pregledi.SingleAsync()).SablonVerzijaId.Should().Be(v1);
        (await new SabloniQueriesHandler(o.H.Db).Handle(new GetSablonVerzijaQuery("DP1"), default)).Broj.Should().Be(2);
    }

    [Fact]
    public async Task Objava_sa_buducim_datumom_je_zakazana_i_postaje_vazeca_svog_dana()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var n = await Sut(o).Handle(new OtvoriNacrtCommand("DP1"), default);
        await Sut(o).Handle(new IzbaciStavkuCommand("DP1", Stavka(n, "Pregled rolni")), default);
        await Sut(o).Invoking(s => s.Handle(new ObjaviNacrtCommand("DP1", Danas.AddDays(-1)), default)).Should().ThrowAsync<BusinessRuleException>();
        (await Sut(o).Handle(new ObjaviNacrtCommand("DP1", Danas.AddDays(7)), default)).Status.Should().Be(StatusVerzije.Zakazana);
        (await new SabloniQueriesHandler(o.H.Db).Handle(new GetSablonVerzijaQuery("DP1"), default)).Broj.Should().Be(1);

        // Novi nacrt kreće od zakazane (najnovije); druga objava čeka da zakazana počne da važi.
        n = await Sut(o).Handle(new OtvoriNacrtCommand("DP1"), default);
        n.OsnovnaVerzija.Should().Be(2);
        n.Zakazana.Should().Be(new ZakazanaDto(2, Danas.AddDays(7)));
        await Sut(o).Handle(new IzbaciStavkuCommand("DP1", Stavka(n, "Podmazivanje")), default);
        await Sut(o).Invoking(s => s.Handle(new ObjaviNacrtCommand("DP1", Danas.AddDays(8)), default)).Should().ThrowAsync<BusinessRuleException>();

        await new ZakazaneProvereHandler(o.H.Db, new FakeClock(new DateTime(2026, 11, 16, 6, 0, 0, DateTimeKind.Utc))).Handle(new PokreniProvereCommand(), default);
        (await new SabloniQueriesHandler(o.H.Db).Handle(new GetSablonVerzijaQuery("DP1"), default)).Broj.Should().Be(2);
        (await Sut(o).Handle(new GetIstorijatQuery("DP1", "vazi", null, null, null), default)).Single().Opis.Should().Be("Verzija 2 je postala važeća.");
    }

    [Fact]
    public async Task Predlog_ide_Direktoru_odobren_ulazi_u_nacrt_i_vezuje_se_za_objavljenu_verziju()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var stavka = await o.H.Db.SablonStavke.Where(s => s.Aktivnost!.Naziv == "Pregled rolni").Select(s => s.Id).SingleAsync();
        var p = await Sut(o, o.Ana).Handle(new PodnesiPredlogCommand("DP1", "izbacivanje", stavka, "Izbaciti pregled rolni", "Transporter nema rolne"), default);
        p.Stavka.Should().Be("Transporter 10-C-08 · Pregled rolni");
        (await o.H.Db.Obavestenja.SingleAsync()).Should().Match<Obavestenje>(x => x.KorisnikId == o.Dir && x.Vrsta == VrsteObavestenja.PredlogSablona);

        await Sut(o).Handle(new OdluciOPredloguCommand(p.Id, true, null), default);
        await Sut(o).Invoking(s => s.Handle(new OdluciOPredloguCommand(p.Id, false, null), default)).Should().ThrowAsync<BusinessRuleException>();
        (await o.H.Db.Obavestenja.OrderBy(x => x.Id).LastAsync()).Should().Match<Obavestenje>(x => x.KorisnikId == o.Ana && x.Vrsta == VrsteObavestenja.IshodZahteva);

        var n = await Sut(o).Handle(new OtvoriNacrtCommand("DP1"), default);
        n.Predlozi.Single().Opis.Should().Be("Izbaciti pregled rolni");
        await Sut(o).Handle(new IzbaciStavkuCommand("DP1", Stavka(n, "Pregled rolni")), default);
        await Sut(o).Handle(new ObjaviNacrtCommand("DP1", Danas), default);
        (await Sut(o).Handle(new GetPredloziQuery(null, "DP1"), default)).Single().Verzija.Should().Be(2);

        var istorijat = await Sut(o).Handle(new GetIstorijatQuery("DP1", null, null, Danas, Danas), default);
        istorijat.Select(x => x.Vrsta).Should().Contain(["predlog", "predlog_odobren", "nacrt", "nacrt_izmena", "objava", "vazi"]);
        istorijat.Should().NotContain(x => x.Vrsta == "prvi_uvoz");   // uvoz je bio pre perioda
    }

    [Fact]
    public void Predlog_trazi_razlog_a_izbacivanje_i_stavku()
    {
        var v = new PodnesiPredlogValidator();
        v.Validate(new PodnesiPredlogCommand("DP1", "ispravka", null, "Ispraviti naziv", "")).Errors.Single().ErrorMessage.Should().Be("Razlog je obavezan.");
        v.Validate(new PodnesiPredlogCommand("DP1", "izbacivanje", null, "x", "y")).Errors.Single().ErrorMessage.Should().Be("Izaberite stavku koja se izbacuje.");
        v.Validate(new PodnesiPredlogCommand("DP1", "dodavanje", null, "Dodati vagu", "Nova vaga")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Uputstvo_ima_tekst_i_najvise_tri_slike_i_ne_pravi_novu_verziju()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var id = await o.H.Db.Aktivnosti.Where(a => a.Naziv == "Pregled rolni").Select(a => a.Id).SingleAsync();
        var sut = new UputstvaHandler(o.H.Db, new FakeSkladiste(), new FakeCurrentUser { UserId = o.Dir }, Pon);

        (await sut.Handle(new GetUputstvoQuery(id), default)).Tekst.Should().BeNull();
        var u = await sut.Handle(new IzmeniUputstvoCommand(id, "  Proveriti da li se svaka rolna okreće.  "), default);
        u.Tekst.Should().Be("Proveriti da li se svaka rolna okreće.");
        u.Azurirao.Should().Be("dir");
        for (var i = 0; i < 3; i++) u = await sut.Handle(new DodajSlikuUputstvaCommand(id, [1, 2], "image/jpeg"), default);
        u.Slike.Should().HaveCount(3);
        await sut.Invoking(s => s.Handle(new DodajSlikuUputstvaCommand(id, [1], "image/jpeg"), default)).Should().ThrowAsync<BusinessRuleException>();
        (await sut.Handle(new UkloniSlikuUputstvaCommand(id, u.Slike[0]), default)).Slike.Should().HaveCount(2);

        (await new AktivnostiHandler(o.H.Db).Handle(new GetAktivnostiQuery(), default)).Single(a => a.Id == id).ImaUputstvo.Should().BeTrue();
        (await o.H.Db.SablonVerzije.CountAsync()).Should().Be(1);
    }
}
