using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Aktivnosti;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.Features.Uredjaji;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Infrastructure.Sabloni;

namespace TpmPregledi.Application.UnitTests.Sabloni;

/// <summary>
/// Uvoz šablona (R2.15) nad pravim fajlom iz repoa (<c>docs/sabloni/sabloni-za-uvoz.xlsx</c>) i nad malim ručnim
/// primerima: pravila redova, „sve ili ništa“, jedan uređaj i jedna aktivnost iz više šablona, ponovni uvoz.
/// </summary>
public class UvozSablonaTests
{
    private static readonly string Fajl = PronadjiFajl();

    private static string PronadjiFajl()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "sabloni", "sabloni-za-uvoz.xlsx")))
            dir = dir.Parent;
        return dir is null ? string.Empty : Path.Combine(dir.FullName, "docs", "sabloni", "sabloni-za-uvoz.xlsx");
    }

    private sealed class FiksniCitac(IReadOnlyList<UvozRed> redovi) : ISablonUvozCitac
    {
        public IReadOnlyList<UvozRed> Procitaj(Stream xlsx) => redovi;
    }

    private static UvoziSabloneHandler Sut(IdentityHarness h, IReadOnlyList<UvozRed> redovi) =>
        new(h.Db, new FiksniCitac(redovi), new FakeCurrentUser(), new FakeClock());

    private static IdentityHarness Harness()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();   // HasData: sekcije, šabloni, tri dodatna polja
        return h;
    }

    private static UvozRed R(int red, string sablon, string sekcija, string naziv, string oznaka, string akt,
        string ok = "Ispravno", string nok = "Neispravno", string polje = "") =>
        new(red, sablon, sekcija, red, naziv, oznaka, akt, ok, nok, polje);

    private static IReadOnlyList<UvozRed> PraviRedovi()
    {
        using var tok = File.OpenRead(Fajl);
        return new SablonUvozCitac().Procitaj(tok);
    }

    [Fact]
    public void Citac_cita_sve_stavke_pravog_fajla()
    {
        var redovi = PraviRedovi();

        redovi.Should().HaveCount(867);
        redovi.Select(r => r.Sablon).Distinct().Should().BeEquivalentTo(Sablon.Tipovi);
        redovi.Should().Contain(r => r.Sablon == "DP1" && r.Naziv == "Merna vaga Sautelma" && r.Oznaka == "10-C-07C");
        redovi.Should().NotContain(r => r.Naziv.Length == 0);
        redovi.Should().Contain(r => r.Sablon == "PZ1" && r.Naziv == "Pumpa" && r.Oznaka == "21-P-02 A/B");
    }

    [Fact]
    public async Task Pravi_fajl_uvozi_svih_sest_sablona()
    {
        await using var h = Harness();
        var redovi = PraviRedovi();

        var rez = await Sut(h, redovi).Handle(new UvoziSabloneCommand(Stream.Null), default);

        rez.Sabloni.Select(s => s.Tip).Should().Equal(Sablon.Tipovi);
        rez.Sabloni.Sum(s => s.Stavki).Should().Be(867);
        (await h.Db.SablonStavke.CountAsync()).Should().Be(867);
        (await h.Db.SablonVerzije.CountAsync(v => v.Status == StatusVerzije.Objavljena && v.Broj == 1)).Should().Be(6);
        (await h.Db.Uredjaji.CountAsync()).Should().Be(rez.NovihUredjaja);
        (await h.Db.SablonDogadjaji.CountAsync(d => d.Vrsta == "prvi_uvoz" && d.VerzijaId != null)).Should().Be(6);
        (await h.Db.SablonStavkaPolja.CountAsync()).Should().BeGreaterThan(100, "kolona „Dodatno polje“ je popunjena na ~150 mesta");

        var dp1 = await new SabloniQueriesHandler(h.Db).Handle(new GetSablonVerzijaQuery("DP1"), default);
        dp1.Sekcije.Select(s => s.Sifra).Should().StartWith(["10", "20", "30"]);
        dp1.Sekcije[0].Uredjaji[0].Naziv.Should().Be("Bubnjasta");
    }

    [Fact]
    public async Task Isti_uredjaj_i_ista_aktivnost_iz_dva_sablona_su_po_jedan_red()
    {
        await using var h = Harness();
        var rez = await Sut(h,
        [
            R(2, "PZ1", "20", "Redler", "20-CR-05", "Pregled lanca", "Neoštećeno", "Oštećeno"),
            R(3, "PZ2", "20", "Redler", "20-CR-05", "Pregled lanca", "Neoštećeno", "Oštećeno"),
        ]).Handle(new UvoziSabloneCommand(Stream.Null), default);

        rez.NovihUredjaja.Should().Be(1);
        rez.NovihAktivnosti.Should().Be(1);
        var u = (await new UredjajiHandler(h.Db).Handle(new GetUredjajiQuery(), default)).Single();
        u.Sabloni.Should().Equal("PZ1", "PZ2");
        (await new AktivnostiHandler(h.Db).Handle(new GetAktivnostiQuery(), default)).Single().BrojStavki.Should().Be(2);
    }

    [Fact]
    public async Task Ponovni_uvoz_istog_sablona_vraca_422()
    {
        await using var h = Harness();
        IReadOnlyList<UvozRed> redovi = [R(2, "DP1", "10", "Elevator", "10-C-13", "Pregled rada zatezne sekcije")];
        await Sut(h, redovi).Handle(new UvoziSabloneCommand(Stream.Null), default);

        var act = () => Sut(h, redovi).Handle(new UvoziSabloneCommand(Stream.Null), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske
            .Should().Contain("Šablon DP1 je već uvezen; izmene idu kroz nacrt.");
    }

    [Fact]
    public void Provera_redova_nabraja_sve_greske_sa_brojem_reda()
    {
        var sekcije = new Dictionary<string, Sekcija> { ["10"] = new() { Id = 1, Sifra = "10" }, ["20"] = new() { Id = 2, Sifra = "20" } };
        var polja = new Dictionary<string, DodatnoPolje> { ["Temperatura ležaja"] = new() { Id = 2 } };
        var sabloni = Sablon.Tipovi.ToDictionary(t => t, t => new Sablon { Tip = t });

        var greske = UvoziSabloneHandler.Proveri(
        [
            R(2, "DP9", "10", "Elevator", "10-C-13", "Pregled"),
            R(3, "DP1", "99", "Elevator", "10-C-13", "Pregled"),
            R(4, "DP1", "10", "Elevator", "10-C-13", "Pregled", "Da", "Da"),
            R(5, "DP1", "10", "Elevator", "10-C-13", "Pregled", polje: "Pritisak"),
            R(6, "DP1", "10", "Puž", "30-F-01 M2", "Prosipanje"),
            R(7, "DP2", "20", "Puž", "30-F-01 M2", "Prosipanje"),
            R(8, "DP1", "10", "Puž", "30-F-01 M2", "Prosipanje"),
        ], sekcije, polja, sabloni);

        greske.Should().HaveCount(6);
        greske[0].Should().StartWith("Red 2").And.Contain("nepoznat šablon");
        greske[1].Should().StartWith("Red 3").And.Contain("nepoznata sekcija");
        greske[2].Should().StartWith("Red 4").And.Contain("dva odgovora su ista");
        greske[3].Should().StartWith("Red 5").And.Contain("nepoznato dodatno polje");
        greske[4].Should().StartWith("Red 7").And.Contain("a u redu 6 u sekciji 10");
        greske[5].Should().StartWith("Red 8").And.Contain("već pojavljuje");
    }

    [Fact]
    public async Task Par_odgovora_aktivnosti_iz_vazeceg_sablona_se_ne_menja()
    {
        await using var h = Harness();
        await Sut(h, [R(2, "DP1", "10", "Elevator", "10-C-13", "Pregled rada zatezne sekcije")])
            .Handle(new UvoziSabloneCommand(Stream.Null), default);
        var akt = await h.Db.Aktivnosti.SingleAsync();
        var sut = new AktivnostiHandler(h.Db);

        var act = () => sut.Handle(new IzmeniAktivnostCommand(akt.Id, new AktivnostInput(akt.Naziv, "Radi", "Ne radi")), default);
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(AktivnostiHandler.PorukaParUUpotrebi);

        // Ispravka naziva je dozvoljena.
        (await sut.Handle(new IzmeniAktivnostCommand(akt.Id, new AktivnostInput("Pregled rada zatezne sekcije (elevator)", "Ispravno", "Neispravno")), default))
            .Naziv.Should().EndWith("(elevator)");
    }

    [Fact]
    public async Task Registar_uredjaj_van_upotrebe_i_duplikat()
    {
        await using var h = Harness();
        var sut = new UredjajiHandler(h.Db);
        var u = await sut.Handle(new KreirajUredjajCommand(new UredjajInput("Pumpa", "21-P-09", 3, "Pumpa")), default);

        u.Sekcija.Should().Be("21");
        u.Sabloni.Should().BeEmpty();
        (await sut.Handle(new GetUredjajiQuery(SamoBezSablona: true), default)).Should().ContainSingle(x => x.Id == u.Id);
        (await sut.Handle(new PostaviStatusUredjajaCommand(u.Id, StatusUredjaja.VanUpotrebe), default)).Status.Should().Be("van_upotrebe");

        var dup = () => sut.Handle(new KreirajUredjajCommand(new UredjajInput("Pumpa", "21-P-09", 3, "Pumpa")), default);
        (await dup.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Single().Should().Contain("„Pumpa 21-P-09“");
    }

    [Theory]
    [InlineData("Merna vaga Sautelma", "Merna vaga")]
    [InlineData("Koš vage", "Koš vage")]
    [InlineData("Puž filtera", "Puž filtera")]
    [InlineData("Puž", "Puž")]
    [InlineData("Ventilator (SK 170)", "Ventilator")]
    [InlineData("Ventilator sekundardni - hladnjak", "Ventilator")]
    [InlineData("HL1", "HL1")]
    public void Vrsta_se_izvodi_iz_naziva(string naziv, string vrsta)
        => UvoziSabloneHandler.VrstaIzNaziva(naziv).Should().Be(vrsta);
}
