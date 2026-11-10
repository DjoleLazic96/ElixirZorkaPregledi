using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Popunjavanje;

/// <summary>Popunjavanje (R4.1–R4.6, R5.2, R3.15): početak, operacije, idempotentnost, predaja, aktivno vreme, slike.</summary>
public class PopunjavanjeTests
{
    private static readonly DateTime T0 = new(2026, 11, 9, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Aktivno_vreme_ne_racuna_pauzu_vecu_od_granice_i_deli_na_delove()
    {
        var vremena = new[] { T0, T0.AddMinutes(2), T0.AddMinutes(5), T0.AddMinutes(40), T0.AddMinutes(41) };
        AktivnoVreme.Izracunaj(vremena, TimeSpan.FromMinutes(10)).Should().Be((6 * 60, 2));
        AktivnoVreme.Izracunaj([], TimeSpan.FromMinutes(10)).Should().Be((0, 0));
        AktivnoVreme.Izracunaj([T0], TimeSpan.FromMinutes(10)).Should().Be((0, 1));
    }

    private sealed class FakeSkladiste : IFajlSkladiste
    {
        public Task<Fajl> SacuvajAsync(byte[] sadrzaj, string mime, CancellationToken ct) =>
            Task.FromResult(new Fajl { Putanja = Guid.NewGuid() + ".jpg", Mime = mime, Velicina = sadrzaj.Length, Sha256 = "x", KreiranoAt = T0 });

        public Stream Otvori(Fajl fajl) => new MemoryStream([1]);
    }

    private sealed record Okruzenje(IdentityHarness H, string Ana, string Bora, int PregledId, int[] Stavke, int UredjajId);

    /// <summary>Šablon PZ1 sa dve sekcije (20: redler sa 2 aktivnosti, 30: puž), PZ dodeljen Ani.</summary>
    private static async Task<Okruzenje> PripremiAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var bora = await h.SeedUserAsync("bora", "Inzenjer2026!", Roles.Inzenjer);
        await new UvoziSabloneHandler(h.Db, new Citac(), new FakeCurrentUser(), new FakeClock()).Handle(new UvoziSabloneCommand(Stream.Null), default);
        var p = new Pregled { Broj = "PZ1-2026-0001", Tip = "PZ1", PlaniraniDatum = new(2026, 11, 9), PrvobitniDatum = new(2026, 11, 9), DodeljenId = ana.Id };
        h.Db.Pregledi.Add(p);
        await h.Db.SaveChangesAsync();
        var stavke = await h.Db.SablonStavke.OrderBy(s => s.Redosled).Select(s => s.Id).ToArrayAsync();
        var redler = await h.Db.Uredjaji.Where(u => u.Oznaka == "20-CR-05").Select(u => u.Id).SingleAsync();
        return new Okruzenje(h, ana.Id, bora.Id, p.Id, stavke, redler);
    }

