using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Pregledi;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Izvestaji;

namespace TpmPregledi.Application.UnitTests.Pregledi;

/// <summary>Overa, dorada, poništavanje, sporan unos i izveštaji (R6.1–R6.10).</summary>
public class OveraTests
{
    private sealed class Citac : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) =>
        [
            new(2, "DP1", "10", 1, "Elevator", "10-C-13", "Pregled rada zatezne sekcije", "Ispravno", "Neispravno", ""),
            new(3, "DP1", "10", 2, "Transporter", "10-C-08", "Pregled rolni", "Okreće", "Ne okreće", "Broj rolni koje ne okreću"),
        ];
    }

    private sealed record Okr(IdentityHarness H, string Ana, string Sup, string Dir, int PregledId, int[] Stavke);

    /// <summary>DP1 koji je Ana popunila i predala (jedna stavka neispravna).</summary>
    private static async Task<Okr> PredatAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var sup = await h.SeedUserAsync("sup", "Supervizor2026!", Roles.Supervizor);
        var dir = await h.SeedUserAsync("dir", "Direktor2026!", Roles.Direktor);
        h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = ana.Id, Pravo = Prava.PreglediPopunjavanje });
        await h.Db.SaveChangesAsync();
        await new UvoziSabloneHandler(h.Db, new Citac(), new FakeCurrentUser(), new FakeClock()).Handle(new UvoziSabloneCommand(Stream.Null), default);
        var p = new Pregled { Broj = "DP1-2026-0001", Tip = "DP1", PlaniraniDatum = new(2026, 11, 9), PrvobitniDatum = new(2026, 11, 9), DodeljenId = ana.Id };
        h.Db.Pregledi.Add(p);
        await h.Db.SaveChangesAsync();
        var stavke = await h.Db.SablonStavke.OrderBy(s => s.Redosled).Select(s => s.Id).ToArrayAsync();

        var popuni = new PopunjavanjeHandler(h.Db, new FakeCurrentUser { UserId = ana.Id }, new FakeClock(FakeClock.PosleRasporeda), new FakeBrojac());
        OperacijaInput Op(string v, object pod) => new(Guid.NewGuid(), v, p.Id, DateTime.UtcNow, JsonSerializer.SerializeToElement(pod, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var r = await popuni.Handle(new SinhronizacijaCommand(null, null,
        [
            Op("zapocni", new { }),
            Op("odgovor", new { stavkaId = stavke[0], vrednost = 0 }),
            Op("odgovor", new { stavkaId = stavke[1], vrednost = 1, napomena = "5 rolni", polja = new Dictionary<string, int> { ["1"] = 5 } }),
            Op("predaja", new { }),
        ]), default);
        r.Odbijene.Should().BeEmpty();
        return new Okr(h, ana.Id, sup.Id, dir.Id, p.Id, stavke);
    }

    private static PreglediHandler Kao(Okr o, string id, string rola) =>
        new(o.H.Db, new FakeCurrentUser { UserId = id, Roles = [rola] }, new FakeClock());

    [Fact]
    public async Task Detalj_pokazuje_odgovore_polja_i_rezime_a_izvestaji_nisu_prazni()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        var d = await Kao(o, o.Sup, Roles.Supervizor).Handle(new GetPregledQuery(o.PregledId), default);

        d.Status.Should().Be("ceka_overu");
        var st = d.Sekcije.Single().Uredjaji[1].Stavke.Single();
        st.Odgovor.Should().Be("Ne okreće");
        st.Polja.Should().Equal("Broj rolni koje ne okreću: 5 kom.");
        d.Istorija.Select(x => x.Vrsta).Should().Contain(["pocetak", "predaja"]);

        var izv = new IzvestajPregleda();
        izv.Pdf(d).Should().HaveCountGreaterThan(1000);
        izv.Docx(d).Should().HaveCountGreaterThan(1000);
        // Zamerka 62: rezime je kraći od celog pregleda (bez tabela po sekcijama), ali ima neispravnu stavku.
        izv.Pdf(d, rezime: true).Length.Should().BeLessThan(izv.Pdf(d).Length);
        using (var w = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(izv.Docx(d, rezime: true)), false))
        {
            var tekst = w.MainDocumentPart!.Document.Body!.InnerText;
            tekst.Should().Contain("— rezime").And.Contain("Ne okreće").And.NotContain("Uređaj" + "Aktivnost");
            w.MainDocumentPart.Document.Body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>().Should().HaveCount(1, "samo zaglavlje");
        }
        var arhiva = await Kao(o, o.Sup, Roles.Supervizor).Handle(new GetPreglediQuery(new PreglediFilteri(null, null, null, null, null, null)), default);
        arhiva.Stavke.Single().Neispravnih.Should().Be(1);
        izv.Excel(arhiva.Stavke).Should().HaveCountGreaterThan(1000);

        // Zamerke 72–73: slike su sličice u PDF-u i Word-u — u rezimeu i uz ispravne stavke, u celom pregledu ispod reda stavke.
        var saSlikama = d with
        {
            Sekcije = d.Sekcije.Select(s => s with
            {
                Uredjaji = s.Uredjaji.Select(u => u with { Stavke = u.Stavke.Select(x => x with { Fotografije = x.Neispravan ? [1L] : [2L] }).ToList() }).ToList(),
            }).ToList(),
        };
        var slike = new Dictionary<long, byte[]> { [1] = SlikeIzvestajaTests.Slika(2048, 1536), [2] = SlikeIzvestajaTests.Slika(1536, 2048) };
        izv.Pdf(saSlikama, rezime: true, slike).Length.Should().BeGreaterThan(izv.Pdf(saSlikama, rezime: true).Length);
        using (var w = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(izv.Docx(saSlikama, rezime: true, slike)), false))
        {
            w.MainDocumentPart!.Document.Body!.InnerText.Should().Contain("Napomene i slike uz ispravne stavke").And.NotContain("slika)");
            w.MainDocumentPart.ImageParts.Count().Should().BeGreaterThan(1, "neispravna i bar jedna ispravna stavka");
        }
        using (var w = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(izv.Docx(saSlikama, slike: slike)), false))
            w.MainDocumentPart!.ImageParts.Count().Should().Be(saSlikama.Sekcije.Sum(s => s.Uredjaji.Sum(u => u.Stavke.Count)));
    }

    [Fact]
    public async Task Supervizor_ne_vraca_ni_ne_ponistava_overu_Direktora()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        (await Kao(o, o.Dir, Roles.Direktor).Handle(new OveriCommand(o.PregledId), default)).OverioDirektor.Should().BeTrue();

        var vrati = () => Kao(o, o.Sup, Roles.Supervizor).Handle(new VratiNaDoraduCommand(o.PregledId, o.Ana, "Proveri rolne"), default);
        (await vrati.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(PreglediHandler.PorukaOveraDirektora);
        var ponisti = () => Kao(o, o.Sup, Roles.Supervizor).Handle(new PonistiCommand(o.PregledId, "Greška"), default);
        (await ponisti.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(PreglediHandler.PorukaOveraDirektora);

        (await Kao(o, o.Dir, Roles.Direktor).Handle(new PonistiCommand(o.PregledId, "Greška u rasporedu"), default)).Status.Should().Be("ponisten");
    }

    [Fact]
    public async Task Ponistavanje_obavestava_inzenjera_i_overioca_i_zatvara_zahtev_za_doradu_zamerke_39_40()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        await Kao(o, o.Sup, Roles.Supervizor).Handle(new OveriCommand(o.PregledId), default);
        await Kao(o, o.Ana, Roles.Inzenjer).Handle(new ZatraziDoraduCommand(o.PregledId, "Pogrešan uređaj"), default);
        var preObavestenja = await o.H.Db.Obavestenja.Select(x => x.Id).ToListAsync();

        await Kao(o, o.Dir, Roles.Direktor).Handle(new PonistiCommand(o.PregledId, "Duplikat"), default);

        (await o.H.Db.ZahteviDorade.SingleAsync()).Status.Should().Be("odbijeno");
        var nova = await o.H.Db.Obavestenja.Where(x => !preObavestenja.Contains(x.Id)).ToListAsync();
        nova.Where(x => x.KorisnikId == o.Ana).Select(x => x.Naslov).Should().BeEquivalentTo("Zahtev je zatvoren", "DP1-2026-0001 je poništen");
        nova.Where(x => x.KorisnikId == o.Sup).Select(x => x.Naslov).Should().Equal("DP1-2026-0001 je poništen");
        nova.Should().NotContain(x => x.KorisnikId == o.Dir, "ko poništava ne obaveštava sebe");
    }

    [Fact]
    public async Task Inzenjer_otkazuje_svoj_zahtev_za_doradu_pa_moze_nov_zamerka_47()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        await Kao(o, o.Ana, Roles.Inzenjer).Handle(new ZatraziDoraduCommand(o.PregledId, "Pogrešan uređaj"), default);

        var tudji = () => Kao(o, o.Sup, Roles.Supervizor).Handle(new OtkaziZahtevZaDoraduCommand(o.PregledId), default);
        await tudji.Should().ThrowAsync<ForbiddenException>();
        var d = await Kao(o, o.Ana, Roles.Inzenjer).Handle(new OtkaziZahtevZaDoraduCommand(o.PregledId), default);

        d.ZahteviDorade.Single().Status.Should().Be("otkazano");
        d.Istorija.Select(x => x.Opis).Should().Contain("Zahtev za doradu otkazao inženjer");
        (await Kao(o, o.Ana, Roles.Inzenjer).Handle(new ZatraziDoraduCommand(o.PregledId, "Ipak treba"), default))
            .ZahteviDorade.Should().Contain(z => z.Status == "zahtev");
    }

    [Fact]
    public async Task Svoj_pregled_se_ne_overava_ni_kad_inzenjer_ima_pravo_overa_zamerka_57()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        o.H.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = o.Ana, Pravo = Prava.Overa });
        await o.H.Db.SaveChangesAsync();

        var overi = () => Kao(o, o.Ana, Roles.Inzenjer).Handle(new OveriCommand(o.PregledId), default);
        (await overi.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(PreglediHandler.PorukaSvojPregled);
        var vrati = () => Kao(o, o.Ana, Roles.Inzenjer).Handle(new VratiNaDoraduCommand(o.PregledId, o.Ana, "x"), default);
        (await vrati.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(PreglediHandler.PorukaSvojPregled);
        (await Kao(o, o.Sup, Roles.Supervizor).Handle(new OveriCommand(o.PregledId), default)).Status.Should().Be("overen", "drugi Supervizor overava");
    }

    [Fact]
    public async Task Vrati_na_doradu_kopira_odgovore_a_dorada_ne_menja_broj_ni_aktivno_vreme()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        var pre = await o.H.Db.Pregledi.AsNoTracking().SingleAsync();
        var d = await Kao(o, o.Sup, Roles.Supervizor).Handle(new VratiNaDoraduCommand(o.PregledId, o.Ana, "Dopuni napomenu"), default);

        d.Status.Should().Be("vracen_na_doradu");
        d.Dorada.Should().Be(1);
        d.Broj.Should().Be("DP1-2026-0001");
        d.Sekcije.Single().Uredjaji[1].Stavke.Single().Napomena.Should().Be("5 rolni", "odgovori prethodne dorade su kopirani");

        var r = await new PopunjavanjeHandler(o.H.Db, new FakeCurrentUser { UserId = o.Ana }, new FakeClock(FakeClock.PosleRasporeda), new FakeBrojac()).Handle(new SinhronizacijaCommand(null, null,
            [new(Guid.NewGuid(), "predaja", o.PregledId, DateTime.UtcNow.AddHours(5), JsonSerializer.SerializeToElement(new { }))]), default);
        r.Odbijene.Should().BeEmpty();
        var posle = await o.H.Db.Pregledi.AsNoTracking().SingleAsync();
        posle.Status.Should().Be(StatusPregleda.CekaOveru);
        posle.AktivnoSekundi.Should().Be(pre.AktivnoSekundi, "vreme dorade ne ulazi u aktivno vreme");
    }

    [Fact]
    public async Task Zahtev_za_doradu_posle_overe_odlucuje_onaj_ko_je_overio_ili_Direktor()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        await Kao(o, o.Sup, Roles.Supervizor).Handle(new OveriCommand(o.PregledId), default);
        await Kao(o, o.Ana, Roles.Inzenjer).Handle(new ZatraziDoraduCommand(o.PregledId, "Pogrešan odgovor"), default);

        var drugiSup = await o.H.SeedUserAsync("sup2", "Supervizor2026!", Roles.Supervizor);
        var tudji = () => Kao(o, drugiSup.Id, Roles.Supervizor).Handle(new OdluciODoradiCommand(o.PregledId, true), default);
        await tudji.Should().ThrowAsync<BusinessRuleException>();

        var d = await Kao(o, o.Sup, Roles.Supervizor).Handle(new OdluciODoradiCommand(o.PregledId, true), default);
        d.Status.Should().Be("vracen_na_doradu");
        d.ZahteviDorade.Single().Status.Should().Be("odobreno");
    }

    [Fact]
    public async Task Sporan_odgovor_se_primenjuje_ili_odbacuje()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        o.H.Db.SporniUnosi.Add(new SporanUnos
        {
            PregledId = o.PregledId, KorisnikId = o.Ana, Razlog = "Ovaj pregled je dodeljen drugom inženjeru.", PrimljenoAt = DateTime.UtcNow,
            Operacija = JsonSerializer.Serialize(new { opId = Guid.NewGuid(), vrsta = "odgovor", pregledId = o.PregledId, klijentAt = DateTime.UtcNow,
                podaci = new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "Curi" } }),
        });
        await o.H.Db.SaveChangesAsync();
        var unos = await o.H.Db.SporniUnosi.SingleAsync();

        var sup = Kao(o, o.Sup, Roles.Supervizor);
        var naPredatom = () => sup.Handle(new OdluciOSpornomUnosuCommand(o.PregledId, unos.Id, true), default);
        (await naPredatom.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Contain(PreglediHandler.PorukaSporanPredat);
        (await o.H.Db.Odgovori.Where(x => x.PregledId == o.PregledId && x.StavkaId == o.Stavke[0]).SingleAsync()).Napomena.Should().NotBe("Curi");

        await sup.Handle(new VratiNaDoraduCommand(o.PregledId, o.Ana, "Primena spornog unosa"), default);
        var d = await sup.Handle(new OdluciOSpornomUnosuCommand(o.PregledId, unos.Id, true), default);
        d.SporniUnosi.Single().Odluka.Should().Be("primenjeno");
        d.Sekcije.Single().Uredjaji[0].Stavke.Single().Napomena.Should().Be("Curi");
    }

    [Fact]
    public async Task Sporan_odgovor_na_overenom_pregledu_se_ne_primenjuje()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        await Kao(o, o.Dir, Roles.Direktor).Handle(new OveriCommand(o.PregledId), default);
        o.H.Db.SporniUnosi.Add(new SporanUnos
        {
            PregledId = o.PregledId, KorisnikId = o.Ana, Razlog = "Ovaj pregled je dodeljen drugom inženjeru.", PrimljenoAt = DateTime.UtcNow,
            Operacija = JsonSerializer.Serialize(new { opId = Guid.NewGuid(), vrsta = "odgovor", pregledId = o.PregledId, klijentAt = DateTime.UtcNow,
                podaci = new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "Curi" } }),
        });
        await o.H.Db.SaveChangesAsync();
        var unos = await o.H.Db.SporniUnosi.SingleAsync();

        var primeni = () => Kao(o, o.Dir, Roles.Direktor).Handle(new OdluciOSpornomUnosuCommand(o.PregledId, unos.Id, true), default);

        (await primeni.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Contain(PreglediHandler.PorukaSporanPredat);
        (await o.H.Db.SporniUnosi.SingleAsync()).Odluka.Should().BeNull();
    }

    [Fact]
    public async Task Sporan_unos_na_nezapocetom_pregledu_ima_tacnu_poruku_i_naziv_stavke()
    {
        var o = await PredatAsync();
        await using var _ = o.H;
        var p = await o.H.Db.Pregledi.SingleAsync(x => x.Id == o.PregledId);
        p.Status = StatusPregleda.Planiran;
        o.H.Db.SporniUnosi.Add(new SporanUnos
        {
            PregledId = o.PregledId, KorisnikId = o.Ana, Razlog = "Ovaj pregled je dodeljen drugom inženjeru.", PrimljenoAt = DateTime.UtcNow,
            Operacija = JsonSerializer.Serialize(new { opId = Guid.NewGuid(), vrsta = "odgovor", pregledId = o.PregledId, klijentAt = DateTime.UtcNow,
                podaci = new { stavkaId = o.Stavke[0], vrednost = 1, napomena = "Curi" } }),
        });
        await o.H.Db.SaveChangesAsync();
        var unos = await o.H.Db.SporniUnosi.SingleAsync();

        var d = await Kao(o, o.Sup, Roles.Supervizor).Handle(new GetPregledQuery(o.PregledId), default);
        d.SporniUnosi.Single().Opis.Should().StartWith("Odgovor — ").And.NotContain("stavku");
        var primeni = () => Kao(o, o.Sup, Roles.Supervizor).Handle(new OdluciOSpornomUnosuCommand(o.PregledId, unos.Id, true), default);
        (await primeni.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Contain(PreglediHandler.PorukaSporanNezapocet);
    }
}
