using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Raspored;

/// <summary>Pravila rasporeda (R3.1–R3.20): radni dan, rotacija, predlog, potvrda, PZ, prenos, odsustva, prebacivanja.</summary>
public class RasporedTests
{
    // 2026-11-09 je ponedeljak.
    private static readonly DateOnly Pon = new(2026, 11, 9);
    private static readonly HashSet<DateOnly> Nista = [];


    // ─── čista pravila ─────────────────────────────────────────────────────

    [Fact]
    public void Radni_dan_preskace_vikend_neradni_dan_i_dan_PZ_a()
    {
        var neradni = new HashSet<DateOnly> { Pon.AddDays(1) };
        var pz = new HashSet<DateOnly> { Pon.AddDays(2) };

        RasporedPravila.SledeciDanZaDp(Pon, neradni, pz, dpNaDanPz: false).Should().Be(Pon.AddDays(3));
        RasporedPravila.SledeciDanZaDp(Pon, neradni, pz, dpNaDanPz: true).Should().Be(Pon.AddDays(2));
        RasporedPravila.SledeciDanZaDp(Pon.AddDays(4), Nista, Nista, false).Should().Be(Pon.AddDays(7), "petak → ponedeljak");
    }

    [Fact]
    public void DP_naizmenicno_a_PZ_redom_od_PZ1_do_PZ4()
    {
        RasporedPravila.SledeciDp(null).Should().Be("DP1");
        RasporedPravila.SledeciDp("DP1").Should().Be("DP2");
        RasporedPravila.SledeciDp("DP2").Should().Be("DP1");
        RasporedPravila.SledeciPz(null).Should().Be("PZ1");
        RasporedPravila.SledeciPz("PZ3").Should().Be("PZ4");
        RasporedPravila.SledeciPz("PZ4").Should().Be("PZ1");
    }

    [Fact]
    public void Rotacija_preskace_odsutne_i_krece_od_pocetka()
    {
        string[] redosled = ["ana", "bora", "ceca"];
        RasporedPravila.SledeciInzenjer(redosled, null, _ => true).Should().Be("ana");
        RasporedPravila.SledeciInzenjer(redosled, "ceca", _ => true).Should().Be("ana");
        RasporedPravila.SledeciInzenjer(redosled, "ana", i => i != "bora").Should().Be("ceca");
        RasporedPravila.SledeciInzenjer(redosled, "ana", _ => false).Should().BeNull();
    }

    [Fact]
    public void Prenos_pomera_preglede_a_inzenjeri_ostaju_na_svojim_danima()
    {
        var p1 = new Pregled { Broj = "DP1-1", Tip = "DP1", PlaniraniDatum = Pon, DodeljenId = "ana" };
        var p2 = new Pregled { Broj = "DP2-1", Tip = "DP2", PlaniraniDatum = Pon.AddDays(1), DodeljenId = "bora" };
        var p3 = new Pregled { Broj = "DP1-2", Tip = "DP1", PlaniraniDatum = Pon.AddDays(2), DodeljenId = "ceca" };

        var rez = RasporedPravila.Prenesi([p1, p2, p3], d => RasporedPravila.SledeciDanZaDp(d, Nista, Nista, false), (_, _) => "ana");

        rez.Select(m => (m.Pregled.Broj, m.NoviDatum, m.NoviDodeljenId)).Should().Equal(
            ("DP1-1", Pon.AddDays(1), "bora"),
            ("DP2-1", Pon.AddDays(2), "ceca"),
            ("DP1-2", Pon.AddDays(3), "ana"));
    }

    [Fact]
    public void Zapocet_pregled_ide_sledeceg_dana_sa_istim_inzenjerom()
    {
        var p1 = new Pregled { Tip = "DP1", PlaniraniDatum = Pon, DodeljenId = "ana", Status = StatusPregleda.URadu };
        var p2 = new Pregled { Tip = "DP2", PlaniraniDatum = Pon.AddDays(1), DodeljenId = "bora" };

        var rez = RasporedPravila.Prenesi([p1, p2], d => d.AddDays(1), (_, _) => "ceca");

        rez[0].NoviDodeljenId.Should().Be("ana");
        rez[0].NoviDatum.Should().Be(Pon.AddDays(1));
        rez[1].NoviDatum.Should().Be(Pon.AddDays(2));
    }