    private sealed class Citac : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) =>
        [
            new(2, "PZ1", "20", 1, "Redler", "20-CR-05", "Pregled lanca", "Neoštećeno", "Oštećeno", "Broj nedostajućih / oštećenih komada"),
            new(3, "PZ1", "20", 2, "Redler", "20-CR-05", "Pregled skija", "Neoštećeno", "Oštećeno", ""),
            new(4, "PZ1", "30", 3, "Puž", "30-F-01 M2", "Pregled ležajeva i spojnice", "Ispravno", "Neispravno", ""),
        ];
    }

    private static PopunjavanjeHandler Sut(Okruzenje o, string korisnik) => new(o.H.Db, new FakeCurrentUser { UserId = korisnik }, new FakeClock(), new FakeBrojac());

    private static OperacijaInput Op(Okruzenje o, string vrsta, object podaci, int minut, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), vrsta, o.PregledId, T0.AddMinutes(minut), JsonSerializer.SerializeToElement(podaci, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Fact]
    public async Task Ceo_tok_pocetak_odgovori_nepotpuna_predaja_pa_predaja_sa_aktivnim_vremenom()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sut = Sut(o, o.Ana);

        var pregled = await sut.Handle(new ZapocniPregledCommand(o.PregledId), default);
        pregled.Status.Should().Be("u_radu");
        pregled.Sekcije.Select(s => s.Sifra).Should().Equal("20", "30");
        pregled.Sekcije[0].Uredjaji[0].Stavke[0].Polja.Single().Naziv.Should().Be("Broj nedostajućih / oštećenih komada");

        var r = await sut.Handle(new SinhronizacijaCommand(null, null,
        [
            Op(o, "odgovor", new { stavkaId = o.Stavke[0], vrednost = 1 }, 0),
            Op(o, "odgovor", new { stavkaId = o.Stavke[1], vrednost = 0 }, 1),
            Op(o, "predaja", new { }, 2),
        ]), default);
        r.Prihvacene.Should().HaveCount(2);
        r.Odbijene.Single().Razlog.Should().Be("Pregled još nije potpun: nedostaje 1 odgovora i 1 obaveznih napomena.");

        r = await sut.Handle(new SinhronizacijaCommand(null, null,
        [
            Op(o, "odgovor", new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "2 karike", polja = new Dictionary<string, int> { ["3"] = 2 } }, 3),
            Op(o, "sve_ispravno", new { sekcija = "30" }, 4),
            Op(o, "predaja", new { }, 30),
        ]), default);
        r.Odbijene.Should().BeEmpty();

        var p = await o.H.Db.Pregledi.AsNoTracking().SingleAsync();
        p.Status.Should().Be(StatusPregleda.CekaOveru);
        p.PredatAt.Should().NotBeNull();
        p.AktivnoSekundi.Should().Be(4 * 60, "razmak 4→30 min je pauza (granica 10 min)");
        p.Delova.Should().Be(2);
        (await o.H.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "izmena_odgovora")).Should().Be(1);
        (await o.H.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "sve_ispravno")).Should().Be(1);

        var kasno = await sut.Handle(new SinhronizacijaCommand(null, null, [Op(o, "odgovor", new { stavkaId = o.Stavke[1], vrednost = 1 }, 31)]), default);
        kasno.Odbijene.Single().Razlog.Should().Be("Predat pregled se menja samo kroz doradu.");
    }

    [Fact]
    public async Task Ponovljen_opId_se_ne_primenjuje_dva_puta()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sut = Sut(o, o.Ana);
        await sut.Handle(new ZapocniPregledCommand(o.PregledId), default);
        var id = Guid.NewGuid();

        await sut.Handle(new SinhronizacijaCommand(null, null, [Op(o, "odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }, 0, id)]), default);
        var drugi = await sut.Handle(new SinhronizacijaCommand(null, null, [Op(o, "odgovor", new { stavkaId = o.Stavke[0], vrednost = 1 }, 1, id)]), default);

        drugi.Prihvacene.Should().Equal(id);
        (await o.H.Db.Odgovori.SingleAsync()).Vrednost.Should().Be(0, "ponovljena operacija je već primenjena");
    }

    [Fact]
    public async Task Neispravni_podaci_odbijaju_samo_tu_operaciju_a_sledece_se_primenjuju()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sut = Sut(o, o.Ana);
        await sut.Handle(new ZapocniPregledCommand(o.PregledId), default);

        var bezVrednosti = Op(o, "odgovor", new { stavkaId = o.Stavke[0] }, 0);
        var pogresanTip = Op(o, "odgovor", new { stavkaId = "x", vrednost = 1 }, 1);
        var ispravna = Op(o, "odgovor", new { stavkaId = o.Stavke[1], vrednost = 1 }, 2);
        var r = await sut.Handle(new SinhronizacijaCommand(null, null, [bezVrednosti, pogresanTip, ispravna]), default);

        r.Odbijene.Select(x => x.OpId).Should().Equal(bezVrednosti.OpId, pogresanTip.OpId);
        r.Odbijene.Should().OnlyContain(x => x.Razlog == PopunjavanjeHandler.PorukaNeispravnaOperacija);
        r.Prihvacene.Should().Equal(ispravna.OpId);
        (await o.H.Db.Odgovori.SingleAsync()).StavkaId.Should().Be(o.Stavke[1]);
    }

    [Fact]
    public async Task Uredjaj_van_funkcije_ne_trazi_odgovore_i_vidi_se_na_sledecem_pregledu()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sut = Sut(o, o.Ana);
        await sut.Handle(new ZapocniPregledCommand(o.PregledId), default);

        var r = await sut.Handle(new SinhronizacijaCommand(null, null,
        [
            Op(o, "van_funkcije", new { uredjajId = o.UredjajId, vanFunkcije = true }, 0),
            Op(o, "odgovor", new { stavkaId = o.Stavke[2], vrednost = 0 }, 1),
            Op(o, "predaja", new { }, 2),
        ]), default);
        r.Odbijene.Should().BeEmpty();

        var sledeci = new Pregled { Broj = "PZ1-2026-0002", Tip = "PZ1", PlaniraniDatum = new(2026, 12, 1), PrvobitniDatum = new(2026, 12, 1), DodeljenId = o.Ana };
        o.H.Db.Pregledi.Add(sledeci);
        await o.H.Db.SaveChangesAsync();
        var dto = await sut.Handle(new ZapocniPregledCommand(sledeci.Id), default);
        dto.Sekcije[0].Uredjaji[0].ProsliPutVanFunkcije.Should().BeTrue();
        dto.Sekcije[0].Uredjaji[0].VanFunkcije.Should().BeFalse("„nije u funkciji“ se ne prenosi");
    }

    [Fact]
    public async Task PZ_sa_dva_inzenjera_svako_upisuje_samo_u_svoje_sekcije()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var p = await o.H.Db.Pregledi.Include(x => x.Ucesnici).SingleAsync();
        p.Ucesnici.Add(new PregledUcesnik { KorisnikId = o.Bora, Sekcije = ["30"] });
        await o.H.Db.SaveChangesAsync();
        await Sut(o, o.Ana).Handle(new ZapocniPregledCommand(o.PregledId), default);

        var bora = await Sut(o, o.Bora).Handle(new SinhronizacijaCommand(null, null,
            [Op(o, "odgovor", new { stavkaId = o.Stavke[0], vrednost = 0 }, 0), Op(o, "odgovor", new { stavkaId = o.Stavke[2], vrednost = 0 }, 1)]), default);
        bora.Prihvacene.Should().HaveCount(1);
        bora.Odbijene.Single().Razlog.Should().StartWith("Sekciju 20 radi");

        var ana = await Sut(o, o.Ana).Handle(new SinhronizacijaCommand(null, null, [Op(o, "predaja", new { }, 2)]), default);
        ana.Odbijene.Single().Razlog.Should().StartWith("PZ se predaje kad su sve sekcije gotove. Nedostaje: sekcija 20");
    }

    [Fact]
    public async Task Tudji_pregled_403_a_sesta_slika_422()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        await FluentActions.Awaiting(() => Sut(o, o.Bora).Handle(new ZapocniPregledCommand(o.PregledId), default)).Should().ThrowAsync<ForbiddenException>();
        await Sut(o, o.Ana).Handle(new ZapocniPregledCommand(o.PregledId), default);

        var slike = new FotografijeHandler(o.H.Db, new FakeSkladiste(), new FakeCurrentUser { UserId = o.Ana }, new FakeClock());
        for (var i = 0; i < FotografijeHandler.NajviseSlika; i++)
            await slike.Handle(new SacuvajFotografijuCommand(Guid.NewGuid(), o.PregledId, o.Stavke[0], "kamera", [1, 2], "image/jpeg"), default);
        var ista = Guid.NewGuid();
        var sesta = () => slike.Handle(new SacuvajFotografijuCommand(ista, o.PregledId, o.Stavke[0], "galerija", [1], "image/jpeg"), default);
        (await sesta.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal("Najviše 5 slika po stavci.");
        (await o.H.Db.Fotografije.CountAsync()).Should().Be(5);
    }
}

