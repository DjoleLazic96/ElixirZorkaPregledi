using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Features.Korisnici.Commands;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Raspored;

/// <summary>Zamerke 54–56: deaktivacija sa prebacivanjem, vraćanje pregleda posle skraćenog odsustva, brisanje neradnog dana.</summary>
public class Zamerke54do56Tests
{
    // 2026-11-09 je ponedeljak.
    private static readonly DateOnly Pon = new(2026, 11, 9);
    private static FakeClock Sat() => new(Pon.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc));

    private static async Task<(IdentityHarness h, string admin, string ana, string bora, string ceca)> HarnessAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var admin = await h.SeedUserAsync("admin", "Admin2026!x", Roles.Admin);
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var bora = await h.SeedUserAsync("bora", "Inzenjer2026!", Roles.Inzenjer);
        var ceca = await h.SeedUserAsync("ceca", "Inzenjer2026!", Roles.Inzenjer);
        foreach (var (u, ime) in new[] { (ana, "Ana"), (bora, "Bora"), (ceca, "Ceca") })
        {
            u.FullName = ime;
            h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = u.Id, Pravo = Prava.PreglediPopunjavanje });
        }
        await h.Db.SaveChangesAsync();
        await new RasporedHandler(h.Db, new FakeBrojac(), new FakeCurrentUser(), Sat()).Handle(new PotvrdiRasporedCommand([
            new StavkaRasporeda(Pon, "DP1", ana.Id), new StavkaRasporeda(Pon.AddDays(1), "DP2", bora.Id), new StavkaRasporeda(Pon.AddDays(2), "DP1", ceca.Id),
            new StavkaRasporeda(Pon.AddDays(3), "DP2", ana.Id), new StavkaRasporeda(Pon.AddDays(4), "DP1", ceca.Id)]), default);
        return (h, admin.Id, ana.Id, bora.Id, ceca.Id);
    }

    [Fact]
    public async Task Deaktivacija_prebacuje_planirane_preglede_na_izabrane_zamene_zamerka_54()
    {
        var (h, admin, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        var cecini = await new OdsustvaHandler(h.Db, new FakeCurrentUser { UserId = admin }, Sat()).Handle(new GetPreglediUOdsustvuQuery(ceca, Pon, Pon.AddYears(1)), default);
        cecini.Should().HaveCount(2);
        cecini.Should().OnlyContain(x => x.PredlogZameneId != null && x.PredlogZameneId != ceca, "predlog zamene nije ona sama");

        await new DeaktivirajKorisnikaHandler(h.Users, new FakeCurrentUser { UserId = admin }, h.Db, new FakeJwtTokenService(), Sat())
            .Handle(new DeaktivirajKorisnikaCommand(ceca, cecini.Select(x => new PrebacivanjeInput(x.Pregled.Id, bora)).ToList()), default);

        (await h.Db.Pregledi.CountAsync(p => p.DodeljenId == ceca)).Should().Be(0);
        (await h.Db.Obavestenja.CountAsync(o => o.KorisnikId == bora && o.Naslov.StartsWith("Dodeljen vam je"))).Should().Be(2);
        (await h.Db.PregledDogadjaji.AnyAsync(d => d.Vrsta == "prebacivanje")).Should().BeTrue();
    }

    [Fact]
    public async Task Skraceno_i_otkazano_odsustvo_nude_vracanje_prebacenih_pregleda_zamerka_55()
    {
        var (h, admin, ana, bora, ceca) = await HarnessAsync();
        await using var _ = h;
        var sup = new OdsustvaHandler(h.Db, new FakeCurrentUser { UserId = admin }, Sat());
        // Ana odsutna pon–čet; oba njena pregleda (pon, čet) idu Bori.
        var anini = await sup.Handle(new GetPreglediUOdsustvuQuery(ana, Pon, Pon.AddDays(3)), default);
        var o = await sup.Handle(new UnesiOdsustvoCommand(ana, Pon, Pon.AddDays(3), "godisnji", null, false,
            anini.Select(x => new PrebacivanjeInput(x.Pregled.Id, bora)).ToList()), default);

        var posle = await sup.Handle(new IzmeniOdsustvoCommand(o.Id, Pon, Pon.AddDays(1)), default);
        posle.ZaVracanje.Should().ContainSingle().Which.PlaniraniDatum.Should().Be(Pon.AddDays(3), "četvrtak više nije u odsustvu");

        (await sup.Handle(new VratiPregledeCommand(o.Id, posle.ZaVracanje!.Select(p => p.Id).ToList()), default)).Should().Be(1);
        (await h.Db.Pregledi.SingleAsync(p => p.PlaniraniDatum == Pon.AddDays(3))).DodeljenId.Should().Be(ana);

        var otkazano = await sup.Handle(new OtkaziOdsustvoCommand(o.Id, true), default);
        otkazano.ZaVracanje.Should().ContainSingle().Which.PlaniraniDatum.Should().Be(Pon, "posle otkazivanja nudi se i ponedeljak");
    }

    [Fact]
    public async Task Brisanje_neradnog_dana_vraca_dan_za_povlacenje_zamerka_56()
    {
        var (h, admin, _, _, _) = await HarnessAsync();
        await using var _h = h;
        var neradni = new NeradniDaniHandler(h.Db, new FakeCurrentUser { UserId = admin }, Sat());
        await neradni.Handle(new UnesiNeradneDaneCommand(Pon.AddDays(2), Pon.AddDays(2), "Praznik", "drzavni", PomeriDp: true), default);
        var id = (await h.Db.NeradniDani.SingleAsync()).Id;

        (await neradni.Handle(new ObrisiNeradniDanCommand(id), default)).Should().Be(Pon.AddDays(2), "sreda je sada prazna, a posle nje ima DP-ova");
    }
}
