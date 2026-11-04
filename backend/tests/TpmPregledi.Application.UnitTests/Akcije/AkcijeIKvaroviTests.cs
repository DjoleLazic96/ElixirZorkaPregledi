using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Akcije;
using TpmPregledi.Application.Features.Popunjavanje;
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
        var sut = new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(), o.Brojac);
        var jo = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return await sut.Handle(new SinhronizacijaCommand(null, null,
            ops.Select(x => new OperacijaInput(Guid.NewGuid(), x.Vrsta, pregledId, new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc), JsonSerializer.SerializeToElement(x.Podaci, jo))).ToList()), default);
    }

    private static AkcijeIKvaroviHandler Ekran(Okr o) => new(o.H.Db, new FakeCurrentUser { UserId = o.Ana, Roles = [Roles.Supervizor] }, new FakeClock());

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

        var akcija = (await Ekran(o).Handle(new GetAkcijeQuery(new AkcijeFilteri()), default)).Single();
        akcija.Broj.Should().Be("A-2026-0001");
        akcija.Rok.Should().Be(new DateOnly(2026, 11, 16));
        akcija.KvarBroj.Should().Be("K-2026-0001");
        akcija.KvarOtvoren.Should().BeTrue();

        // Sledeći pregled: uređaj nosi otvorenu akciju za pop-up.
        var p2 = await NoviPregledAsync(o, "DP1-2026-0002", new DateOnly(2026, 11, 11));
        await PosaljiAsync(o, p2, ("zapocni", new { }));
        var dto = await new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(), o.Brojac).Handle(new GetPopunjavanjeQuery(p2), default);
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
        (await Ekran(o).Handle(new GetKvarQuery(kvar.Id), default)).Prijave.Single().Napomena.Should().Be("7 rolni");
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
