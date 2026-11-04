using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Features.Analitika;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Analitika;

/// <summary>Pravila računanja (R8.7), ekran uređaja (R8.4–R8.6), ispunjenje plana (R8.9), Dashboard (R8.10–R8.11) i pretraga (R8.14).</summary>
public class AnalitikaTests
{
    private static readonly DateOnly D9 = new(2026, 11, 9);

    // ─── Čista pravila ────────────────────────────────────────────────────

    [Fact]
    public void Izmedju_dva_pregleda_vazi_stanje_sa_poslednjeg()
    {
        var ist = AnalitikaPravila.Istorija(
            [new StanjeNaDan(D9.AddDays(-5), StanjeUredjaja.Ispravan), new StanjeNaDan(D9.AddDays(2), StanjeUredjaja.Neispravan)], D9, D9.AddDays(3));
        ist.Select(x => x.Stanje).Should().Equal(StanjeUredjaja.Ispravan, StanjeUredjaja.Ispravan, StanjeUredjaja.Neispravan, StanjeUredjaja.Neispravan);
        AnalitikaPravila.Istorija([], D9, D9).Single().Stanje.Should().BeNull();
    }

    [Fact]
    public void Kvar_traje_do_prvog_ispravnog_a_van_funkcije_se_ne_racuna()
    {
        StanjeUredjaja? I = StanjeUredjaja.Ispravan, N = StanjeUredjaja.Neispravan, V = StanjeUredjaja.VanFunkcije;
        var dani = new[] { null, I, I, I, N, N, V, N, I, I, N };
        var a = AnalitikaPravila.Izracunaj(dani.Select((s, i) => (D9.AddDays(i), s)).ToList());
        a.Trajanja.Should().Equal(3, 1); // van funkcije između dva „neispravan“ ne prekida ni ne produžava kvar
        a.BrojKvarova.Should().Be(2);
        a.ProsekTrajanja.Should().Be(2);
        a.NajduzeBezKvara.Should().Be(3);
        a.DanaBezKvaraSada.Should().Be(0);
        a.DanaOdPoslednjegKvara.Should().Be(2);
    }

    [Fact]
    public void Rok_overe_ne_broji_vikend()
    {
        var petak = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
        AnalitikaPravila.SatiRadnihDana(petak, petak.AddDays(3)).Should().Be(24);
        AnalitikaPravila.SatiRadnihDana(petak, petak.AddHours(-1)).Should().Be(0);
    }

    // ─── Kroz popunjavanje ────────────────────────────────────────────────

