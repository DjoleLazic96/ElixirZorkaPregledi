using FluentAssertions;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Auth.Commands;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;

namespace TpmPregledi.Application.UnitTests.Features.Auth;

/// <summary>
/// Promena sopstvene lozinke: uspeh revokuje sve refresh tokene; pogrešna trenutna → 400 pod
/// currentPassword; politika nove → 400 pod newPassword sa srpskom porukom; anoniman → 401.
/// </summary>
public class ChangePasswordHandlerTests
{
    [Fact]
    public async Task Uspeh_menja_lozinku_i_revokuje_sve_tokene()
    {
        await using var h = IdentityHarness.New();
        var user = await h.SeedUserAsync("pera", "Stara2026!");
        var jwt  = new FakeJwtTokenService();
        var sut  = new ChangePasswordHandler(h.Users, new FakeCurrentUser { UserId = user.Id, UserName = "pera" }, jwt);

        await sut.Handle(new ChangePasswordCommand("Stara2026!", "Nova2026!"), default);

        (await h.Users.CheckPasswordAsync(user, "Nova2026!")).Should().BeTrue();
        jwt.RevokeAllZa.Should().ContainSingle().Which.Should().Be(user.Id);
    }

    [Fact]
    public async Task Pogresna_trenutna_lozinka_vraca_400_pod_currentPassword()
    {
        await using var h = IdentityHarness.New();
        var user = await h.SeedUserAsync("pera", "Stara2026!");
        var jwt  = new FakeJwtTokenService();
        var sut  = new ChangePasswordHandler(h.Users, new FakeCurrentUser { UserId = user.Id }, jwt);

        var act = () => sut.Handle(new ChangePasswordCommand("Pogresna9!", "Nova2026!"), default);

        var ex = await act.Should().ThrowAsync<ValidationFailedException>();
        ex.Which.Errors.Should().ContainKey("currentPassword")
            .WhoseValue.Should().ContainSingle().Which.Should().Be("Trenutna lozinka nije ispravna.");
        jwt.RevokeAllZa.Should().BeEmpty();
    }

    [Fact]
    public async Task Slaba_nova_lozinka_vraca_400_pod_newPassword_sa_srpskim_porukama()
    {
        await using var h = IdentityHarness.New();
        var user = await h.SeedUserAsync("pera", "Stara2026!");
        var sut  = new ChangePasswordHandler(h.Users, new FakeCurrentUser { UserId = user.Id }, new FakeJwtTokenService());

        // 8+ znakova (prolazi FluentValidation), ali bez cifre i velikog slova → Identity politika.
        var act = () => sut.Handle(new ChangePasswordCommand("Stara2026!", "samomalaslova"), default);

        var ex = await act.Should().ThrowAsync<ValidationFailedException>();
        var poruke = ex.Which.Errors.Should().ContainKey("newPassword").WhoseValue;
        poruke.Should().Contain("Lozinka mora sadržati najmanje jednu cifru (0–9).");
        poruke.Should().Contain("Lozinka mora sadržati najmanje jedno veliko slovo (A–Z).");
    }

    [Fact]
    public async Task Anoniman_poziv_baca_401()
    {
        await using var h = IdentityHarness.New();
        var sut = new ChangePasswordHandler(h.Users, FakeCurrentUser.Anoniman, new FakeJwtTokenService());

        await FluentActions.Awaiting(() => sut.Handle(new ChangePasswordCommand("a", "b"), default))
            .Should().ThrowAsync<AuthenticationFailedException>();
    }

    [Fact]
    public void Validator_trazi_oba_polja_i_min_8()
    {
        var v = new ChangePasswordValidator();
        v.Validate(new ChangePasswordCommand("", "Nova2026!")).IsValid.Should().BeFalse();
        v.Validate(new ChangePasswordCommand("Stara", "")).IsValid.Should().BeFalse();
        v.Validate(new ChangePasswordCommand("Stara", "Kr4tka")).IsValid.Should().BeFalse();
        v.Validate(new ChangePasswordCommand("Stara", "Nova2026!")).IsValid.Should().BeTrue();
    }
}
