using FluentAssertions;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Korisnici.Commands;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Korisnici;

/// <summary>
/// Handleri korisnika nad pravim Identity-jem (InMemory): guardovi kroz ceo handler, revokacija
/// refresh tokena pri promeni role / deaktivaciji / resetu lozinke, 422 za zauzeto ime, 404.
/// </summary>
public class KorisniciHandlerTests
{
    // ─── Kreiraj ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Kreiranje_dodeljuje_rolu_i_vraca_dto_sa_jednom_rolom()
    {
        await using var h = IdentityHarness.New();
        await h.Roles.CreateAsync(new ApplicationRole { Name = Roles.Korisnik });
        var sut = new KreirajKorisnikaHandler(h.Users, new FakeClock());

        var dto = await sut.Handle(new KreirajKorisnikaCommand("pera", "Pera Perić", " pera@test.local ", "Korisnik2026!", Roles.Korisnik), default);

        dto.KorisnickoIme.Should().Be("pera");
        dto.Rola.Should().Be(Roles.Korisnik);
        dto.Email.Should().Be("pera@test.local");
        dto.Aktivan.Should().BeTrue();
        dto.PoslednjaPrijava.Should().BeNull();
        (await h.Users.IsInRoleAsync((await h.Users.FindByNameAsync("pera"))!, Roles.Korisnik)).Should().BeTrue();
    }

    [Fact]
    public async Task Kreiranje_sa_zauzetim_imenom_vraca_422()
    {
        await using var h = IdentityHarness.New();
        await h.SeedUserAsync("pera", "Korisnik2026!", Roles.Korisnik);
        var sut = new KreirajKorisnikaHandler(h.Users, new FakeClock());

        await FluentActions.Awaiting(() => sut.Handle(new KreirajKorisnikaCommand("pera", "Drugi", null, "Korisnik2026!", Roles.Korisnik), default))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Kreiranje_sa_slabom_lozinkom_vraca_400_pod_lozinka_sa_srpskom_porukom()
    {
        await using var h = IdentityHarness.New();
        await h.Roles.CreateAsync(new ApplicationRole { Name = Roles.Korisnik });
        var sut = new KreirajKorisnikaHandler(h.Users, new FakeClock());

        var act = () => sut.Handle(new KreirajKorisnikaCommand("pera", "Pera", null, "samomalaslova", Roles.Korisnik), default);

        var ex = await act.Should().ThrowAsync<ValidationFailedException>();
        ex.Which.Errors.Should().ContainKey("lozinka").WhoseValue.Should().Contain("Lozinka mora sadržati najmanje jednu cifru (0–9).");
    }

    // ─── Izmeni ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Promena_role_revokuje_tokene_i_korisnik_ima_tacno_novu_rolu()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var pera  = await h.SeedUserAsync("pera", "Korisnik2026!", Roles.Korisnik);
        await h.Roles.CreateAsync(new ApplicationRole { Name = Roles.Admin }).ContinueWith(_ => { }); // već postoji — ignoriši
        var jwt = new FakeJwtTokenService();
        var sut = new IzmeniKorisnikaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, jwt);

        var dto = await sut.Handle(new IzmeniKorisnikaCommand(pera.Id, "Pera Novi", null, Roles.Admin), default);