    private sealed class Citac : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) =>
        [
            new(2, "DP1", "10", 1, "Transporter", "10-C-08", "Pregled rolni", "Okreće", "Ne okreće", ""),
            new(3, "DP1", "10", 2, "Elevator", "10-C-13", "Pregled rada zatezne sekcije", "Ispravno", "Neispravno", ""),
        ];
    }

    private sealed record Okr(IdentityHarness H, string Ana, FakeBrojac Brojac, int[] Stavke, int Transporter, int Elevator, int[] Pregledi);

    /// <summary>
    /// 9.11. Transporter neispravan; 11.11. (prenet sa 10.) sve ispravno; 12.11. ispravno, Elevator van funkcije; 13.11. nije urađen (Ana odsutna).
    /// </summary>
    private static async Task<Okr> PripremiAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = ana.Id, Pravo = Prava.PreglediPopunjavanje });
        await new UvoziSabloneHandler(h.Db, new Citac(), new FakeCurrentUser(), new FakeClock()).Handle(new UvoziSabloneCommand(Stream.Null), default);
        var stavke = await h.Db.SablonStavke.OrderBy(s => s.Redosled).ToListAsync();
        var o = new Okr(h, ana.Id, new FakeBrojac(), stavke.Select(s => s.Id).ToArray(), stavke[0].UredjajId, stavke[1].UredjajId, new int[4]);

        o.Pregledi[0] = await PregledAsync(o, "DP1-2026-0001", D9, D9,
            ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "7 rolni" }), ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }));
        o.Pregledi[1] = await PregledAsync(o, "DP1-2026-0002", D9.AddDays(1), D9.AddDays(2),
            ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }), ("odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }));
        o.Pregledi[2] = await PregledAsync(o, "DP1-2026-0003", D9.AddDays(3), D9.AddDays(3),
            ("odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }), ("van_funkcije", new { uredjajId = o.Elevator, vanFunkcije = true }));
        var p4 = new Pregled { Broj = "DP1-2026-0004", Tip = "DP1", PlaniraniDatum = D9.AddDays(4), PrvobitniDatum = D9.AddDays(4), DodeljenId = o.Ana };
        h.Db.Pregledi.Add(p4);
        h.Db.Odsustva.Add(new Odsustvo { KorisnikId = o.Ana, Od = D9.AddDays(4), Do = D9.AddDays(4), Status = "odobreno" });
        await h.Db.SaveChangesAsync();
        o.Pregledi[3] = p4.Id;

        // Vremena predaje i overe: 1. na dan i overen za 21 h, 2. kasnije (prenet), 3. na dan, neoveren.
        var pr = await h.Db.Pregledi.OrderBy(p => p.Id).ToListAsync();
        pr[0].PredatAt = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        pr[0].OverenoAt = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
        pr[0].Status = StatusPregleda.Overen;
        pr[1].PredatAt = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        pr[2].PredatAt = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        await h.Db.SaveChangesAsync();
        return o;
    }

    private static async Task<int> PregledAsync(Okr o, string broj, DateOnly prvobitni, DateOnly planirani, params (string Vrsta, object Podaci)[] ops)
    {
        var p = new Pregled { Broj = broj, Tip = "DP1", PlaniraniDatum = planirani, PrvobitniDatum = prvobitni, DodeljenId = o.Ana };
        o.H.Db.Pregledi.Add(p);
        await o.H.Db.SaveChangesAsync();
        var sut = new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(), o.Brojac);
        var jo = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var sve = new[] { ("zapocni", (object)new { }) }.Concat(ops).Append(("predaja", new { }));
        var r = await sut.Handle(new SinhronizacijaCommand(null, null, sve.Select(x => new OperacijaInput(Guid.NewGuid(), x.Item1, p.Id,
            new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc), JsonSerializer.SerializeToElement(x.Item2, jo))).ToList()), default);
        r.Odbijene.Should().BeEmpty();
        return p.Id;
    }

    private static readonly FakeClock Sat14 = new(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc));

    private static UredjajEkranHandler Ekran(Okr o) => new(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, Sat14);

    [Fact]
    public async Task Ekran_uredjaja_daje_istoriju_analitiku_preglede_i_dosije()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;

        var e = await Ekran(o).Handle(new GetUredjajEkranQuery(o.Transporter, 30), default);
        e.Status.Should().Be("ok");
        e.DanaOdPregleda.Should().Be(2);
        e.Sabloni.Should().Equal("DP1");
        e.Istorija.Should().HaveCount(30);
        e.Istorija.Single(d => d.Dan == D9.AddDays(1)).Stanje.Should().Be("neispravan");
        e.Istorija.Last().Stanje.Should().Be("ispravan");
        e.Analitika.Trajanja.Should().Equal(2);
        e.Analitika.DanaBezKvaraSada.Should().Be(4);
        e.Analitika.DanaOdPoslednjegKvara.Should().Be(3);
        e.NajcescaStavka.Should().Be("Pregled rolni");
        e.Pregledi.Select(p => p.Stanje).Should().Equal("ispravan", "ispravan", "neispravan");
        e.Dosije.Single().Napomena.Should().Be("7 rolni");
        e.Kvarovi.Single().Zatvoren.Should().Be(D9.AddDays(2));

        (await Ekran(o).Handle(new GetUredjajEkranQuery(o.Elevator), default)).Status.Should().Be("van_funkcije");
        (await Ekran(o).Handle(new GetUredjajEkranQuery(o.Transporter, 1000), default)).Istorija.Should().HaveCount(365);
        (await Ekran(o).Handle(new GetUredjajPoQrQuery(await o.H.Db.Uredjaji.Where(u => u.Id == o.Elevator).Select(u => u.QrToken).SingleAsync()), default))
            .Should().Be(o.Elevator);
    }

    [Fact]
    public async Task Ponisten_pregled_ne_ulazi_u_istoriju_ali_se_vidi_u_listi()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        (await o.H.Db.Pregledi.SingleAsync(p => p.Id == o.Pregledi[0])).Status = StatusPregleda.Ponisten;
        await o.H.Db.SaveChangesAsync();

        var e = await Ekran(o).Handle(new GetUredjajEkranQuery(o.Transporter, 30), default);
        e.Istorija.Single(d => d.Dan == D9).Stanje.Should().BeNull();
        e.Pregledi.Should().HaveCount(3);
    }

    [Fact]
    public async Task Ispunjenje_plana_broji_na_dan_kasnije_propusteno_preneto_i_overu_u_roku_bez_dana_odsustva()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;

        var r = await new AnalitikaHandler(o.H.Db, Sat14).Handle(new GetIspunjenjeQuery(new DateOnly(2026, 11, 1), null), default);
        r.Ukupno.Should().BeEquivalentTo(new IspunjenjeRedDto("Ukupno", 4, 2, 1, 1, 1, 50, 1, 1, 33.3, 21));
        r.PoMesecu.Single().Naziv.Should().Be("2026-11");
        r.PoInzenjeru.Single().Planirano.Should().Be(3); // 13.11. je bila na odsustvu
    }

    [Fact]
    public async Task Opsta_analitika_broji_kvarove_trajanje_i_van_funkcije()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;

        var a = await new AnalitikaHandler(o.H.Db, Sat14).Handle(new GetOpstaAnalitikaQuery(null, null), default);
        a.Kvarova.Should().Be(1);
        a.ProsekTrajanjaKvara.Should().Be(2);
        a.PoUredjaju.Single().Id.Should().Be(o.Transporter);
        a.PoMesecu.Single().Naziv.Should().Be("2026-11");
        a.VanFunkcije.Single().Naziv.Should().Be("Elevator 10-C-13");
    }

    [Fact]
    public async Task Dashboard_daje_moje_aktivnosti_inzenjeru_i_uredjaje_van_funkcije()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        o.H.Db.Pregledi.Add(new Pregled { Broj = "DP1-2026-0005", Tip = "DP1", PlaniraniDatum = D9.AddDays(6), PrvobitniDatum = D9.AddDays(6), DodeljenId = o.Ana });
        await o.H.Db.SaveChangesAsync();

        var d = await new DashboardHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana, Roles = [Roles.Inzenjer] }, Sat14).Handle(new GetDashboardQuery(), default);
        d.Moje.Should().NotBeNull();
        d.Moje!.Narednih7.Single().Broj.Should().Be("DP1-2026-0005");
        d.CekaOdluku.Should().BeNull();
        d.CekajuOveru.Select(p => p.Broj).Should().Equal("DP1-2026-0002", "DP1-2026-0003");
        d.VanFunkcije.Single().Should().Be(new VanFunkcijeDto(o.Elevator, "Elevator 10-C-13", D9.AddDays(3), 2));
        d.Kpi.VanFunkcije.Should().Be(1);
    }

    [Fact]
    public async Task Pretraga_trazi_uredjaje_i_brojeve_od_dva_znaka()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sut = new PretragaHandler(o.H.Db);

        (await sut.Handle(new PretragaQuery("C-1"), default)).Select(r => r.Tekst).Should().Equal("Elevator 10-C-13");
        (await sut.Handle(new PretragaQuery("dp1-2026-000"), default)).Should().HaveCount(4).And.OnlyContain(r => r.Vrsta == "pregled");
        (await sut.Handle(new PretragaQuery("k-2026"), default)).Single().Vrsta.Should().Be("kvar");
        (await sut.Handle(new PretragaQuery("1"), default)).Should().BeEmpty();
    }
}
