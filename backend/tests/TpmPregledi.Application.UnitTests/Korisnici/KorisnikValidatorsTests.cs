using FluentAssertions;
using TpmPregledi.Application.Features.Korisnici.Commands;

namespace TpmPregledi.Application.UnitTests.Korisnici;

/// <summary>
/// Validatori korisnika: obavezna polja, min dužina lozinke, e-mail format kad je dat,
/// rola iz Roles.All (case-sensitive). Snagu lozinke proverava Identity, ne FluentValidation.
/// </summary>
public class KorisnikValidatorsTests
{
    private static KreirajKorisnikaCommand ValidKreiraj() =>
        new("pera", "Pera Perić", "pera@test.local", "Korisnik2026!", "Korisnik");

    [Fact]
    public void Kreiraj_validan_prolazi()
        => new KreirajKorisnikaValidator().Validate(ValidKreiraj()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Kreiraj_odbija_prazno_ili_kratko_korisnicko_ime(string ime)
        => new KreirajKorisnikaValidator().Validate(ValidKreiraj() with { KorisnickoIme = ime }).IsValid.Should().BeFalse();

    [Fact]
    public void Kreiraj_odbija_prazno_ime()
        => new KreirajKorisnikaValidator().Validate(ValidKreiraj() with { Ime = "" }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("Kr4tka")]
    public void Kreiraj_odbija_prekratku_lozinku(string lozinka)
        => new KreirajKorisnikaValidator().Validate(ValidKreiraj() with { Lozinka = lozinka }).IsValid.Should().BeFalse();

    [Fact]
    public void Kreiraj_odbija_neispravan_email_a_prazan_dozvoljava()
    {
        var v = new KreirajKorisnikaValidator();
        v.Validate(ValidKreiraj() with { Email = "nije-email" }).IsValid.Should().BeFalse();
        v.Validate(ValidKreiraj() with { Email = null }).IsValid.Should().BeTrue();
        v.Validate(ValidKreiraj() with { Email = "" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Korisnik")]
    public void Kreiraj_prihvata_dozvoljene_role(string rola)
        => new KreirajKorisnikaValidator().Validate(ValidKreiraj() with { Rola = rola }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("Superadmin")]
    [InlineData("korisnik")]
    public void Kreiraj_odbija_nepoznatu_rolu(string rola)
    {
        var r = new KreirajKorisnikaValidator().Validate(ValidKreiraj() with { Rola = rola });
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.ErrorMessage.Contains("Rola je obavezna.") || e.ErrorMessage.StartsWith("Nepoznata rola."));
    }

    [Fact]
    public void Izmeni_validan_prolazi_a_odbija_prazan_id_i_nepoznatu_rolu()
    {
        var v = new IzmeniKorisnikaValidator();
        v.Validate(new IzmeniKorisnikaCommand("id1", "Novo Ime", null, "Admin")).IsValid.Should().BeTrue();
        v.Validate(new IzmeniKorisnikaCommand("", "Ime", null, "Admin")).IsValid.Should().BeFalse();
        v.Validate(new IzmeniKorisnikaCommand("id1", "Ime", null, "Nesto")).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("NovaSifra1!", true)]
    [InlineData("", false)]
    [InlineData("Kr4tka", false)]
    public void Resetuj_lozinku_trazi_min_8(string lozinka, bool ocekivano)
        => new ResetujLozinkuValidator().Validate(new ResetujLozinkuCommand("id1", lozinka)).IsValid.Should().Be(ocekivano);
}