        dto.Rola.Should().Be(Roles.Admin);
        dto.Ime.Should().Be("Pera Novi");
        (await h.Users.GetRolesAsync(pera)).Should().Equal(Roles.Admin);
        jwt.RevokeAllZa.Should().ContainSingle().Which.Should().Be(pera.Id);
    }

    [Fact]
    public async Task Izmena_bez_promene_role_ne_revokuje_tokene()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var pera  = await h.SeedUserAsync("pera", "Korisnik2026!", Roles.Korisnik);
        var jwt = new FakeJwtTokenService();
        var sut = new IzmeniKorisnikaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, jwt);

        await sut.Handle(new IzmeniKorisnikaCommand(pera.Id, "Samo ime", null, Roles.Korisnik), default);

        jwt.RevokeAllZa.Should().BeEmpty();
    }

    [Fact]
    public async Task Promena_sopstvene_role_vraca_422()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        await h.SeedUserAsync("admin2", "Admin2026!", Roles.Admin);
        await h.Roles.CreateAsync(new ApplicationRole { Name = Roles.Korisnik });
        var sut = new IzmeniKorisnikaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, new FakeJwtTokenService());

        var act = () => sut.Handle(new IzmeniKorisnikaCommand(admin.Id, "Admin", null, Roles.Korisnik), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal("Ne možete promeniti sopstvenu rolu.");
    }

    [Fact]
    public async Task Skidanje_Admin_role_poslednjem_adminu_vraca_422()
    {
        await using var h = IdentityHarness.New();
        var admin  = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var drugi  = await h.SeedUserAsync("drugi", "Admin2026!", Roles.Admin, active: false);  // neaktivan admin se ne računa
        await h.Roles.CreateAsync(new ApplicationRole { Name = Roles.Korisnik });
        var akter = new FakeCurrentUser { UserId = drugi.Id };
        var sut = new IzmeniKorisnikaHandler(h.Users, akter, new FakeJwtTokenService());

        var act = () => sut.Handle(new IzmeniKorisnikaCommand(admin.Id, "Admin", null, Roles.Korisnik), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske
            .Should().Equal("Ne možete skinuti rolu Admin poslednjem aktivnom administratoru.");
    }

    [Fact]
    public async Task Izmena_nepostojeceg_vraca_404()
    {
        await using var h = IdentityHarness.New();
        var sut = new IzmeniKorisnikaHandler(h.Users, new FakeCurrentUser(), new FakeJwtTokenService());

        await FluentActions.Awaiting(() => sut.Handle(new IzmeniKorisnikaCommand("nema", "Ime", null, Roles.Admin), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ─── Deaktiviraj / Aktiviraj ────────────────────────────────────────────

    [Fact]
    public async Task Deaktivacija_poslednjeg_admina_vraca_422()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var sut = new DeaktivirajKorisnikaHandler(h.Users, new FakeCurrentUser { UserId = "neko-drugi" }, new FakeJwtTokenService());

        var act = () => sut.Handle(new DeaktivirajKorisnikaCommand(admin.Id), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske
            .Should().Equal("Ne možete deaktivirati poslednjeg aktivnog administratora.");
    }

    [Fact]
    public async Task Samodeaktivacija_vraca_422()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        await h.SeedUserAsync("admin2", "Admin2026!", Roles.Admin);
        var sut = new DeaktivirajKorisnikaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, new FakeJwtTokenService());

        var act = () => sut.Handle(new DeaktivirajKorisnikaCommand(admin.Id), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal("Ne možete deaktivirati sami sebe.");
    }

    [Fact]
    public async Task Deaktivacija_gasi_nalog_revokuje_tokene_i_aktivacija_vraca()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var pera  = await h.SeedUserAsync("pera", "Korisnik2026!", Roles.Korisnik);
        var jwt   = new FakeJwtTokenService();
        var akter = new FakeCurrentUser { UserId = admin.Id };

        var dto = await new DeaktivirajKorisnikaHandler(h.Users, akter, jwt).Handle(new DeaktivirajKorisnikaCommand(pera.Id), default);
        dto.Aktivan.Should().BeFalse();
        dto.Rola.Should().Be(Roles.Korisnik);
        jwt.RevokeAllZa.Should().Equal(pera.Id);

        // Idempotentno: drugi poziv ne revokuje ponovo.
        await new DeaktivirajKorisnikaHandler(h.Users, akter, jwt).Handle(new DeaktivirajKorisnikaCommand(pera.Id), default);
        jwt.RevokeAllZa.Should().HaveCount(1);

        var vracen = await new AktivirajKorisnikaHandler(h.Users).Handle(new AktivirajKorisnikaCommand(pera.Id), default);
        vracen.Aktivan.Should().BeTrue();
    }

    // ─── Reset lozinke ──────────────────────────────────────────────────────

    [Fact]
    public async Task Reset_lozinke_menja_lozinku_i_revokuje_tokene()
    {
        await using var h = IdentityHarness.New();
        var pera = await h.SeedUserAsync("pera", "Stara2026!", Roles.Korisnik);
        var jwt  = new FakeJwtTokenService();

        await new ResetujLozinkuHandler(h.Users, jwt).Handle(new ResetujLozinkuCommand(pera.Id, "Nova2026!"), default);

        (await h.Users.CheckPasswordAsync(pera, "Nova2026!")).Should().BeTrue();
        jwt.RevokeAllZa.Should().Equal(pera.Id);
    }

    [Fact]
    public async Task Reset_sa_slabom_lozinkom_vraca_400_pod_novaLozinka()
    {
        await using var h = IdentityHarness.New();
        var pera = await h.SeedUserAsync("pera", "Stara2026!", Roles.Korisnik);
        var sut  = new ResetujLozinkuHandler(h.Users, new FakeJwtTokenService());

        var act = () => sut.Handle(new ResetujLozinkuCommand(pera.Id, "samomalaslova"), default);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("novaLozinka");
    }
}
