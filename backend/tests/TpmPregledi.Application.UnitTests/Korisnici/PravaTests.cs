using FluentAssertions;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Korisnici;
using TpmPregledi.Application.Features.Korisnici.Commands;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Korisnici;

/// <summary>
/// Prava štikliranjem (R1.5, R1.6, R1.8): podrazumevani skup po roli, guardovi, zamena skupa i
/// revokacija sesija.
/// </summary>
public class PravaTests
{
    [Fact]
    public void Podrazumevana_prava_po_roli_prate_R1_5()
    {
        Prava.Podrazumevana(Roles.Inzenjer).Should().Equal(Prava.PreglediPopunjavanje, Prava.Analitika);
        Prava.Podrazumevana(Roles.Supervizor).Should().Contain(Prava.Overa).And.NotContain(Prava.SabloniObjava);
        Prava.Podrazumevana(Roles.Direktor).Should().Contain([Prava.Overa, Prava.SabloniObjava, Prava.NeradniDani]);
        Prava.Podrazumevana(Roles.Admin).Should().Contain([Prava.Korisnici, Prava.Podesavanja]).And.NotContain(Prava.Overa);
    }

    [Fact]
    public void Popunjavanje_ima_samo_Inzenjer_u_podrazumevanim()
    {
        foreach (var rola in Roles.All.Where(r => r != Roles.Inzenjer))
            Prava.Podrazumevana(rola).Should().NotContain(Prava.PreglediPopunjavanje);
    }

    [Fact]
    public void Sva_podrazumevana_prava_su_poznata()
    {
        foreach (var rola in Roles.All)
            Prava.Podrazumevana(rola).Should().BeSubsetOf(Prava.Sva);
    }

    [Fact]
    public async Task Kreiranje_daje_podrazumevana_prava_role()
    {
        await using var h = IdentityHarness.New();
        await h.Roles.CreateAsync(new ApplicationRole { Name = Roles.Supervizor });
        var sut = new KreirajKorisnikaHandler(h.Users, h.Db, new FakeClock());

        var dto = await sut.Handle(new KreirajKorisnikaCommand("mika", "Mika Mikić", null, "Korisnik2026!", Roles.Supervizor), default);

        dto.Prava.Should().Equal(Prava.Podrazumevana(Roles.Supervizor));
        (await KorisnikPrava.ProcitajAsync(h.Db, dto.Id, default)).Should().Equal(dto.Prava);
    }

    [Fact]
    public async Task Promena_role_vraca_podrazumevana_prava_nove_role()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var pera  = await h.SeedUserAsync("pera", "Korisnik2026!", Roles.Inzenjer);
        await h.Roles.CreateAsync(new ApplicationRole { Name = Roles.Direktor });
        await KorisnikPrava.ZameniAsync(h.Db, pera.Id, Prava.Podrazumevana(Roles.Inzenjer), default);
        await h.Db.SaveChangesAsync();
        var sut = new IzmeniKorisnikaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, h.Db, new FakeJwtTokenService());

        var dto = await sut.Handle(new IzmeniKorisnikaCommand(pera.Id, "Pera", null, Roles.Direktor), default);

        dto.Prava.Should().Equal(Prava.Podrazumevana(Roles.Direktor));
    }

    [Fact]
    public async Task Postavljanje_prava_menja_skup_i_revokuje_sesije()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var mika  = await h.SeedUserAsync("mika", "Korisnik2026!", Roles.Supervizor);
        var jwt   = new FakeJwtTokenService();
        var sut   = new PostaviPravaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, h.Db, jwt);

        var dto = await sut.Handle(new PostaviPravaCommand(mika.Id, [Prava.Overa, Prava.Analitika]), default);

        dto.Prava.Should().Equal(Prava.Analitika, Prava.Overa);   // redosled iz Prava.Sva
        jwt.RevokeAllZa.Should().Contain(mika.Id);
    }

    [Fact]
    public async Task Popunjavanje_pregleda_ne_moze_dobiti_Supervizor()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var mika  = await h.SeedUserAsync("mika", "Korisnik2026!", Roles.Supervizor);
        var sut   = new PostaviPravaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, h.Db, new FakeJwtTokenService());

        (await FluentActions.Awaiting(() => sut.Handle(new PostaviPravaCommand(mika.Id, [Prava.PreglediPopunjavanje]), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(KorisnikGuards.PorukaPopunjavanjeSamoInzenjer);
    }

    [Fact]
    public async Task Admin_ne_moze_sebi_oduzeti_upravljanje_korisnicima()
    {
        await using var h = IdentityHarness.New();
        var admin = await h.SeedUserAsync("admin", "Admin2026!", Roles.Admin);
        var sut   = new PostaviPravaHandler(h.Users, new FakeCurrentUser { UserId = admin.Id }, h.Db, new FakeJwtTokenService());

        (await FluentActions.Awaiting(() => sut.Handle(new PostaviPravaCommand(admin.Id, [Prava.Podesavanja]), default))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(KorisnikGuards.PorukaSamooduzimanjeKorisnika);
    }

    [Fact]
    public void Validator_odbija_nepoznato_pravo()
    {
        new PostaviPravaValidator().Validate(new PostaviPravaCommand("x", ["brisanje.svega"])).IsValid.Should().BeFalse();
        new PostaviPravaValidator().Validate(new PostaviPravaCommand("x", [Prava.Overa])).IsValid.Should().BeTrue();
    }
}
