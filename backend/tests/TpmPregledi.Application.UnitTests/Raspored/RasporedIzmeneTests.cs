using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Raspored;

/// <summary>Zamerke 26–31: predlog od danas, otkazivanje, povlačenje niza, kvačice/umetanje, promena tipa DP-a, podsetnik.</summary>
public class RasporedIzmeneTests
{
    // 2026-11-09 je ponedeljak.
    private static readonly DateOnly Pon = new(2026, 11, 9);

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

    private static RasporedHandler Sut(IdentityHarness h, DateOnly? danas = null) =>
        new(h.Db, new FakeBrojac(), new FakeCurrentUser(), danas is { } d ? new FakeClock(d.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc)) : new FakeClock());

    private static async Task<List<(DateOnly, string)>> NizAsync(IdentityHarness h) =>
        (await h.Db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("DP") && p.Status != StatusPregleda.Ponisten)
            .OrderBy(p => p.PlaniraniDatum).Select(p => new { p.PlaniraniDatum, p.Tip }).ToListAsync())
        .Select(x => (x.PlaniraniDatum, x.Tip)).ToList();

    [Fact]
    public void Povlacenje_je_obrnuto_od_prenosa_a_inzenjeri_ostaju_na_danima()
    {
        var p1 = new Pregled { Broj = "DP2-1", Tip = "DP2", PlaniraniDatum = Pon.AddDays(2), DodeljenId = "bora" };
        var p2 = new Pregled { Broj = "DP1-2", Tip = "DP1", PlaniraniDatum = Pon.AddDays(3), DodeljenId = "ceca" };

        var (pomeranja, oslobodjen, inz) = RasporedPravila.Povuci(Pon.AddDays(1), "ana", [p1, p2]);

        pomeranja.Select(m => (m.Pregled.Broj, m.NoviDatum, m.NoviDodeljenId)).Should().Equal(
            ("DP2-1", Pon.AddDays(1), "ana"), ("DP1-2", Pon.AddDays(2), "bora"));
        oslobodjen.Should().Be(Pon.AddDays(3));
        inz.Should().Be("ceca");
        RasporedPravila.Povuci(Pon, "ana", []).Oslobodjen.Should().BeNull();
    }

    [Fact]
    public async Task Predlog_ne_nudi_prosle_dane_a_tip_racuna_od_DP_a_dan_pre_u_istoj_nedelji()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var _ = h;
        // Ponedeljak DP1, četvrtak DP2 već postoje; danas je utorak.
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(3), "DP2", bora)]), default);

        var predlog = await Sut(h, Pon.AddDays(1)).Handle(new PredlogRasporedaQuery(Pon), default);

        predlog.Select(p => (p.Datum, p.Tip, p.Rupa)).Should().Equal(
            (Pon.AddDays(1), "DP2", true), (Pon.AddDays(2), "DP1", true), (Pon.AddDays(4), "DP1", false));
        predlog[2].DatumPre.Should().Be(Pon.AddDays(3), "petak gleda četvrtkov DP2");
    }

    [Fact]
    public async Task Pomeren_PZ_na_dan_sa_DP_om_pomera_DP_ove_kao_zakazivanje_zamerka_43()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        // Pon DP1, uto DP2, sre DP1, čet DP2; PZ u petak.
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(1), "DP2", bora),
            new StavkaRasporeda(Pon.AddDays(2), "DP1", ana), new StavkaRasporeda(Pon.AddDays(3), "DP2", bora)]), default);
        var pz = await Sut(h, Pon).Handle(new ZakaziPzCommand(Pon.AddDays(4), null, ceca, null), default);

        var r = await Sut(h, Pon).Handle(new IzmeniDodeluCommand(pz.Id, null, Pon.AddDays(1)), default);

        (await NizAsync(h)).Should().Equal((Pon, "DP1"), (Pon.AddDays(2), "DP2"), (Pon.AddDays(3), "DP1"), (Pon.AddDays(4), "DP2"));
        r.SlobodanDan.Should().BeNull("petak je popunio pomereni niz");
    }

    [Fact]
    public async Task PZ_pomeren_sa_srede_na_utorak_pomera_samo_DP_iz_utorka_u_sredu_zamerka_43()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        // Pon DP1, uto DP2, čet DP1, pet DP2; PZ u sredu (sredinom nedelje, posle njega ima DP-ova).
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(1), "DP2", bora),
            new StavkaRasporeda(Pon.AddDays(3), "DP1", ana), new StavkaRasporeda(Pon.AddDays(4), "DP2", bora)]), default);
        var pz = await Sut(h, Pon).Handle(new ZakaziPzCommand(Pon.AddDays(2), null, ceca, null), default);
        var preIds = (await h.Db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("DP")).OrderBy(p => p.PlaniraniDatum).Select(p => new { p.Id, p.PlaniraniDatum }).ToListAsync());

        await Sut(h, Pon).Handle(new IzmeniDodeluCommand(pz.Id, null, Pon.AddDays(1)), default);

        var posle = await h.Db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("DP")).ToDictionaryAsync(p => p.Id, p => p.PlaniraniDatum);
        posle[preIds[1].Id].Should().Be(Pon.AddDays(2), "DP iz utorka ide u sredu koju je PZ oslobodio");
        posle[preIds[2].Id].Should().Be(Pon.AddDays(3), "DP-ovi posle srede ostaju gde su");
        posle[preIds[3].Id].Should().Be(Pon.AddDays(4));

        // Nazad na sredu: DP iz srede ide u utorak, ostali ostaju.
        await Sut(h, Pon).Handle(new IzmeniDodeluCommand(pz.Id, null, Pon.AddDays(2)), default);
        posle = await h.Db.Pregledi.AsNoTracking().Where(p => p.Tip.StartsWith("DP")).ToDictionaryAsync(p => p.Id, p => p.PlaniraniDatum);
        posle[preIds[1].Id].Should().Be(Pon.AddDays(1));
        posle[preIds[2].Id].Should().Be(Pon.AddDays(3));
    }

    [Fact]
    public async Task Otkazan_DP_sa_zadrzanim_rasporedom_se_ponistava_a_zahtev_se_zatvara()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var _ = h;
        var dp = (await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(1), "DP1", ana), new StavkaRasporeda(Pon.AddDays(2), "DP2", bora)]), default))[0];
        h.Db.ZahteviPrebacivanja.Add(new ZahtevPrebacivanja { PregledId = dp.Id, PodneoId = ana, Razlog = "Lekar", KreiranoAt = DateTime.UtcNow });
        await h.Db.SaveChangesAsync();

        var r = await Sut(h, Pon).Handle(new OtkaziPregledCommand(dp.Id, "Fabrika ne radi", Pomeri: false), default);

        r.Status.Should().Be(StatusPregleda.Ponisten);
        r.SlobodanDan.Should().BeNull("DP sa „zadrži raspored“ je svesno ostavljen prazan");
        (await h.Db.ZahteviPrebacivanja.SingleAsync()).Status.Should().Be("odbijeno");
        (await h.Db.Obavestenja.CountAsync(o => o.KorisnikId == ana && o.Naslov.Contains("otkazan"))).Should().Be(1);
        (await NizAsync(h)).Should().Equal((Pon.AddDays(2), "DP2"));
        await Sut(h, Pon).Invoking(s => s.Handle(new OtkaziPregledCommand(dp.Id, "opet", false), default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*");
    }

    [Fact]
    public async Task Otkazan_DP_sa_pomeranjem_ide_dan_napred_sa_celim_nizom()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        var novi = await Sut(h, Pon).Handle(new PotvrdiRasporedCommand(
            [new StavkaRasporeda(Pon.AddDays(1), "DP1", ana), new StavkaRasporeda(Pon.AddDays(2), "DP2", bora), new StavkaRasporeda(Pon.AddDays(3), "DP1", ceca)]), default);

        await Sut(h, Pon).Handle(new OtkaziPregledCommand(novi[0].Id, "Zastoj linije", Pomeri: true), default);

        (await NizAsync(h)).Should().Equal((Pon.AddDays(2), "DP1"), (Pon.AddDays(3), "DP2"), (Pon.AddDays(4), "DP1"));
        var prvi = await h.Db.Pregledi.SingleAsync(p => p.Id == novi[0].Id);
        prvi.Propusten.Should().BeFalse("ručno pomeranje je sivo, ne „nije urađen“");
        prvi.Status.Should().Be(StatusPregleda.Planiran);
    }

    [Fact]
    public async Task Pomeren_PZ_oslobadja_dan_a_povlacenje_vraca_smenu_DP1_DP2()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand(
            [new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(1), "DP2", bora), new StavkaRasporeda(Pon.AddDays(2), "DP1", ceca)]), default);
        // PZ u utorak gura DP-ove dan napred (pon DP1, sre DP2, čet DP1).
        var pz = await Sut(h, Pon).Handle(new ZakaziPzCommand(Pon.AddDays(1), null, ana, null), default);
        (await NizAsync(h)).Should().Equal((Pon, "DP1"), (Pon.AddDays(2), "DP2"), (Pon.AddDays(3), "DP1"));

        var pomeren = await Sut(h, Pon).Handle(new IzmeniDodeluCommand(pz.Id, null, Pon.AddDays(21)), default);
        pomeren.SlobodanDan.Should().Be(Pon.AddDays(1), "utorak je ostao bez DP-a usred niza");

        (await Sut(h, Pon).Handle(new PovuciRasporedCommand(Pon.AddDays(1)), default)).Should().Be(2);
        (await NizAsync(h)).Should().Equal((Pon, "DP1"), (Pon.AddDays(1), "DP2"), (Pon.AddDays(2), "DP1"));
        (await h.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "povlacenje")).Should().Be(2);
        await Sut(h, Pon).Invoking(s => s.Handle(new PovuciRasporedCommand(Pon.AddDays(1)), default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Otkazan_PZ_nudi_povlacenje_a_sledeci_PZ_je_isti_tip()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var _ = h;
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(1), "DP1", ana), new StavkaRasporeda(Pon.AddDays(2), "DP2", bora)]), default);
        var pz = await Sut(h, Pon).Handle(new ZakaziPzCommand(Pon.AddDays(1), null, ana, null), default);
        (await Sut(h, Pon).Handle(new SledeciPzQuery(), default)).Should().Be("PZ2");

        var r = await Sut(h, Pon).Handle(new OtkaziPregledCommand(pz.Id, "Nema delova", Pomeri: false), default);

        r.SlobodanDan.Should().Be(Pon.AddDays(1));
        (await Sut(h, Pon).Handle(new SledeciPzQuery(), default)).Should().Be("PZ1", "otkazan PZ se ne računa u redosled");
        await Sut(h, Pon).Invoking(s => s.Handle(new OtkaziPregledCommand(pz.Id, "x", Pomeri: true), default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Potvrda_sa_uskladjivanjem_povlaci_niz_u_rupu_i_popunjava_kraj_nedelje()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        // pon DP1, (uto rupa), sre DP2, čet DP1, pet DP2.
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand(
            [new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(2), "DP2", bora),
             new StavkaRasporeda(Pon.AddDays(3), "DP1", ceca), new StavkaRasporeda(Pon.AddDays(4), "DP2", ana)]), default);

        var novi = await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(1), "DP2", ceca)], Uskladi: true), default);

        (await NizAsync(h)).Should().Equal((Pon, "DP1"), (Pon.AddDays(1), "DP2"), (Pon.AddDays(2), "DP1"), (Pon.AddDays(3), "DP2"), (Pon.AddDays(4), "DP1"));
        novi.Single().PlaniraniDatum.Should().Be(Pon.AddDays(4), "nov DP je na kraju nedelje, a ne u rupi");
        (await h.Db.Pregledi.SingleAsync(p => p.PlaniraniDatum == Pon.AddDays(1))).DodeljenId.Should().Be(ceca, "utorak dobija inženjera iz predloga");
    }

    [Fact]
    public async Task Potvrda_bez_uskladjivanja_racuna_tip_od_dana_pre()
    {
        var (h, ana, bora, _) = await HarnessAsync();
        await using var _ = h;
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon, "DP1", ana)]), default);

        // Klijent šalje pogrešan tip — server ga računa od DP-a dan pre.
        var novi = await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(1), "DP1", bora)]), default);

        novi.Single().Tip.Should().Be("DP2");
    }

    [Fact]
    public async Task Promena_tipa_DP_a_daje_nov_broj_i_uskladjuje_naredne()
    {
        var (h, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        var novi = await Sut(h, Pon).Handle(new PotvrdiRasporedCommand(
            [new StavkaRasporeda(Pon, "DP1", ana), new StavkaRasporeda(Pon.AddDays(1), "DP2", bora), new StavkaRasporeda(Pon.AddDays(2), "DP1", ceca)]), default);

        var r = await Sut(h, Pon).Handle(new PromeniTipDpCommand(novi[0].Id, "DP2", Uskladi: true), default);

        r.Broj.Should().StartWith("DP2-").And.NotBe(novi[0].Broj, "broj sadrži tip, pa promena tipa daje nov broj");
        (await NizAsync(h)).Should().Equal((Pon, "DP2"), (Pon.AddDays(1), "DP1"), (Pon.AddDays(2), "DP2"));
        (await h.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "promena_tipa")).Should().Be(3);
        await Sut(h, Pon).Invoking(s => s.Handle(new PromeniTipDpCommand(novi[0].Id, "DP2", false), default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Propusten_DP_je_oznacen_kao_propusten_pri_automatskom_prenosu()
    {
        var (h, ana, _, _) = await HarnessAsync();
        await using var _ = h;
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon, "DP1", ana)]), default);

        await Sut(h, Pon).Handle(new PrenesiCommand(Pon), default);

        var p = await h.Db.Pregledi.SingleAsync();
        p.Propusten.Should().BeTrue();
        RasporedHandler.Dto(p, await RasporedKontekst.UcitajAsync(h.Db, default)).Propusten.Should().BeTrue();
    }

    [Fact]
    public async Task Podsetnik_cetvrtkom_i_petkom_gleda_svaki_dan_sledece_nedelje()
    {
        var (h, ana, _, _) = await HarnessAsync();
        await using var _ = h;
        var dir = await h.SeedUserAsync("dir", "Direktor2026!", Roles.Direktor);
        h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = dir.Id, Pravo = Prava.Overa });   // podrazumevani primaoci podsetnika
        await h.Db.SaveChangesAsync();
        // Sledeće nedelje postoji samo ponedeljak (npr. petkov DP prenet) — nedelja nije isplanirana.
        await Sut(h, Pon).Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Pon.AddDays(7), "DP1", ana)]), default);

        var cetvrtak = new DateTime(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);
        await new ZakazaneProvereHandler(h.Db, new FakeClock(cetvrtak)).Handle(new PokreniProvereCommand(), default);
        await new ZakazaneProvereHandler(h.Db, new FakeClock(cetvrtak)).Handle(new PokreniProvereCommand(), default);
        await new ZakazaneProvereHandler(h.Db, new FakeClock(cetvrtak.AddDays(1))).Handle(new PokreniProvereCommand(), default);

        var podsetnici = await h.Db.Obavestenja.Where(o => o.Vrsta == VrsteObavestenja.PredlogRasporeda && o.KorisnikId == dir.Id).ToListAsync();
        podsetnici.Should().HaveCount(2, "jednom u četvrtak i još jednom u petak");
        podsetnici[0].Tekst.Should().Contain("potvrđen za: 17.11., 18.11., 19.11., 20.11.", "ponedeljak već ima DP");
    }
}
