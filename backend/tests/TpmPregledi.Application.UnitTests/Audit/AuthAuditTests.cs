using FluentAssertions;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Auth.Commands;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;

namespace TpmPregledi.Application.UnitTests.Audit;

/// <summary>
/// Login/logout handleri beleže događaje kroz IAuditDogadjaji i — ključno — beleženje neuspeha
/// ne menja 401 ponašanje za sve tačke neuspeha: nepostojeći/neaktivan korisnik, lockout, pogrešna lozinka.
/// Uspešna prijava briše neaktivne tokene korisnika.
/// </summary>
public class AuthAuditTests
{
    private static LoginCommandHandler Handler(IdentityHarness h, FakeAuditDogadjaji audit, FakeJwtTokenService? jwt = null) =>
        new(h.Users, jwt ?? new FakeJwtTokenService(), FakeCurrentUser.Anoniman, new FakeClock(), audit);

    [Fact]
    public async Task Uspesna_prijava_belezi_LOGIN_jednom_i_cisti_neaktivne_tokene()
    {
        await using var h = IdentityHarness.New();
        var user  = await h.SeedUserAsync("admin", "Admin2026!");
        var audit = new FakeAuditDogadjaji();
        var jwt   = new FakeJwtTokenService();

        var result = await Handler(h, audit, jwt).Handle(new LoginCommand("admin", "Admin2026!"), default);

        result.Response.User.UserName.Should().Be("admin");
        audit.Uspeh.Should().Be(1);
        audit.Neuspeh.Should().Be(0);
        audit.PoslednjiUspehUserId.Should().Be(user.Id);
        jwt.DeleteInactiveZa.Should().ContainSingle().Which.Should().Be(user.Id);
    }

    [Fact]
    public async Task Uspesna_prijava_upisuje_LastLoginAt()
    {
        await using var h = IdentityHarness.New();
        await h.SeedUserAsync("admin", "Admin2026!");

        await Handler(h, new FakeAuditDogadjaji()).Handle(new LoginCommand("admin", "Admin2026!"), default);

        (await h.Users.FindByNameAsync("admin"))!.LastLoginAt.Should().Be(FakeClock.Podrazumevano);
    }

    [Fact]
    public async Task Pogresna_lozinka_belezi_LOGIN_FAIL_i_baca_401_sa_genericnom_porukom()
    {
        await using var h = IdentityHarness.New();
        await h.SeedUserAsync("admin", "Admin2026!");
        var audit = new FakeAuditDogadjaji();

        var act = () => Handler(h, audit).Handle(new LoginCommand("admin", "Pogresna9!"), default);

        (await act.Should().ThrowAsync<AuthenticationFailedException>())
            .Which.Message.Should().Be("Neispravno korisničko ime ili lozinka.");
        audit.Neuspeh.Should().Be(1);
        audit.Uspeh.Should().Be(0);
        audit.PoslednjeNeuspehIme.Should().Be("admin");
    }

    [Fact]
    public async Task Nepostojeci_korisnik_belezi_LOGIN_FAIL_i_baca_401()
    {
        await using var h = IdentityHarness.New();
        var audit = new FakeAuditDogadjaji();

        var act = () => Handler(h, audit).Handle(new LoginCommand("nema-me", "Bilo2026!"), default);

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        audit.Neuspeh.Should().Be(1);
        audit.PoslednjeNeuspehIme.Should().Be("nema-me");
    }

    [Fact]
    public async Task Neaktivan_korisnik_belezi_LOGIN_FAIL_i_baca_401()
    {
        await using var h = IdentityHarness.New();
        await h.SeedUserAsync("deakt", "Admin2026!", active: false);
        var audit = new FakeAuditDogadjaji();

        var act = () => Handler(h, audit).Handle(new LoginCommand("deakt", "Admin2026!"), default);

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        audit.Neuspeh.Should().Be(1);
    }

    [Fact]
    public async Task Zakljucan_nalog_belezi_LOGIN_FAIL_i_baca_401_i_sa_tacnom_lozinkom()
    {
        await using var h = IdentityHarness.New();
        await h.SeedUserAsync("locked", "Admin2026!",
            lockoutEnd: new DateTimeOffset(2999, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var audit = new FakeAuditDogadjaji();

        var act = () => Handler(h, audit).Handle(new LoginCommand("locked", "Admin2026!"), default);

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        audit.Neuspeh.Should().Be(1);
        audit.Uspeh.Should().Be(0);
    }

    [Fact]
    public async Task Pet_promasaja_zakljucava_nalog()
    {
        await using var h = IdentityHarness.New();
        var user = await h.SeedUserAsync("brute", "Admin2026!");
        var handler = Handler(h, new FakeAuditDogadjaji());

        for (var i = 0; i < 5; i++)
            await FluentActions.Awaiting(() => handler.Handle(new LoginCommand("brute", "Pogresna9!"), default))
                .Should().ThrowAsync<AuthenticationFailedException>();

        (await h.Users.IsLockedOutAsync((await h.Users.FindByIdAsync(user.Id))!)).Should().BeTrue();
        await FluentActions.Awaiting(() => handler.Handle(new LoginCommand("brute", "Admin2026!"), default))
            .Should().ThrowAsync<AuthenticationFailedException>("tačna lozinka ne pomaže dok traje lockout");
    }

    [Fact]
    public async Task Odjava_revokuje_token_i_belezi_LOGOUT()
    {
        var audit = new FakeAuditDogadjaji();
        var jwt   = new FakeJwtTokenService();
        var handler = new LogoutCommandHandler(jwt, new FakeCurrentUser { UserId = "u-9", UserName = "pera" }, audit);

        await handler.Handle(new LogoutCommand("refresh-abc"), default);

        audit.Odjava.Should().Be(1);
        jwt.RevokeCount.Should().Be(1);
    }
}
