using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Raspored;

/// <summary>Zamerka 68 (subota ili nedelja kao radni dan, DP samo ručno) i 69 (poruka zašto DP ne može tog dana).</summary>
public class Zamerke68i69Tests
{
    // 2026-11-09 je ponedeljak; 14.11. subota.
    private static readonly DateOnly Pon = new(2026, 11, 9);
    private static readonly DateOnly Sub = Pon.AddDays(5);
    private static FakeClock Sat() => new(Pon.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc));

    private sealed record Okruzenje(IdentityHarness H, string Direktor, string Sup, string Ana, string Bora) : IAsyncDisposable
    {
        public RasporedHandler Raspored => new(H.Db, new FakeBrojac(), new FakeCurrentUser { UserId = Direktor }, Sat());
        public NeradniDaniHandler Neradni => new(H.Db, new FakeCurrentUser { UserId = Direktor }, Sat());
        public ValueTask DisposeAsync() => H.DisposeAsync();
    }

    private static async Task<Okruzenje> OkruzenjeAsync(bool iSledecaNedelja = false)
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var dir = await h.SeedUserAsync("dir", "Direktor2026!", Roles.Direktor);
        var sup = await h.SeedUserAsync("sup", "Supervizor2026!", Roles.Supervizor);
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var bora = await h.SeedUserAsync("bora", "Inzenjer2026!", Roles.Inzenjer);
        dir.FullName = "Dragan";
        h.Db.KorisnikPrava.AddRange(
            new KorisnikPravo { KorisnikId = dir.Id, Pravo = Prava.Overa }, new KorisnikPravo { KorisnikId = sup.Id, Pravo = Prava.Overa });
        foreach (var (u, ime) in new[] { (ana, "Ana"), (bora, "Bora") })
        {
            u.FullName = ime;
            h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = u.Id, Pravo = Prava.PreglediPopunjavanje });
        }
        await h.Db.SaveChangesAsync();
        var o = new Okruzenje(h, dir.Id, sup.Id, ana.Id, bora.Id);
        var dani = Enumerable.Range(0, 5).Select(i => Pon.AddDays(i)).ToList();
        if (iSledecaNedelja) dani.AddRange(Enumerable.Range(7, 5).Select(i => Pon.AddDays(i)));
        await o.Raspored.Handle(new PotvrdiRasporedCommand(dani.Select((d, i) => new StavkaRasporeda(d, "DP1", i % 2 == 0 ? ana.Id : bora.Id)).ToList()), default);
        return o;
    }

    [Fact]
    public async Task Poruka_kaze_zasto_dp_ne_moze_tog_dana_zamerka_69()
    {
        await using var o = await OkruzenjeAsync();
        await o.Neradni.Handle(new UnesiNeradneDaneCommand(Pon.AddDays(10), Pon.AddDays(10), "Dan fabrike", "fabrika"), default);
        var petak = await o.H.Db.Pregledi.SingleAsync(p => p.PlaniraniDatum == Pon.AddDays(4));

        var subota = () => o.Raspored.Handle(new IzmeniDodeluCommand(petak.Id, null, Sub), default);
        (await subota.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should()
            .ContainSingle(e => e.StartsWith("14.11.2026 je subota — DP se radi samo radnim danima."));
        var praznik = () => o.Raspored.Handle(new IzmeniDodeluCommand(petak.Id, null, Pon.AddDays(10)), default);
        (await praznik.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should()
            .ContainSingle(e => e == "19.11.2026 je neradni dan (Dan fabrike) — tada se DP ne radi.");
    }

    [Fact]
    public async Task Radni_dan_se_unosi_samo_za_vikend_i_javlja_se_supervizoru_zamerka_68()
    {
        await using var o = await OkruzenjeAsync();
        var radniDan = () => o.Neradni.Handle(new UnesiNeradneDaneCommand(Pon.AddDays(4), Sub, "Remont", NeradniDan.VrstaRadni), default);
        await radniDan.Should().ThrowAsync<BusinessRuleException>("petak je već radni dan");

        await o.Neradni.Handle(new UnesiNeradneDaneCommand(Sub, Sub, "Remont", NeradniDan.VrstaRadni), default);

        var obavestenje = await o.H.Db.Obavestenja.SingleAsync(x => x.Vrsta == VrsteObavestenja.PredlogRasporeda);
        obavestenje.KorisnikId.Should().Be(o.Sup, "Direktor koji je uneo ne dobija svoje obaveštenje");
        obavestenje.Naslov.Should().Be("Subota 14.11.2026 je radni dan");
        obavestenje.Tekst.Should().Contain("Uneto: Dragan");

        var raspored = await o.Raspored.Handle(new GetRasporedQuery(Pon, Pon.AddDays(6)), default);
        raspored.RadniDani.Should().ContainSingle(d => d.Datum == Sub);
        raspored.NeradniDani.Should().BeEmpty();
    }

    [Fact]
    public async Task Predlog_i_prenos_ne_popunjavaju_radni_vikend_dan_zamerka_68()
    {
        await using var o = await OkruzenjeAsync();
        await o.Neradni.Handle(new UnesiNeradneDaneCommand(Sub, Sub, "Remont", NeradniDan.VrstaRadni), default);

        var predlog = await o.Raspored.Handle(new PredlogRasporedaQuery(Sub), default);
        predlog.Select(s => s.Datum).Should().NotContain(Sub).And.Contain(Pon.AddDays(7));

        // Propušten petak ide na ponedeljak, ne na radnu subotu.
        await o.Raspored.Handle(new PrenesiCommand(Pon.AddDays(4)), default);
        (await o.H.Db.Pregledi.AnyAsync(p => p.PlaniraniDatum == Sub)).Should().BeFalse();
        (await o.H.Db.Pregledi.AnyAsync(p => p.PlaniraniDatum == Pon.AddDays(7))).Should().BeTrue();
    }

    [Fact]
    public async Task Rucno_dodat_dp_na_radnu_subotu_predlaze_sledeci_a_tip_moze_da_se_promeni_zamerka_68()
    {
        await using var o = await OkruzenjeAsync();
        var bezRadnog = () => o.Raspored.Handle(new PredlogDanaQuery(Sub), default);
        await bezRadnog.Should().ThrowAsync<BusinessRuleException>();
        await o.Neradni.Handle(new UnesiNeradneDaneCommand(Sub, Sub, "Remont", NeradniDan.VrstaRadni), default);

        var predlog = await o.Raspored.Handle(new PredlogDanaQuery(Sub), default);
        predlog.Tip.Should().Be("DP2", "u petak je DP1");
        predlog.InzenjerId.Should().Be(o.Bora, "posle Ane u petak ide Bora po rotaciji");
        predlog.Rupa.Should().BeFalse("posle subote nema planiranih DP-ova");

        await o.Raspored.Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Sub, "DP1", o.Ana)], PostujTip: true), default);
        var dp = await o.H.Db.Pregledi.SingleAsync(p => p.PlaniraniDatum == Sub);
        dp.Tip.Should().Be("DP1", "izabrani tip važi kad je ručno promenjen");
        dp.DodeljenId.Should().Be(o.Ana);
        (await o.H.Db.Obavestenja.AnyAsync(x => x.KorisnikId == o.Ana && x.Naslov == "Novi pregledi u rasporedu")).Should().BeTrue();
    }

    [Fact]
    public async Task Umetni_na_radnu_subotu_pomera_naredne_da_smena_ostane_pravilna_zamerka_68()
    {
        await using var o = await OkruzenjeAsync(iSledecaNedelja: true);
        await o.Neradni.Handle(new UnesiNeradneDaneCommand(Sub, Sub, "Remont", NeradniDan.VrstaRadni), default);
        var ponedeljak = await o.H.Db.Pregledi.AsNoTracking().SingleAsync(p => p.PlaniraniDatum == Pon.AddDays(7));

        var predlog = await o.Raspored.Handle(new PredlogDanaQuery(Sub), default);
        predlog.Rupa.Should().BeTrue();
        await o.Raspored.Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Sub, predlog.Tip, predlog.InzenjerId)], Uskladi: true), default);

        var tipovi = await o.H.Db.Pregledi.Where(p => p.Tip.StartsWith("DP") && p.PlaniraniDatum >= Pon.AddDays(4))
            .OrderBy(p => p.PlaniraniDatum).Select(p => p.Tip).ToListAsync();
        tipovi.Should().Equal("DP1", "DP2", "DP1", "DP2", "DP1", "DP2", "DP1");
        (await o.H.Db.Pregledi.SingleAsync(p => p.Id == ponedeljak.Id)).PlaniraniDatum.Should().Be(Sub, "subota preuzima sledeći DP iz niza");
    }

    [Fact]
    public async Task Brisanje_radnog_dana_pomera_dp_sa_njega_zamerka_68()
    {
        await using var o = await OkruzenjeAsync();
        await o.Neradni.Handle(new UnesiNeradneDaneCommand(Sub, Sub, "Remont", NeradniDan.VrstaRadni), default);
        await o.Raspored.Handle(new PotvrdiRasporedCommand([new StavkaRasporeda(Sub, "DP2", o.Bora)]), default);
        var id = (await o.H.Db.NeradniDani.SingleAsync()).Id;

        (await o.Neradni.Handle(new ObrisiNeradniDanCommand(id, PomeriDp: true), default)).Should().BeNull();

        (await o.H.Db.Pregledi.AnyAsync(p => p.PlaniraniDatum == Sub)).Should().BeFalse();
        (await o.H.Db.Pregledi.SingleAsync(p => p.Tip == "DP2" && p.PlaniraniDatum > Sub)).PlaniraniDatum.Should().Be(Pon.AddDays(7));
    }

    [Fact]
    public void Rok_za_overu_racuna_radnu_subotu_zamerka_68()
    {
        var petak = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);
        AnalitikaPravila.SatiRadnihDana(petak, petak.AddDays(3)).Should().Be(24);
        AnalitikaPravila.SatiRadnihDana(petak, petak.AddDays(3), new HashSet<DateOnly> { Sub }).Should().Be(48);
    }
}