/// <summary>Rad bez interneta (R10.1–R10.5, R6.8, R5.1): paket, početak sa telefona, jedan telefon, sporan unos, sat.</summary>
public class RadBezInternetaTests
{
    private static readonly DateTime T0 = new(2026, 11, 9, 7, 0, 0, DateTimeKind.Utc);

    private static OperacijaInput Op(int pregledId, string vrsta, object podaci, int minut) =>
        new(Guid.NewGuid(), vrsta, pregledId, T0.AddMinutes(minut), JsonSerializer.SerializeToElement(podaci, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private sealed class Citac : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) =>
            [new(2, "DP1", "10", 1, "Elevator", "10-C-13", "Pregled rada zatezne sekcije", "Ispravno", "Neispravno", "")];
    }

    private static async Task<(IdentityHarness H, string Ana, string Bora, int PregledId)> PripremiAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var bora = await h.SeedUserAsync("bora", "Inzenjer2026!", Roles.Inzenjer);
        await new UvoziSabloneHandler(h.Db, new Citac(), new FakeCurrentUser(), new FakeClock()).Handle(new UvoziSabloneCommand(Stream.Null), default);
        var danas = new FakeClock().Today;
        var p = new Pregled { Broj = "DP1-2026-0001", Tip = "DP1", PlaniraniDatum = danas, PrvobitniDatum = danas, DodeljenId = ana.Id };
        h.Db.Pregledi.Add(p);
        await h.Db.SaveChangesAsync();
        return (h, ana.Id, bora.Id, p.Id);
    }

    [Fact]
    public async Task Paket_sadrzi_planiran_pregled_sa_vazecom_verzijom_a_pocetak_sa_telefona_ga_vezuje()
    {
        var (h, ana, _, id) = await PripremiAsync();
        await using var _ = h;
        var sut = new PopunjavanjeHandler(h.Db, new FakeCurrentUser { UserId = ana }, new FakeClock(), new FakeBrojac());

        var paket = await sut.Handle(new GetMojPaketQuery(), default);
        var p = paket.Pregledi.Single();
        p.Status.Should().Be("planiran");
        p.VerzijaId.Should().NotBeNull();
        p.Sekcije.Single().Uredjaji.Single().Stavke.Should().ContainSingle();

        var r = await sut.Handle(new SinhronizacijaCommand("telefon-1", T0, [Op(id, "zapocni", new { verzijaId = p.VerzijaId }, 0)]), default);
        r.Odbijene.Should().BeEmpty();
        var pregled = await h.Db.Pregledi.AsNoTracking().SingleAsync();
        pregled.UredjajKlijenta.Should().Be("telefon-1");
        pregled.ZapocetAt.Should().Be(T0, "vreme početka je vreme sa telefona");

        var drugi = await sut.Handle(new SinhronizacijaCommand("telefon-2", null,
            [Op(id, "odgovor", new { stavkaId = p.Sekcije[0].Uredjaji[0].Stavke[0].StavkaId, vrednost = 0 }, 1)]), default);
        drugi.Odbijene.Single().Razlog.Should().Be(PopunjavanjeHandler.PorukaDrugiTelefon);
    }

    [Fact]
    public async Task Operacije_posle_prebacivanja_se_cuvaju_kao_sporan_unos()
    {
        var (h, ana, bora, id) = await PripremiAsync();
        await using var _ = h;
        var p = await h.Db.Pregledi.SingleAsync();
        p.DodeljenId = bora;
        await h.Db.SaveChangesAsync();

        var r = await new PopunjavanjeHandler(h.Db, new FakeCurrentUser { UserId = ana }, new FakeClock(), new FakeBrojac())
            .Handle(new SinhronizacijaCommand("telefon-1", null, [Op(id, "zapocni", new { }, 0)]), default);

        r.Odbijene.Should().ContainSingle();
        (await h.Db.SporniUnosi.SingleAsync()).KorisnikId.Should().Be(ana);
    }

    [Fact]
    public async Task Sat_telefona_koji_odstupa_vise_od_dozvoljenog_se_belezi()
    {
        var (h, ana, _, id) = await PripremiAsync();
        await using var _ = h;
        var clock = new FakeClock();
        await new PopunjavanjeHandler(h.Db, new FakeCurrentUser { UserId = ana }, clock, new FakeBrojac())
            .Handle(new SinhronizacijaCommand("telefon-1", clock.UtcNow.AddMinutes(-30), [Op(id, "zapocni", new { }, 0)]), default);

        (await h.Db.PregledDogadjaji.CountAsync(d => d.Vrsta == "odstupanje_sata")).Should().Be(1);
    }
}