    // ─── handleri nad bazom ────────────────────────────────────────────────

    private static async Task<(IdentityHarness h, string ana, string bora, string ceca)> HarnessAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var ana  = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var bora = await h.SeedUserAsync("bora", "Inzenjer2026!", Roles.Inzenjer);
        var ceca = await h.SeedUserAsync("ceca", "Inzenjer2026!", Roles.Inzenjer);
        foreach (var (u, ime) in new[] { (ana, "Ana"), (bora, "Bora"), (ceca, "Ceca") })
        {
            u.FullName = ime;
            h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = u.Id, Pravo = Prava.PreglediPopunjavanje });
        }
        await h.Db.SaveChangesAsync();
        return (h, ana.Id, bora.Id, ceca.Id);
    }

    private static RasporedHandler Sut(IdentityHarness h) => new(h.Db, new FakeBrojac(), new FakeCurrentUser(), new FakeClock());

    [Fact]
    public async Task Predlog_rotira_preskace_neradni_dan_i_odsutne_a_potvrda_daje_brojeve()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        h.Db.NeradniDani.Add(new NeradniDan { Datum = Pon.AddDays(2), Naziv = "Praznik" });
        h.Db.Odsustva.Add(new Odsustvo { KorisnikId = bora, Od = Pon, Do = Pon, Status = "odobreno" });
        await h.Db.SaveChangesAsync();

        var predlog = await Sut(h).Handle(new PredlogRasporedaQuery(Pon), default);

        predlog.Select(p => (p.Datum, p.Tip, p.InzenjerId)).Should().Equal(
            (Pon, "DP1", ana), (Pon.AddDays(1), "DP2", bora), (Pon.AddDays(3), "DP1", ceca), (Pon.AddDays(4), "DP2", ana));

        var potvrda = await Sut(h).Handle(new PotvrdiRasporedCommand(predlog.Select(p => new StavkaRasporeda(p.Datum, p.Tip, p.InzenjerId)).ToList()), default);
        potvrda.Select(p => p.Broj).Should().Equal("DP1-2026-0001", "DP2-2026-0001", "DP1-2026-0002", "DP2-2026-0002");
        (await h.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "dodela")).Should().Be(4);

        var ponovo = () => Sut(h).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon, "DP1", ana)]), default);
        (await ponovo.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Contain(g => g.Contains("već u rasporedu"));
    }

    [Fact]
    public async Task Prenos_propustenog_DP_a_upisuje_istoriju_i_drugi_put_ne_radi_nista()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        await Sut(h).Handle(new PotvrdiRasporedCommand(
            [new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(1), "DP2", bora)]), default);

        (await Sut(h).Handle(new PrenesiCommand(Pon), default)).Should().Be(2);
        (await Sut(h).Handle(new PrenesiCommand(Pon), default)).Should().Be(0, "na ponedeljku više nema DP-a");

        var dp1 = await h.Db.Pregledi.SingleAsync(p => p.Tip == "DP1");
        dp1.PlaniraniDatum.Should().Be(Pon.AddDays(1));
        dp1.PrvobitniDatum.Should().Be(Pon);
        dp1.DodeljenId.Should().Be(bora, "inženjer utorka radi preneti DP1");
        (await h.Db.Pregledi.SingleAsync(p => p.Tip == "DP2")).PlaniraniDatum.Should().Be(Pon.AddDays(2));
        (await h.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "prenos")).Should().Be(2);
    }

    [Fact]
    public async Task PZ_redom_promena_PZ_i_vec_zakazan_PZ_a_DP_tog_dana_se_pomera()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var __ = h;
        await Sut(h).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(1), "DP1", ana)]), default);

        (await Sut(h).Handle(new SledeciPzQuery(), default)).Should().Be("PZ1");
        var pz = await Sut(h).Handle(new ZakaziPzCommand(Pon.AddDays(1), null, bora, null), default);
        pz.Broj.Should().Be("PZ1-2026-0001");
        (await h.Db.Pregledi.SingleAsync(p => p.Tip == "DP1")).PlaniraniDatum.Should().Be(Pon.AddDays(2), "na dan PZ-a DP se ne radi");

        (await Sut(h).Handle(new SledeciPzQuery(), default)).Should().Be("PZ2");
        var dupli = () => Sut(h).Handle(new ZakaziPzCommand(Pon.AddDays(14), "PZ1", ana, null), default);
        (await dupli.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Single().Should().StartWith("Taj PZ je već zakazan");

        await Sut(h).Handle(new ZakaziPzCommand(Pon.AddDays(21), "PZ4", ana, "Kraći zastoj"), default);
        (await h.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "promena_pz")).Should().Be(1);
        (await Sut(h).Handle(new SledeciPzQuery(), default)).Should().Be("PZ1", "redosled se nastavlja od zakazanog PZ-a");
    }

    [Fact]
    public async Task Odsustvo_preklapanje_i_odobri_i_prebaci_a_zapocet_se_ne_prebacuje()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        await Sut(h).Handle(new PotvrdiRasporedCommand(
            [new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(1), "DP2", ana)]), default);
        var sut = new OdsustvaHandler(h.Db, new FakeCurrentUser { UserId = ana }, new FakeClock());

        var zahtev = await sut.Handle(new UnesiOdsustvoCommand(ana, Pon, Pon.AddDays(1), "godisnji", null, true, null), default);
        zahtev.Status.Should().Be("zahtev");
        var preklop = () => sut.Handle(new UnesiOdsustvoCommand(ana, Pon.AddDays(1), Pon.AddDays(3), "bolovanje", null, true, null), default);
        (await preklop.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(OdsustvaHandler.PorukaPreklapanje);

        var zaPrebacivanje = await sut.Handle(new GetPreglediUOdsustvuQuery(ana, Pon, Pon.AddDays(1)), default);
        zaPrebacivanje.Should().HaveCount(2).And.OnlyContain(x => x.PredlogZameneId == bora);

        var dp2 = await h.Db.Pregledi.SingleAsync(p => p.Tip == "DP2");
        dp2.Status = StatusPregleda.URadu;
        await h.Db.SaveChangesAsync();
        var sZapocetim = () => sut.Handle(new OdluciOOdsustvuCommand(zahtev.Id, true, null,
            zaPrebacivanje.Select(x => new PrebacivanjeInput(x.Pregled.Id, ceca)).ToList()), default);
        (await sZapocetim.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(OdsustvaHandler.PorukaZapocet);

        h.Db.ChangeTracker.Clear();
        var dp1 = await h.Db.Pregledi.SingleAsync(p => p.Tip == "DP1");
        var odobreno = await sut.Handle(new OdluciOOdsustvuCommand(zahtev.Id, true, null, [new PrebacivanjeInput(dp1.Id, ceca)]), default);
        odobreno.Status.Should().Be("odobreno");
        (await h.Db.Pregledi.AsNoTracking().SingleAsync(p => p.Id == dp1.Id)).DodeljenId.Should().Be(ceca);
    }

    [Fact]
    public async Task Zahtev_za_prebacivanje_svog_pregleda_i_odluka()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var __ = h;
        var p = (await Sut(h).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon, "DP1", ana)]), default)).Single();

        var tudji = new ZahteviPrebacivanjaHandler(h.Db, new FakeCurrentUser { UserId = bora }, new FakeClock());
        await FluentActions.Awaiting(() => tudji.Handle(new PodnesiZahtevPrebacivanjaCommand(p.Id, "Ne mogu", null), default))
            .Should().ThrowAsync<ForbiddenException>();

        var moj = new ZahteviPrebacivanjaHandler(h.Db, new FakeCurrentUser { UserId = ana }, new FakeClock());
        var z = await moj.Handle(new PodnesiZahtevPrebacivanjaCommand(p.Id, "Lekar", bora), default);
        z.PredlozenaZamena.Should().Be("Bora");

        var odluka = await moj.Handle(new OdluciOPrebacivanjuCommand(z.Id, true, bora), default);
        odluka.Status.Should().Be("odobreno");
        odluka.Pregled.DodeljenId.Should().Be(bora);
    }

    [Fact]
    public async Task Raniji_pocetak_trazi_se_samo_za_PZ_a_odobrenje_pomera_PZ_na_danas()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var __ = h;
        h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = bora, Pravo = Prava.RasporedUredjivanje });
        await h.Db.SaveChangesAsync();
        var danas = new FakeClock(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));   // utorak
        var dp = (await Sut(h).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(3), "DP1", ana)]), default)).Single();
        var pz = await Sut(h).Handle(new ZakaziPzCommand(Pon.AddDays(4), "PZ1", ana, null), default);
        var moj = new ZahteviPrebacivanjaHandler(h.Db, new FakeCurrentUser { UserId = ana }, danas);

        (await FluentActions.Awaiting(() => moj.Handle(new PodnesiZahtevRanijegPocetkaCommand(dp.Id, "Zastoj ranije"), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(ZahteviPrebacivanjaHandler.PorukaRanijiSamoPz);
        await FluentActions.Awaiting(() => new ZahteviPrebacivanjaHandler(h.Db, new FakeCurrentUser { UserId = bora }, danas)
            .Handle(new PodnesiZahtevRanijegPocetkaCommand(pz.Id, "x"), default)).Should().ThrowAsync<ForbiddenException>();

        var z = await moj.Handle(new PodnesiZahtevRanijegPocetkaCommand(pz.Id, "Zastoj počinje danas"), default);
        z.Vrsta.Should().Be(VrsteZahteva.RanijiPocetak);
        (await h.Db.Obavestenja.SingleAsync(o => o.KorisnikId == bora && o.Naslov.StartsWith("Zahtev za raniji"))).Tekst.Should().Contain("Zastoj počinje danas");
        await FluentActions.Awaiting(() => moj.Handle(new PodnesiZahtevRanijegPocetkaCommand(pz.Id, "opet"), default))
            .Should().ThrowAsync<BusinessRuleException>();

        var odluka = await new ZahteviPrebacivanjaHandler(h.Db, new FakeCurrentUser { UserId = bora }, danas)
            .Handle(new OdluciOPrebacivanjuCommand(z.Id, true, null), default);
        odluka.Status.Should().Be("odobreno");
        var p = await h.Db.Pregledi.AsNoTracking().SingleAsync(x => x.Id == pz.Id);
        (p.PlaniraniDatum, p.PrvobitniDatum, p.DodeljenId).Should().Be((Pon.AddDays(1), Pon.AddDays(1), ana));
        (await h.Db.PregledDogadjaji.CountAsync(x => x.PregledId == pz.Id && x.Vrsta == "raniji_pocetak")).Should().Be(1);
        (await h.Db.Obavestenja.SingleAsync(o => o.KorisnikId == ana && o.Naslov == "Raniji početak odobren")).Link.Should().Be($"/pregled/{pz.Id}/popuni");
    }

    [Fact]
    public async Task Istorija_pokazuje_sta_je_promenjeno_u_rasporedu_i_drugog_inzenjera_PZ_a()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var __ = h;
        var pz = await Sut(h).Handle(new ZakaziPzCommand(Pon.AddDays(8), "PZ1", ana, null), default);
        await Sut(h).Handle(new IzmeniDodeluCommand(pz.Id, bora, Pon.AddDays(9)), default);
        await Sut(h).Handle(new PostaviUcesnikaCommand(pz.Id, ceca, ["20", "30"]), default);

        var detalj = await new TpmPregledi.Application.Features.Pregledi.PreglediHandler(h.Db, new FakeCurrentUser(), new FakeClock()).Handle(new TpmPregledi.Application.Features.Pregledi.GetPregledQuery(pz.Id), default);
        detalj.Istorija.Select(x => x.Opis).Should().Contain("Izmena rasporeda: inženjer Ana → Bora; datum 17.11.2026 → 18.11.2026")
            .And.Contain("Drugi inženjer: Ceca, sekcije 20, 30");
        detalj.Ucesnici.Single().Sekcije.Should().Equal("20", "30");
    }

    [Fact]
    public async Task Odsustvo_se_menja_i_otkazuje_a_inzenjer_povlaci_samo_svoj_zahtev_na_cekanju()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var __ = h;
        var sup = new OdsustvaHandler(h.Db, new FakeCurrentUser { UserId = bora }, new FakeClock());
        var o = await sup.Handle(new UnesiOdsustvoCommand(ana, Pon, Pon.AddDays(4), "godisnji", null, false, null), default);

        var izmenjeno = await sup.Handle(new IzmeniOdsustvoCommand(o.Id, Pon, Pon.AddDays(1)), default);
        (izmenjeno.Od, izmenjeno.Do).Should().Be((Pon, Pon.AddDays(1)));
        (await h.Db.Obavestenja.CountAsync(x => x.KorisnikId == ana && x.Naslov == "Odsustvo izmenjeno")).Should().Be(1);

        var anaH = new OdsustvaHandler(h.Db, new FakeCurrentUser { UserId = ana }, new FakeClock());
        await FluentActions.Awaiting(() => anaH.Handle(new OtkaziOdsustvoCommand(o.Id, false), default)).Should().ThrowAsync<ForbiddenException>();

        (await sup.Handle(new OtkaziOdsustvoCommand(o.Id, true), default)).Status.Should().Be("otkazano");
        (await RasporedKontekst.UcitajAsync(h.Db, default)).JeOdsutan(ana, Pon).Should().BeFalse("otkazano odsustvo se ne računa");
        await FluentActions.Awaiting(() => sup.Handle(new IzmeniOdsustvoCommand(o.Id, Pon, Pon), default)).Should().ThrowAsync<BusinessRuleException>();

        // Posle otkazivanja period je slobodan; inženjer povlači svoj zahtev dok čeka odluku.
        var zahtev = await anaH.Handle(new UnesiOdsustvoCommand(ana, Pon, Pon, "slobodan_dan", null, true, null), default);
        (await anaH.Handle(new OtkaziOdsustvoCommand(zahtev.Id, false), default)).Status.Should().Be("otkazano");
    }

    [Fact]
    public async Task Odsutan_inzenjer_ne_moze_da_se_izabere_rucno_a_pregled_koji_mu_je_ostao_je_oznacen()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var __ = h;
        h.Db.Odsustva.Add(new Odsustvo { KorisnikId = bora, Od = Pon.AddDays(8), Do = Pon.AddDays(10), Vrsta = "godisnji", Status = "odobreno" });
        await h.Db.SaveChangesAsync();
        const string poruka = "Bora je odsutan 18.11.2026 (Godišnji odmor). Izaberite drugog inženjera.";

        (await FluentActions.Awaiting(() => Sut(h).Handle(new ZakaziPzCommand(Pon.AddDays(9), "PZ1", bora, null), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(poruka);
        (await FluentActions.Awaiting(() => Sut(h).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(9), "DP1", bora)]), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(poruka);

        var pz = await Sut(h).Handle(new ZakaziPzCommand(Pon.AddDays(9), "PZ1", ana, null), default);
        (await FluentActions.Awaiting(() => Sut(h).Handle(new IzmeniDodeluCommand(pz.Id, bora, null), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(poruka);
        await FluentActions.Awaiting(() => Sut(h).Handle(new PostaviUcesnikaCommand(pz.Id, bora, ["20"]), default)).Should().ThrowAsync<BusinessRuleException>();

        // Pregled koji je ostao odsutnom („Ne prebacuj“) je označen, a Supervizor ga vidi u „Čeka moju odluku“.
        h.Db.Odsustva.Add(new Odsustvo { KorisnikId = ana, Od = Pon.AddDays(9), Do = Pon.AddDays(9), Vrsta = "bolovanje", Status = "odobreno" });
        await h.Db.SaveChangesAsync();
        var nedelja = await Sut(h).Handle(new GetRasporedQuery(Pon.AddDays(7), Pon.AddDays(13)), default);
        nedelja.Pregledi.Single(x => x.Id == pz.Id).InzenjerOdsutan.Should().BeTrue();

        var sup = await h.SeedUserAsync("sup", "Supervizor2026!", Roles.Supervizor);
        h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = sup.Id, Pravo = Prava.RasporedUredjivanje });
        await h.Db.SaveChangesAsync();
        var dash = await new TpmPregledi.Application.Features.Analitika.DashboardHandler(h.Db, new FakeCurrentUser { UserId = sup.Id, Roles = [Roles.Supervizor] }, new FakeClock(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)))
            .Handle(new TpmPregledi.Application.Features.Analitika.GetDashboardQuery(), default);
        dash.CekaOdluku!.Should().Contain(o => o.Vrsta == "odsutan" && o.Opis.Contains("PZ1-2026-0001"));
    }

    [Fact]
    public void Podesavanja_imaju_DP_na_dan_PZ_iskljuceno_podrazumevano()
        => PodesavanjaDto.Podrazumevana.DpNaDanPz.Should().BeFalse();
}
