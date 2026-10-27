using FluentAssertions;
using TpmPregledi.Application.Features.Kategorije.Commands;

namespace TpmPregledi.Application.UnitTests.Kategorije;

/// <summary>PRIMER feature-a: validatori kategorije — jedan test za validan ulaz, po jedan za svako pravilo.</summary>
public class KategorijeValidatorsTests
{
    private static KreirajKategorijuCommand Valid() => new("K1", "Prva kategorija", "Opis");

    [Fact]
    public void Kreiraj_validan_prolazi()
        => new KreirajKategorijuValidator().Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void Kreiraj_prazna_sifra_pada_sa_srpskom_porukom()
    {
        var r = new KreirajKategorijuValidator().Validate(Valid() with { Sifra = "" });
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.ErrorMessage == "Šifra je obavezna.");
    }

    [Fact]
    public void Kreiraj_sifra_preko_32_pada()
        => new KreirajKategorijuValidator().Validate(Valid() with { Sifra = new string('x', 33) }).IsValid.Should().BeFalse();

    [Fact]
    public void Kreiraj_prazan_naziv_pada()
    {
        var r = new KreirajKategorijuValidator().Validate(Valid() with { Naziv = "" });
        r.Errors.Should().Contain(e => e.ErrorMessage == "Naziv je obavezan.");
    }

    [Fact]
    public void Kreiraj_naziv_preko_200_pada()
        => new KreirajKategorijuValidator().Validate(Valid() with { Naziv = new string('x', 201) }).IsValid.Should().BeFalse();

    [Fact]
    public void Kreiraj_opis_preko_1000_pada_a_null_prolazi()
    {
        var v = new KreirajKategorijuValidator();
        v.Validate(Valid() with { Opis = new string('x', 1001) }).IsValid.Should().BeFalse();
        v.Validate(Valid() with { Opis = null }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Izmeni_trazi_pozitivan_id_i_ista_pravila()
    {
        var v = new IzmeniKategorijuValidator();
        v.Validate(new IzmeniKategorijuCommand(1, "K1", "Naziv", null)).IsValid.Should().BeTrue();
        v.Validate(new IzmeniKategorijuCommand(0, "K1", "Naziv", null)).IsValid.Should().BeFalse();
        v.Validate(new IzmeniKategorijuCommand(1, "", "Naziv", null)).IsValid.Should().BeFalse();
    }
}
