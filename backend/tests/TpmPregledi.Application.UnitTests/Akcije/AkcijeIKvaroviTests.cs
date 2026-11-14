using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Akcije;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Pregledi;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Akcije;

/// <summary>Akcije (R7.x, R4.7) i kvarovi (R8.7, R8.15, R7.15) kroz popunjavanje dva uzastopna pregleda.</summary>
public class AkcijeIKvaroviTests
{
    private sealed class Citac : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) =>
        [
            new(2, "DP1", "10", 1, "Transporter", "10-C-08", "Pregled rolni", "Okreće", "Ne okreće", ""),
            new(3, "DP1", "10", 2, "Elevator", "10-C-13", "Pregled rada zatezne sekcije", "Ispravno", "Neispravno", ""),
        ];
    }

    private sealed record Okr(IdentityHarness H, string Ana, FakeBrojac Brojac, int[] Stavke);

    private static async Task<Okr> PripremiAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        await new UvoziSabloneHandler(h.Db, new Citac(), new FakeCurrentUser(), new FakeClock()).Handle(new UvoziSabloneCommand(Stream.Null), default);
        var stavke = await h.Db.SablonStavke.OrderBy(s => s.Redosled).Select(s => s.Id).ToArrayAsync();
        return new Okr(h, ana.Id, new FakeBrojac(), stavke);
    }

    private static async Task<int> NoviPregledAsync(Okr o, string broj, DateOnly dan)
    {
        var p = new Pregled { Broj = broj, Tip = "DP1", PlaniraniDatum = dan, PrvobitniDatum = dan, DodeljenId = o.Ana };
        o.H.Db.Pregledi.Add(p);
        await o.H.Db.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<SinhronizacijaRezultat> PosaljiAsync(Okr o, int pregledId, params (string Vrsta, object Podaci)[] ops)
    {
        var sut = new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(FakeClock.PosleRasporeda), o.Brojac);
        var jo = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return await sut.Handle(new SinhronizacijaCommand(null, null,
            ops.Select(x => new OperacijaInput(Guid.NewGuid(), x.Vrsta, pregledId, new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc), JsonSerializer.SerializeToElement(x.Podaci, jo))).ToList()), default);
    }

    private static AkcijeIKvaroviHandler Ekran(Okr o) => new(o.H.Db, new FakeCurrentUser { UserId = o.Ana, Roles = [Roles.Supervizor] }, new FakeClock(FakeClock.PosleRasporeda.AddHours(1)));

    [Fact]
    public async Task Kvar_menja_samo_najnoviji_predat_pregled_za_stavku()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        // Noviji (13.11.) je predat prvi i kaže „ispravno“; stariji (11.11.) stiže kasnije sa „neispravno“ → kvar se ne otvara.
        var novi = await NoviPregledAsync(o, "DP1-2026-0002", new DateOnly(2026, 11, 13));
        await PosaljiAsync(o, novi, ("zapocni", new { }), ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }), ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }), ("predaja", new { }));
        var stari = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 11));
        (await PosaljiAsync(o, stari, ("zapocni", new { }), ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "ne okreće" }),
            ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }), ("predaja", new { }))).Odbijene.Should().BeEmpty();
        (await o.H.Db.Kvarovi.CountAsync()).Should().Be(0, "noviji pregled je već rekao da je stavka ispravna");

        // Obrnuto: noviji (16.11.) otvara kvar; stariji (15.11.) sa „ispravno“ predat kasnije ga ne zatvara.
        var p16 = await NoviPregledAsync(o, "DP1-2026-0004", new DateOnly(2026, 11, 16));
        await PosaljiAsync(o, p16, ("zapocni", new { }), ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "ne okreće" }), ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }), ("predaja", new { }));
        var p15 = await NoviPregledAsync(o, "DP1-2026-0003", new DateOnly(2026, 11, 15));
        await PosaljiAsync(o, p15, ("zapocni", new { }), ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }), ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }), ("predaja", new { }));
        var kvar = await o.H.Db.Kvarovi.SingleAsync();
        kvar.Zatvoren.Should().BeNull();
        kvar.Prijava.Should().Be(1);
    }

    [Fact]
    public async Task Rok_akcije_posle_ponoci_racuna_se_po_beogradskom_danu()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var p = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 12));
        await PosaljiAsync(o, p, ("zapocni", new { }));
        var jo = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        // 12.11. u 23:30 UTC je 13.11. u 00:30 u Beogradu → „hitno“ ima rok 14.11. (zamerka 12).
        await new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(FakeClock.PosleRasporeda), o.Brojac).Handle(new SinhronizacijaCommand(null, null,
            [new OperacijaInput(Guid.NewGuid(), "akcija", p, new DateTime(2026, 11, 12, 23, 30, 0, DateTimeKind.Utc),
                JsonSerializer.SerializeToElement(new { stavkaId = o.Stavke[0], vrsta = "ostalo", hitnost = "hitno", opis = (string?)null }, jo))]), default);
        (await o.H.Db.Akcije.SingleAsync()).Rok.Should().Be(new DateOnly(2026, 11, 14));
    }

    [Fact]
    public void Rok_iz_hitnosti()
    {
        var d = new DateOnly(2026, 11, 9);
        AkcijaPravila.Rok("hitno", d, null).Should().Be(d.AddDays(1));
        AkcijaPravila.Rok("7_dana", d, null).Should().Be(d.AddDays(7));
        AkcijaPravila.Rok("sledeci_pz", d, new DateOnly(2026, 12, 1)).Should().Be(new DateOnly(2026, 12, 1));
        AkcijaPravila.Rok("pratiti", d, null).Should().BeNull();
    }

    [Fact]
    public async Task Neispravno_otvara_kvar_i_akciju_a_sledeci_pregled_zatvara_akciju_pop_upom_i_kvar_ispravnim()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var p1 = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9));
        var r = await PosaljiAsync(o, p1,
            ("zapocni", new { }),
            ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "7 rolni" }),
            ("akcija", new { stavkaId = o.Stavke[0], vrsta = "zameniti", hitnost = "7_dana", opis = "Zameniti rolne" }),
            ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }),
            ("predaja", new { }));
        r.Odbijene.Should().BeEmpty();
        // Slika uz neispravnu stavku (zamerka 71): vidi se na akciji i uz prijavu kvara.
        var fajl = new Fajl { Putanja = "x.jpg", Mime = "image/jpeg", Sha256 = "x", KreiranoAt = DateTime.UtcNow };
        o.H.Db.Fajlovi.Add(fajl);
        await o.H.Db.SaveChangesAsync();
        var slika = new Fotografija { PregledId = p1, StavkaId = o.Stavke[0], FajlId = fajl.Id, KorisnikId = o.Ana, KlijentId = Guid.NewGuid(), KreiranoAt = DateTime.UtcNow };
        o.H.Db.Fotografije.Add(slika);
        await o.H.Db.SaveChangesAsync();

        var akcija = (await Ekran(o).Handle(new GetAkcijeQuery(new AkcijeFilteri()), default)).Single();
        (await Ekran(o).Handle(new GetAkcijaQuery(akcija.Id), default)).Fotografije.Should().Equal(slika.Id);
        akcija.Broj.Should().Be("A-2026-0001");
        akcija.Rok.Should().Be(new DateOnly(2026, 11, 16));
        akcija.KvarBroj.Should().Be("K-2026-0001");
        akcija.KvarOtvoren.Should().BeTrue();

        // Sledeći pregled: uređaj nosi otvorenu akciju za pop-up.
        var p2 = await NoviPregledAsync(o, "DP1-2026-0002", new DateOnly(2026, 11, 11));
        await PosaljiAsync(o, p2, ("zapocni", new { }));
        var dto = await new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(FakeClock.PosleRasporeda), o.Brojac).Handle(new GetPopunjavanjeQuery(p2), default);
        dto.Sekcije[0].Uredjaji[0].OtvoreneAkcije.Single().Broj.Should().Be("A-2026-0001");

        r = await PosaljiAsync(o, p2,
            ("akcija_odgovor", new { akcijaId = akcija.Id, uradjeno = true }),
            ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }),
            ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }),
            ("predaja", new { }));
        r.Odbijene.Should().BeEmpty();

        var detalj = await Ekran(o).Handle(new GetAkcijaQuery(akcija.Id), default);
        detalj.Akcija.Status.Should().Be("zatvorena");
        detalj.Istorija.Select(x => x.Vrsta).Should().Equal("otvorena", "zatvorena");
        var kvar = (await Ekran(o).Handle(new GetKvaroviQuery(new KvaroviFilteri(Otvoreno: null)), default)).Single();
        kvar.Zatvoren.Should().Be(new DateOnly(2026, 11, 11));
        kvar.TrajanjeDana.Should().Be(2);
        var prijava = (await Ekran(o).Handle(new GetKvarQuery(kvar.Id), default)).Prijave.Single();
        prijava.Napomena.Should().Be("7 rolni");
        prijava.Fotografije.Should().Equal(slika.Id);
    }

    [Fact]
    public async Task Akcija_ne_pita_na_pregledu_na_kom_je_otvorena_a_posle_odgovora_Ne_ne_pita_ponovo_zamerka_46()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var citaj = (int id) => new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(FakeClock.PosleRasporeda), o.Brojac)
            .Handle(new GetPopunjavanjeQuery(id), default);
        var p1 = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9));
        await PosaljiAsync(o, p1, ("zapocni", new { }), ("akcija", new { stavkaId = o.Stavke[0], vrsta = "zameniti", hitnost = "7_dana", opis = "Rolne" }));

        var u1 = (await citaj(p1)).Sekcije[0].Uredjaji[0];
        u1.OtvoreneAkcije.Should().BeEmpty("akcija otvorena na ovom pregledu ne pravi pitanje");
        u1.AkcijeOvogPregleda!.Single().Broj.Should().Be("A-2026-0001");
        await PosaljiAsync(o, p1, ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }), ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }), ("predaja", new { }));

        var p2 = await NoviPregledAsync(o, "DP1-2026-0002", new DateOnly(2026, 11, 10));
        await PosaljiAsync(o, p2, ("zapocni", new { }));
        var akcijaId = (await citaj(p2)).Sekcije[0].Uredjaji[0].OtvoreneAkcije.Single().Id;
        await PosaljiAsync(o, p2, ("akcija_odgovor", new { akcijaId, uradjeno = false }));
        (await citaj(p2)).Sekcije[0].Uredjaji[0].OtvoreneAkcije.Should().BeEmpty("na „Ne“ je već odgovoreno na ovom pregledu");

        var p3 = await NoviPregledAsync(o, "DP1-2026-0003", new DateOnly(2026, 11, 11));
        await PosaljiAsync(o, p3, ("zapocni", new { }));
        (await citaj(p3)).Sekcije[0].Uredjaji[0].OtvoreneAkcije.Should().ContainSingle("sledeći pregled pita ponovo");
    }

    [Fact]
    public async Task Detalj_pregleda_nosi_akcije_ispod_stavke_i_odgovore_na_ranije_akcije_zamerka_48()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var detalj = (int id) => new PreglediHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana, Roles = [Roles.Supervizor] }, new FakeClock(FakeClock.PosleRasporeda))
            .Handle(new GetPregledQuery(id), default);
        var p1 = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9));
        await PosaljiAsync(o, p1, ("zapocni", new { }), ("akcija", new { stavkaId = o.Stavke[1], vrsta = "podmazati", hitnost = "hitno", opis = "Mast" }));

        var d1 = await detalj(p1);
        d1.Akcije!.Single().Should().BeEquivalentTo(new { Broj = "A-2026-0001", StavkaId = o.Stavke[1], Vrsta = "podmazati", Hitnost = "hitno", Opis = "Mast" });
        new TpmPregledi.Infrastructure.Izvestaji.IzvestajPregleda().Pdf(d1).Should().NotBeEmpty();
        new TpmPregledi.Infrastructure.Izvestaji.IzvestajPregleda().Docx(d1).Should().NotBeEmpty();

        var p2 = await NoviPregledAsync(o, "DP1-2026-0002", new DateOnly(2026, 11, 10));
        await PosaljiAsync(o, p2, ("zapocni", new { }), ("akcija_odgovor", new { akcijaId = d1.Akcije![0].Id, uradjeno = true }));
        var d2 = await detalj(p2);
        d2.Akcije.Should().BeEmpty();
        d2.OdgovoriNaAkcije!.Single().Should().BeEquivalentTo(new { Broj = "A-2026-0001", Uradjena = true });
    }

    private sealed class Skladiste : IFajlSkladiste
    {
        public Task<Fajl> SacuvajAsync(byte[] sadrzaj, string mime, CancellationToken ct) =>
            Task.FromResult(new Fajl { Putanja = Guid.NewGuid() + ".jpg", Mime = mime, Velicina = sadrzaj.Length, Sha256 = "x", KreiranoAt = DateTime.UtcNow });

        public Stream Otvori(Fajl fajl) => new MemoryStream([1]);
    }

    [Fact]
    public async Task Zahtev_za_zatvaranje_odobren_zatvara_akciju_sa_podacima_a_odbijen_je_ostavlja_otvorenom_zamerka_49()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sup = await o.H.SeedUserAsync("sup", "Supervizor2026!", Roles.Supervizor);
        o.H.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = sup.Id, Pravo = Prava.AkcijeZatvaranje });
        await o.H.Db.SaveChangesAsync();
        var sat = new FakeClock(FakeClock.PosleRasporeda);
        var inz = new ZatvaranjeAkcijeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana, Roles = [Roles.Inzenjer] }, sat, new Skladiste());
        var nadredjeni = new ZatvaranjeAkcijeHandler(o.H.Db, new FakeCurrentUser { UserId = sup.Id, Roles = [Roles.Supervizor] }, sat, new Skladiste());
        var p1 = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9));
        await PosaljiAsync(o, p1, ("zapocni", new { }), ("akcija", new { stavkaId = o.Stavke[0], vrsta = "zameniti", hitnost = "7_dana" }));
        var akcijaId = (await o.H.Db.Akcije.SingleAsync()).Id;
        var danas = sat.Today;

        // Prvi zahtev: odbijen uz razlog — akcija ostaje otvorena.
        var d = await inz.Handle(new PodnesiZahtevZatvaranjaCommand(akcijaId, Guid.NewGuid(), "Zamenjena traka", danas, "Ana"), default);
        d.Akcija.ZahtevZatvaranja!.Uradjeno.Should().Be("Zamenjena traka");
        (await FluentActions.Awaiting(() => inz.Handle(new PodnesiZahtevZatvaranjaCommand(akcijaId, Guid.NewGuid(), "Opet", danas, "Ana"), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal("Zahtev za zatvaranje ove akcije već čeka odluku.");
        new OdluciOZahtevuZatvaranjaValidator().Validate(new OdluciOZahtevuZatvaranjaCommand(1, false, null)).IsValid.Should().BeFalse();
        d = await nadredjeni.Handle(new OdluciOZahtevuZatvaranjaCommand(d.Akcija.ZahtevZatvaranja.Id, false, "Traka nije zamenjena"), default);
        d.Akcija.Status.Should().Be("otvorena");
        d.Akcija.ZahtevZatvaranja.Should().BeNull();
        (await o.H.Db.Obavestenja.Where(x => x.KorisnikId == o.Ana).Select(x => x.Naslov).ToListAsync()).Should().Contain("Zahtev za A-2026-0001 je odbijen");

        // Drugi zahtev sa slikom: ponovljeno slanje istog klijentId ne pravi duplikat; odobren zatvara akciju sa podacima.
        var klijent = Guid.NewGuid();
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
        await inz.Handle(new PodnesiZahtevZatvaranjaCommand(akcijaId, klijent, "Zamenjena traka", danas.AddDays(-1), "Služba: Mašinska", jpeg, "image/jpeg"), default);
        d = await inz.Handle(new PodnesiZahtevZatvaranjaCommand(akcijaId, klijent, "Zamenjena traka", danas.AddDays(-1), "Služba: Mašinska", jpeg, "image/jpeg"), default);
        (await o.H.Db.ZahteviZatvaranja.CountAsync(z => z.Status == "zahtev")).Should().Be(1);
        d.Akcija.ZahtevZatvaranja!.ImaSliku.Should().BeTrue();
        d = await nadredjeni.Handle(new OdluciOZahtevuZatvaranjaCommand(d.Akcija.ZahtevZatvaranja.Id, true, null), default);
        d.Akcija.Should().BeEquivalentTo(new { Status = "zatvorena", Uradjeno = "Zamenjena traka", UradjenoDatum = danas.AddDays(-1), Izvrsilac = "Služba: Mašinska", ImaSlikuZatvaranja = true });
        d.Istorija.Select(x => x.Vrsta).Should().Contain(["zahtev_zatvaranja", "zahtev_odbijen", "zatvorena"]);
    }

    [Fact]
    public async Task Inzenjer_otkazuje_svoj_zahtev_a_zatvaranje_sa_ekrana_trazi_sta_kada_i_ko_zamerke_47_49()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sat = new FakeClock(FakeClock.PosleRasporeda);
        var inz = new ZatvaranjeAkcijeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana, Roles = [Roles.Inzenjer] }, sat, new Skladiste());
        var p1 = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9));
        await PosaljiAsync(o, p1, ("zapocni", new { }), ("akcija", new { stavkaId = o.Stavke[0], vrsta = "ocistiti", hitnost = "pratiti" }));
        var akcijaId = (await o.H.Db.Akcije.SingleAsync()).Id;

        var d = await inz.Handle(new PodnesiZahtevZatvaranjaCommand(akcijaId, Guid.NewGuid(), "Očišćeno", sat.Today, "Ana"), default);
        (await FluentActions.Awaiting(() => inz.Handle(new PodnesiZahtevZatvaranjaCommand(akcijaId, Guid.NewGuid(), "x", sat.Today.AddDays(1), "Ana"), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal("Zahtev za zatvaranje ove akcije već čeka odluku.");
        d = await inz.Handle(new OtkaziZahtevZatvaranjaCommand(d.Akcija.ZahtevZatvaranja!.Id), default);
        d.Akcija.ZahtevZatvaranja.Should().BeNull();
        (await o.H.Db.ZahteviZatvaranja.SingleAsync()).Status.Should().Be("otkazano");

        // Zahtev na čekanju se zatvara kad nadređeni zatvori akciju sa ekrana; zatvaranje traži šta, kada i ko.
        d = await inz.Handle(new PodnesiZahtevZatvaranjaCommand(akcijaId, Guid.NewGuid(), "Očišćeno", sat.Today, "Ana"), default);
        new ZatvoriAkcijuValidator().Validate(new ZatvoriAkcijuCommand(akcijaId, false, null, "Očišćeno", null, "Bora")).IsValid.Should().BeFalse();
        d = await Ekran(o).Handle(new ZatvoriAkcijuCommand(akcijaId, false, null, "Očišćeno kompresorom", sat.Today, "Bora"), default);
        d.Akcija.Should().BeEquivalentTo(new { Status = "zatvorena", Uradjeno = "Očišćeno kompresorom", Izvrsilac = "Bora" });
        (await o.H.Db.ZahteviZatvaranja.OrderBy(z => z.Id).LastAsync()).Status.Should().Be("odbijeno");
    }

    [Fact]
    public async Task Ponovljena_prijava_povecava_broj_a_zatvaranje_kvara_oznacava_otvorenu_akciju()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        foreach (var (broj, dan) in new[] { ("DP1-2026-0001", 9), ("DP1-2026-0002", 10) })
        {
            var id = await NoviPregledAsync(o, broj, new DateOnly(2026, 11, dan));
            await PosaljiAsync(o, id, ("zapocni", new { }), ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 1, napomena = $"prijava {dan}" }),
                ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }), ("predaja", new { }));
        }
        var akcijaPregled = await o.H.Db.Pregledi.Select(p => p.Id).MaxAsync();
        (await o.H.Db.Kvarovi.SingleAsync()).Prijava.Should().Be(2);

        // Akcija otvorena sa ekrana nije moguća — otvaramo je kroz treći pregled koji ostaje u radu, pa četvrti zatvara kvar.
        var p3 = await NoviPregledAsync(o, "DP1-2026-0003", new DateOnly(2026, 11, 11));
        await PosaljiAsync(o, p3, ("zapocni", new { }), ("akcija", new { stavkaId = o.Stavke[0], vrsta = "ocistiti", hitnost = "pratiti" }));
        var p4 = await NoviPregledAsync(o, "DP1-2026-0004", new DateOnly(2026, 11, 12));
        await PosaljiAsync(o, p4, ("zapocni", new { }), ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }), ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }), ("predaja", new { }));

        var a = (await Ekran(o).Handle(new GetAkcijeQuery(new AkcijeFilteri()), default)).Single();
        a.StavkaSadaIspravna.Should().BeTrue();
        a.KvarOtvoren.Should().BeFalse();
        akcijaPregled.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Zatvaranje_sa_ekrana_traži_razlog_i_ne_ponavlja_se_a_zaduzeni_ide_u_istoriju()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var p1 = await NoviPregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9));
        await PosaljiAsync(o, p1, ("zapocni", new { }), ("akcija", new { stavkaId = o.Stavke[1], vrsta = "podmazati", hitnost = "hitno" }));
        var id = (await o.H.Db.Akcije.SingleAsync()).Id;

        (await Ekran(o).Handle(new PostaviZaduzenogCommand(id, "Mašinska"), default)).Akcija.Zaduzen.Should().Be("Mašinska");
        (await Ekran(o).Handle(new ZatvoriAkcijuCommand(id, true, "Uređaj se menja"), default)).Akcija.Status.Should().Be("otkazana");
        await FluentActions.Awaiting(() => Ekran(o).Handle(new ZatvoriAkcijuCommand(id, false, "x"), default)).Should().ThrowAsync<BusinessRuleException>();
        new ZatvoriAkcijuValidator().Validate(new ZatvoriAkcijuCommand(id, false, "")).IsValid.Should().BeFalse();
        (await Ekran(o).Handle(new GetAkcijaQuery(id), default)).Istorija.Select(x => x.Vrsta).Should().Equal("otvorena", "zaduzen", "otkazana");
    }
}
