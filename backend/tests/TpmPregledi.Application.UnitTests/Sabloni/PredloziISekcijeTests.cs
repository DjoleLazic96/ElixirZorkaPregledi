using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Auth.Commands;
using TpmPregledi.Application.Features.Korisnici.Commands;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.Features.Sekcije;
using TpmPregledi.Application.Features.Uredjaji;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Sabloni;

/// <summary>Zamerke 23, 33, 34, 36, 37: obavezna promena lozinke, sekcije, predlog iz pregleda, „samo moji“, saglasnost i duplikati.</summary>
public class PredloziISekcijeTests
{
    private sealed record Okr(IdentityHarness H, string Dir, string Ana, string Bora);

    private static async Task<Okr> PripremiAsync()
    {
        var h = IdentityHarness.New();
        h.Db.Database.EnsureCreated();
        var dir = await h.SeedUserAsync("dir", "Direktor2026!", Roles.Direktor);
        var ana = await h.SeedUserAsync("ana", "Inzenjer2026!", Roles.Inzenjer);
        var bora = await h.SeedUserAsync("bora", "Inzenjer2026!", Roles.Inzenjer);
        h.Db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = dir.Id, Pravo = Prava.SabloniNacrt });
        h.Db.Sabloni.Add(new Sablon { Tip = "DP1" });
        await h.Db.SaveChangesAsync();
        return new Okr(h, dir.Id, ana.Id, bora.Id);
    }

    private static IzmeneSablonaHandler Sut(Okr o, string ko) => new(o.H.Db, new FakeCurrentUser { UserId = ko }, new FakeClock());

    [Fact]
    public async Task Slazem_se_dodaje_ime_a_samo_moji_vidi_svoje_i_one_sa_kojima_se_slozio()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var p = await Sut(o, o.Ana).Handle(new PodnesiPredlogCommand("DP1", "dodavanje", null, "Dodati ventilator 30-B-05", "Ugrađen 10.11."), default);
        await Sut(o, o.Dir).Handle(new PodnesiPredlogCommand("DP1", "ispravka", null, "Ispraviti naziv", "Greška u kucanju"), default);

        var r = await Sut(o, o.Bora).Handle(new SlazemSeSaPredlogomCommand(p.Id), default);
        await Sut(o, o.Bora).Handle(new SlazemSeSaPredlogomCommand(p.Id), default);   // drugi klik ne duplira

        r.Saglasni!.Select(s => s.Id).Should().Equal(o.Bora);
        (await Sut(o, o.Bora).Handle(new GetPredloziQuery(null, null, SamoMoji: true), default)).Select(x => x.Id).Should().Equal(p.Id);
        (await Sut(o, o.Bora).Handle(new GetPredloziQuery(null, null), default)).Should().HaveCount(2);
        await Sut(o, o.Ana).Invoking(s => s.Handle(new SlazemSeSaPredlogomCommand(p.Id), default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*");
    }

    [Fact]
    public async Task Duplikat_se_zatvara_a_odluka_o_originalu_stize_i_njegovom_podnosiocu_i_saglasnima()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var original = await Sut(o, o.Ana).Handle(new PodnesiPredlogCommand("DP1", "dodavanje", null, "Fali ventilator", "Nov"), default);
        var dupli = await Sut(o, o.Bora).Handle(new PodnesiPredlogCommand("DP1", "dodavanje", null, "Dodati novi ventilator", "Ugrađen"), default);

        var d = await Sut(o, o.Dir).Handle(new OdluciOPredloguCommand(dupli.Id, false, null, DuplikatOd: original.Id), default);
        d.Status.Should().Be("duplikat");
        d.OriginalId.Should().Be(original.Id);

        await Sut(o, o.Dir).Handle(new OdluciOPredloguCommand(original.Id, true, null), default);
        var ishodi = await o.H.Db.Obavestenja.Where(x => x.Vrsta == VrsteObavestenja.IshodZahteva && x.Naslov.Contains("odobren")).Select(x => x.KorisnikId).ToListAsync();
        ishodi.Should().BeEquivalentTo([o.Ana, o.Bora]);
        await Sut(o, o.Dir).Invoking(s => s.Handle(new OdluciOPredloguCommand(original.Id, false, null, DuplikatOd: original.Id), default))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Predlog_iz_pregleda_pamti_pregled_i_upisuje_istoriju_pregleda()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var pregled = new Pregled { Broj = "DP1-2026-0005", Tip = "DP1", PlaniraniDatum = new DateOnly(2026, 11, 9), PrvobitniDatum = new DateOnly(2026, 11, 9), DodeljenId = o.Ana };
        o.H.Db.Pregledi.Add(pregled);
        await o.H.Db.SaveChangesAsync();

        var p = await Sut(o, o.Ana).Handle(new PodnesiPredlogCommand("DP1", "dodavanje", null, "Sekcija 30: fali ventilator", "Nov", pregled.Id), default);

        p.PregledBroj.Should().Be("DP1-2026-0005");
        (await o.H.Db.PregledDogadjaji.SingleAsync()).Vrsta.Should().Be("predlog_izmene");
        (await o.H.Db.Obavestenja.SingleAsync()).Tekst.Should().Contain("iz pregleda DP1-2026-0005");
    }

    [Fact]
    public async Task Admin_dodaje_sekciju_menja_redosled_a_neaktivna_se_ne_bira_za_nov_uredjaj()
    {
        var o = await PripremiAsync();
        await using var _ = o.H;
        var sut = new GetSekcijeHandler(o.H.Db);
        var nova = await sut.Handle(new NovaSekcijaCommand("80", null), default);
        nova.Naziv.Should().Be("Sekcija 80");
        await sut.Invoking(s => s.Handle(new NovaSekcijaCommand("80", "Duplikat"), default)).Should().ThrowAsync<BusinessRuleException>();

        var sve = await sut.Handle(new GetSekcijeQuery(), default);
        var obrnuto = sve.Select(s => s.Id).Reverse().ToList();
        (await sut.Handle(new RedosledSekcijaCommand(obrnuto), default)).First().Sifra.Should().Be("80");
        await sut.Invoking(s => s.Handle(new RedosledSekcijaCommand([nova.Id]), default)).Should().ThrowAsync<BusinessRuleException>();

        await sut.Handle(new IzmeniSekcijuCommand(nova.Id, "Pakovanje", Aktivna: false), default);
        var uredjaji = new UredjajiHandler(o.H.Db);
        await uredjaji.Invoking(u => u.Handle(new KreirajUredjajCommand(new UredjajInput("Ventilator", "80-B-01", nova.Id, "Ventilator")), default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*");
    }

    [Fact]
    public async Task Posle_reseta_korisnik_mora_da_promeni_lozinku_a_promena_to_brise()
    {
        await using var h = IdentityHarness.New();
        var pera = await h.SeedUserAsync("pera", "Stara2026!", Roles.Inzenjer);

        await new ResetujLozinkuHandler(h.Users, new FakeJwtTokenService()).Handle(new ResetujLozinkuCommand(pera.Id, "OdAdmina2026!"), default);
        (await h.Users.FindByIdAsync(pera.Id))!.MoraPromenitiLozinku.Should().BeTrue();

        await new ChangePasswordHandler(h.Users, new FakeCurrentUser { UserId = pera.Id }, new FakeJwtTokenService())
            .Handle(new ChangePasswordCommand("OdAdmina2026!", "Moja2026!"), default);
        (await h.Users.FindByIdAsync(pera.Id))!.MoraPromenitiLozinku.Should().BeFalse();
    }
}
