using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Obavestenja;

/// <summary>Obaveštenja (R9.1–R9.4): primaoci, idempotentne zakazane provere, lična obaveštenja, zvono i isporuka push-a i mejla.</summary>
public class ObavestenjaTests
{
    private sealed class FakePush : IPushSlanje
    {
        public string? JavniKljuc => "javni";
        public List<string> Poslato { get; } = [];
        public HashSet<string> Istekle { get; } = [];
        public bool Baci { get; set; }

        public Task<PushIshod> PosaljiAsync(PushPretplata p, string poruka, CancellationToken ct)
        {
            if (Baci) throw new HttpRequestException("push servis nedostupan");
            if (Istekle.Contains(p.Endpoint)) return Task.FromResult(PushIshod.Istekla);
            Poslato.Add($"{p.Endpoint}|{poruka}");
            return Task.FromResult(PushIshod.Poslato);
        }
    }

    private sealed class FakeMejl : IMejlSlanje
    {
        public List<string> Poslato { get; } = [];

        public Task PosaljiAsync(string adresa, string naslov, string tekst, string? link, CancellationToken ct)
        {
            Poslato.Add($"{adresa}|{naslov}");
            return Task.CompletedTask;
        }
    }

    private sealed record Okr(IdentityHarness H, string Ana, string Sup, string Dir, string Admin);

