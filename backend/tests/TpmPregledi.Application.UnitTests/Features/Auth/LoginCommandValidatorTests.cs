using FluentAssertions;
using TpmPregledi.Application.Features.Auth.Commands;

namespace TpmPregledi.Application.UnitTests.Features.Auth;

/// <summary>Validator prijave: oba polja obavezna, validna komanda prolazi.</summary>
public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _v = new();

    [Fact]
    public void Prazno_korisnicko_ime_pada()
        => _v.Validate(new LoginCommand("", "pw")).IsValid.Should().BeFalse();

    [Fact]
    public void Prazna_lozinka_pada()
        => _v.Validate(new LoginCommand("admin", "")).IsValid.Should().BeFalse();

    [Fact]
    public void Validna_komanda_prolazi()
        => _v.Validate(new LoginCommand("admin", "Admin2026!")).IsValid.Should().BeTrue();
}