    /// <summary>Ana (Inženjer), Sup (Supervizor) i Dir (Direktor) sa podrazumevanim pravima; Admin bez prava overe.</summary>
    private static async Task<Okr> PripremiAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var sup = await h.SeedUserAsync("sup", "Supervizor2026!", Roles.Supervizor);
        var dir = await h.SeedUserAsync("dir", "Direktor2026!", Roles.Direktor);
        var adm = await h.SeedUserAsync("adm", "Admin2026!", Roles.Admin);
        h.Db.KorisnikPrava.AddRange(
            new KorisnikPravo { KorisnikId = ana.Id, Pravo = Prava.PreglediPopunjavanje },
            new KorisnikPravo { KorisnikId = sup.Id, Pravo = Prava.Overa }, new KorisnikPravo { KorisnikId = sup.Id, Pravo = Prava.OdsustvaOdobravanje },
            new KorisnikPravo { KorisnikId = sup.Id, Pravo = Prava.RasporedUredjivanje },
            new KorisnikPravo { KorisnikId = dir.Id, Pravo = Prava.Overa });
        await h.Db.SaveChangesAsync();
        return new Okr(h, ana.Id, sup.Id, dir.Id, adm.Id);
    }

    private static async Task<Pregled> PregledAsync(Okr o, string broj, DateOnly dan, string status = StatusPregleda.Planiran, DateTime? predatAt = null)
    {
        var p = new Pregled { Broj = broj, Tip = broj[..3], PlaniraniDatum = dan, PrvobitniDatum = dan, DodeljenId = o.Ana, Status = status, PredatAt = predatAt };
        o.H.Db.Pregledi.Add(p);
        await o.H.Db.SaveChangesAsync();
        return p;
    }

    private static FakeClock Sat(int dan, int sat, int min = 0) => new(new DateTime(2026, 11, dan, sat, min, 0, DateTimeKind.Utc));

    private static Task<List<Obavestenje>> SvaAsync(Okr o) => o.H.Db.Obavestenja.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

    [Fact]
    public async Task Podrazumevani_primaoci_su_Supervizor_i_Direktor_a_Admin_ih_menja_po_vrsti()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        (await Obavesti.PrimaociAsync(o.H.Db, VrsteObavestenja.RokOvere, default)).Should().BeEquivalentTo([o.Sup, o.Dir]);

        var sut = new ObavestenjaHandler(o.H.Db, new FakeCurrentUser { UserId = o.Admin }, new FakeClock(), new FakePush());
        var r = await sut.Handle(new PostaviPrimaoceCommand(new() { [VrsteObavestenja.RokOvere] = [o.Admin] }), default);
        r.Vrste.Single(v => v.Vrsta == VrsteObavestenja.RokOvere).Should().Match<VrstaPrimalacaDto>(v => v.Podeseno && v.KorisnikIds.Single() == o.Admin);
        r.Vrste.Single(v => v.Vrsta == VrsteObavestenja.Rezime).Podeseno.Should().BeFalse();
        (await Obavesti.PrimaociAsync(o.H.Db, VrsteObavestenja.Rezime, default)).Should().HaveCount(2);

        await sut.Invoking(s => s.Handle(new PostaviPrimaoceCommand(new() { [VrsteObavestenja.Rezime] = ["nema"] }), default))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Nije_zapocet_ide_posle_13h_primaocima_i_inzenjeru_i_samo_jednom()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var p = await PregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9));

        // 11:30 UTC = 12:30 u Beogradu — pre upozorenja.
        (await new ZakazaneProvereHandler(o.H.Db, Sat(9, 11, 30)).Handle(new PokreniProvereCommand(), default)).Should().Be(0);
        (await new ZakazaneProvereHandler(o.H.Db, Sat(9, 12, 5)).Handle(new PokreniProvereCommand(), default)).Should().Be(3);
        (await new ZakazaneProvereHandler(o.H.Db, Sat(9, 12, 30)).Handle(new PokreniProvereCommand(), default)).Should().Be(0);

        var sva = await SvaAsync(o);
        sva.Select(x => x.KorisnikId).Should().BeEquivalentTo([o.Ana, o.Sup, o.Dir]);
        sva.Should().OnlyContain(x => x.Vrsta == VrsteObavestenja.NijeZapocet && x.Link == $"/pregledi/{p.Id}");
    }

    [Fact]
    public async Task Rok_overe_broji_samo_radne_dane_a_rok_akcije_ide_jednom()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        // Predat u petak u podne; vikend se ne broji.
        await PregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 13), StatusPregleda.CekaOveru, new DateTime(2026, 11, 13, 11, 0, 0, DateTimeKind.Utc));

        await new ZakazaneProvereHandler(o.H.Db, Sat(16, 10)).Handle(new PokreniProvereCommand(), default);
        (await SvaAsync(o)).Should().NotContain(x => x.Vrsta == VrsteObavestenja.RokOvere);
        await new ZakazaneProvereHandler(o.H.Db, Sat(16, 12)).Handle(new PokreniProvereCommand(), default);
        (await SvaAsync(o)).Count(x => x.Vrsta == VrsteObavestenja.RokOvere).Should().Be(2);

        var u = new Uredjaj { Naziv = "Transporter", Oznaka = "10-C-08", Sekcija = new Sekcija { Sifra = "10" } };
        var akt = new Aktivnost { Naziv = "Pregled rolni", OdgovorIspravan = "Da", OdgovorNeispravan = "Ne" };
        o.H.Db.AddRange(u, akt);
        await o.H.Db.SaveChangesAsync();
        o.H.Db.Akcije.Add(new Akcija { Broj = "A-2026-0001", PregledId = (await o.H.Db.Pregledi.FirstAsync()).Id, UredjajId = u.Id, AktivnostId = akt.Id, Rok = new DateOnly(2026, 11, 15), KreiraoId = o.Ana });
        await o.H.Db.SaveChangesAsync();
        await new ZakazaneProvereHandler(o.H.Db, Sat(16, 13)).Handle(new PokreniProvereCommand(), default);
        await new ZakazaneProvereHandler(o.H.Db, Sat(17, 13)).Handle(new PokreniProvereCommand(), default);
        (await SvaAsync(o)).Where(x => x.Vrsta == VrsteObavestenja.RokAkcije).Should().HaveCount(2).And.OnlyContain(x => x.Tekst.Contains("Transporter 10-C-08"));
    }

    [Fact]
    public async Task Rezime_posle_kraja_radnog_dana_i_predlog_rasporeda_cetvrtkom()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        // Četvrtak 12.11.: jedan predat, jedan nije; za nedelju od 16.11. nema DP-a.
        await PregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 12), StatusPregleda.CekaOveru, new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc));
        await PregledAsync(o, "PZ1-2026-0001", new DateOnly(2026, 11, 12));

        await new ZakazaneProvereHandler(o.H.Db, Sat(12, 13)).Handle(new PokreniProvereCommand(), default);   // 14:00, pre kraja dana
        (await SvaAsync(o)).Should().NotContain(x => x.Vrsta == VrsteObavestenja.Rezime);
        await new ZakazaneProvereHandler(o.H.Db, Sat(12, 14, 30)).Handle(new PokreniProvereCommand(), default);
        var sva = await SvaAsync(o);
        sva.First(x => x.Vrsta == VrsteObavestenja.Rezime).Tekst.Should().StartWith("Predato 1 od 2 pregleda, propušteno 1.");
        sva.First(x => x.Vrsta == VrsteObavestenja.PredlogRasporeda).Tekst.Should().Contain("16.11.2026");
    }

    [Fact]
    public async Task Rezime_navodi_preneti_DP_i_stize_radnim_danom_i_bez_pregleda_zamerka_61()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        // Utorak 10.11.: DP1 nije ni započet i prenet je na sredu (Bora); prenos za danas je obrađen.
        var p = await PregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 10));
        TpmPregledi.Application.Features.Raspored.PregledIstorija.Dodaj(o.H.Db, p, "prenos",
            new { razlog = "propusten", stari = new { datum = new DateOnly(2026, 11, 10), inzenjer = "Ana Anić" }, novi = new { datum = new DateOnly(2026, 11, 11), inzenjer = "Bora Borić" } },
            null, new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc));
        p.PlaniraniDatum = new DateOnly(2026, 11, 11);
        o.H.Db.Podesavanja.Add(new Podesavanje { Kljuc = PrenosPropustenog.KljucPoslednji, Vrednost = "\"2026-11-10\"" });
        await o.H.Db.SaveChangesAsync();

        await new ZakazaneProvereHandler(o.H.Db, Sat(10, 14, 1)).Handle(new PokreniProvereCommand(), default);
        var rezime = (await SvaAsync(o)).First(x => x.Vrsta == VrsteObavestenja.Rezime);
        rezime.Tekst.Should().Contain("Preneto: DP1-2026-0001 (Ana Anić) → sre 11.11. Bora Borić; naredni DP-ovi pomereni za dan.");
        rezime.Link.Should().Be("/kalendar");

        // Četvrtak bez pregleda: rezime ipak stiže (posle 15 min i bez zapisa prenosa); subota bez pregleda: ne.
        await new ZakazaneProvereHandler(o.H.Db, Sat(12, 14, 20)).Handle(new PokreniProvereCommand(), default);
        (await SvaAsync(o)).Should().Contain(x => x.Kljuc == "rezime:2026-11-12" && x.Tekst == "Danas nije bilo zakazanih pregleda. Proverite raspored.");
        await new ZakazaneProvereHandler(o.H.Db, Sat(14, 14, 20)).Handle(new PokreniProvereCommand(), default);
        (await SvaAsync(o)).Should().NotContain(x => x.Kljuc == "rezime:2026-11-14");
    }

    [Theory]
    [InlineData(null, "2026-11-10", true, new[] { "2026-11-10" })]
    [InlineData(null, "2026-11-10", false, new string[0])]
    [InlineData("2026-11-09", "2026-11-10", false, new string[0])]
    [InlineData("2026-11-10", "2026-11-10", true, new string[0])]
    [InlineData("2026-11-06", "2026-11-10", false, new[] { "2026-11-07", "2026-11-08", "2026-11-09" })]
    [InlineData("2026-11-06", "2026-11-10", true, new[] { "2026-11-07", "2026-11-08", "2026-11-09", "2026-11-10" })]
    public void Prenos_obradjuje_i_dane_kad_server_nije_radio_zamerka_59(string? poslednji, string danas, bool prosaoKraj, string[] ocekivano) =>
        PrenosPropustenog.DaniZaObradu(poslednji is null ? null : DateOnly.Parse(poslednji), DateOnly.Parse(danas), prosaoKraj)
            .Should().Equal(ocekivano.Select(DateOnly.Parse));

    [Fact]
    public async Task Pregled_inzenjera_bez_prava_popunjavanja_ceka_odluku_zamerka_66()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        await PregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 20));
        o.H.Db.KorisnikPrava.RemoveRange(o.H.Db.KorisnikPrava.Where(x => x.KorisnikId == o.Ana));
        await o.H.Db.SaveChangesAsync();

        var dash = await new TpmPregledi.Application.Features.Analitika.DashboardHandler(o.H.Db,
                new FakeCurrentUser { UserId = o.Sup, Roles = [Roles.Supervizor] }, Sat(16, 9))
            .Handle(new TpmPregledi.Application.Features.Analitika.GetDashboardQuery(), default);
        dash.CekaOdluku!.Should().ContainSingle(x => x.Vrsta == "bez_prava").Which.Opis.Should().Contain("nema pravo popunjavanja — prebacite pregled");
    }

    [Fact]
    public async Task Promena_dodele_i_ishod_odsustva_obavestavaju_inzenjera()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var ben = await o.H.SeedUserAsync("ben", "Inzenjer2026!", Roles.Inzenjer);
        o.H.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = ben.Id, Pravo = Prava.PreglediPopunjavanje });
        await o.H.Db.SaveChangesAsync();
        var p = await PregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 10));

        var sup = new FakeCurrentUser { UserId = o.Sup, Roles = [Roles.Supervizor] };
        await new RasporedHandler(o.H.Db, new FakeBrojac(), sup, new FakeClock()).Handle(new IzmeniDodeluCommand(p.Id, ben.Id, null), default);
        var sva = await SvaAsync(o);
        sva.Single(x => x.KorisnikId == ben.Id).Naslov.Should().Be("Dodeljen vam je DP1-2026-0001");
        sva.Single(x => x.KorisnikId == o.Ana).Naslov.Should().Be("DP1-2026-0001 više nije vaš");

        var zahtev = new Odsustvo { KorisnikId = o.Ana, Od = new DateOnly(2026, 11, 20), Do = new DateOnly(2026, 11, 21), Status = "zahtev" };
        o.H.Db.Odsustva.Add(zahtev);
        await o.H.Db.SaveChangesAsync();
        await new OdsustvaHandler(o.H.Db, sup, new FakeClock()).Handle(new OdluciOOdsustvuCommand(zahtev.Id, false, "Nema zamene", null), default);
        (await SvaAsync(o)).Last().Should().Match<Obavestenje>(x => x.KorisnikId == o.Ana && x.Vrsta == VrsteObavestenja.IshodZahteva && x.Tekst.Contains("Nema zamene"));
    }

    [Fact]
    public async Task Zvono_broji_neprocitana_i_ono_sto_ceka_odluku_supervizora()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        await PregledAsync(o, "DP1-2026-0001", new DateOnly(2026, 11, 9), StatusPregleda.CekaOveru, DateTime.UtcNow);
        o.H.Db.Odsustva.Add(new Odsustvo { KorisnikId = o.Ana, Od = new DateOnly(2026, 11, 20), Do = new DateOnly(2026, 11, 20), Status = "zahtev" });
        await Obavesti.DodajAsync(o.H.Db, [o.Sup, o.Sup], "probno", "A", "a", null, null, DateTime.UtcNow, default);
        await Obavesti.DodajAsync(o.H.Db, [o.Sup], "probno", "B", "b", null, "k1", DateTime.UtcNow, default);
        await Obavesti.DodajAsync(o.H.Db, [o.Sup], "probno", "B", "b", null, "k1", DateTime.UtcNow, default);   // isti ključ: ne duplira
        await o.H.Db.SaveChangesAsync();

        var sut = new ObavestenjaHandler(o.H.Db, new FakeCurrentUser { UserId = o.Sup, Roles = [Roles.Supervizor] }, new FakeClock(), new FakePush());
        var z = await sut.Handle(new GetZvonoQuery(), default);
        z.Neprocitanih.Should().Be(2);
        z.CekaOdluku.Should().Be(2);   // overa + odsustvo
        (await sut.Handle(new OznaciProcitanoCommand([z.Stavke[0].Id]), default)).Neprocitanih.Should().Be(1);
        (await sut.Handle(new OznaciProcitanoCommand(null), default)).Neprocitanih.Should().Be(0);

        var inz = new ObavestenjaHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana, Roles = [Roles.Inzenjer] }, new FakeClock(), new FakePush());
        (await inz.Handle(new GetZvonoQuery(), default)).CekaOdluku.Should().Be(0);
    }

    [Fact]
    public async Task Isporuka_salje_push_i_mejl_brise_istekle_pretplate_i_ponavlja_posle_greske()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var push = new FakePush();
        push.Istekle.Add("https://push/stara");
        var mejl = new FakeMejl();
        var sup = new ObavestenjaHandler(o.H.Db, new FakeCurrentUser { UserId = o.Sup }, new FakeClock(), push);
        await sup.Handle(new PrijaviPushCommand("https://push/nova", "p", "a", "Chrome"), default);
        await sup.Handle(new PrijaviPushCommand("https://push/stara", "p", "a", "Chrome"), default);
        await sup.Handle(new PosaljiProbnoCommand(), default);

        push.Baci = true;
        (await new IsporukaHandler(o.H.Db, new FakeClock(), push, mejl).Handle(new IsporuciObavestenjaCommand(), default)).Should().Be(0);
        (await o.H.Db.Obavestenja.SingleAsync()).Pokusaja.Should().Be(1);

        push.Baci = false;
        (await new IsporukaHandler(o.H.Db, new FakeClock(), push, mejl).Handle(new IsporuciObavestenjaCommand(), default)).Should().Be(1);
        var poslato = push.Poslato.Single().Split('|', 2);
        poslato[0].Should().Be("https://push/nova");
        JsonDocument.Parse(poslato[1]).RootElement.GetProperty("title").GetString().Should().Be("Probno obaveštenje");
        mejl.Poslato.Single().Should().Be("sup@test.local|Probno obaveštenje");
        (await o.H.Db.PushPretplate.Select(p => p.Endpoint).ToListAsync()).Should().Equal("https://push/nova");
        (await o.H.Db.Obavestenja.SingleAsync()).IsporucenoAt.Should().NotBeNull();
    }

    // ─── Iz popunjavanja ──────────────────────────────────────────────────

    private sealed class Citac : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) => [new(2, "DP1", "10", 1, "Transporter", "10-C-08", "Pregled rolni", "Okreće", "Ne okreće", "")];
    }

    [Fact]
    public async Task Hitna_akcija_i_treca_prijava_iste_stavke_salju_obavestenja()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        await new UvoziSabloneHandler(o.H.Db, new Citac(), new FakeCurrentUser(), new FakeClock()).Handle(new UvoziSabloneCommand(Stream.Null), default);
        var stavka = await o.H.Db.SablonStavke.Select(s => s.Id).SingleAsync();
        var brojac = new FakeBrojac();
        var jo = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        for (var dan = 9; dan <= 11; dan++)
        {
            var p = await PregledAsync(o, $"DP1-2026-000{dan - 8}", new DateOnly(2026, 11, dan));
            var ops = new List<(string, object)> { ("zapocni", new { }), ("odgovor", new { stavkaId = stavka, vrednost = 1, napomena = "ne okreće" }) };
            if (dan == 9) ops.Add(("akcija", new { stavkaId = stavka, vrsta = "zameniti", hitnost = "hitno" }));
            ops.Add(("predaja", new { }));
            var r = await new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(FakeClock.PosleRasporeda), brojac).Handle(new SinhronizacijaCommand(null, null,
                ops.Select(x => new OperacijaInput(Guid.NewGuid(), x.Item1, p.Id, new DateTime(2026, 11, dan, 8, 0, 0, DateTimeKind.Utc), JsonSerializer.SerializeToElement(x.Item2, jo))).ToList()), default);
            r.Odbijene.Should().BeEmpty();
        }

        var sva = await SvaAsync(o);
        sva.Where(x => x.Vrsta == VrsteObavestenja.HitnaAkcija).Should().HaveCount(2).And.OnlyContain(x => x.Naslov == "Hitna akcija A-2026-0001");
        sva.Where(x => x.Vrsta == VrsteObavestenja.PonovljeniKvar).Should().HaveCount(2).And.OnlyContain(x => x.Tekst.StartsWith("Pregled rolni je neispravno 3 puta"));
    }
}
