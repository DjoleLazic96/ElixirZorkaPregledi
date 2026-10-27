using FluentAssertions;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Korisnici;

namespace TpmPregledi.Application.UnitTests.Korisnici;

/// <summary>Četiri guarda (blueprint 07 §5) → 422 sa tačnim porukama. Čista logika, bez baze.</summary>
public class KorisnikGuardsTests
{
    // ─── Guard 1: sopstvena rola ────────────────────────────────────────────

    [Fact]
    public void Promena_sopstvene_role_vraca_422()
    {
        var act = () => KorisnikGuards.ZabraniPromenuSopstveneRole("u1", "u1");

        act.Should().Throw<BusinessRuleException>()
           .Which.Greske.Should().ContainSingle().Which.Should().Be("Ne možete promeniti sopstvenu rolu.");
    }

    [Fact]
    public void Promena_tudje_role_prolazi()
        => FluentActions.Invoking(() => KorisnikGuards.ZabraniPromenuSopstveneRole("u2", "u1")).Should().NotThrow();

    // ─── Guard 2: samodeaktivacija ──────────────────────────────────────────

    [Fact]
    public void Samodeaktivacija_vraca_422()
    {
        var act = () => KorisnikGuards.ZabraniSamodeaktivaciju("u1", "u1");

        act.Should().Throw<BusinessRuleException>()
           .Which.Greske.Should().ContainSingle().Which.Should().Be("Ne možete deaktivirati sami sebe.");
    }

    [Fact]
    public void Deaktivacija_drugog_prolazi_a_bez_aktera_se_ne_aktivira()
    {
        FluentActions.Invoking(() => KorisnikGuards.ZabraniSamodeaktivaciju("u2", "u1")).Should().NotThrow();
        FluentActions.Invoking(() => KorisnikGuards.ZabraniSamodeaktivaciju("u1", null)).Should().NotThrow();
    }

    // ─── Guard 3: skidanje Admin poslednjem ─────────────────────────────────

    [Fact]
    public void Skidanje_Admin_role_poslednjem_aktivnom_adminu_vraca_422()
    {
        var act = () => KorisnikGuards.ZabraniSkidanjeAdminaPoslednjem("admin1", ciljaniJeAdmin: true, ciljaniJeAktivan: true, ["admin1"]);

        act.Should().Throw<BusinessRuleException>()
           .Which.Greske.Should().ContainSingle().Which.Should().Be("Ne možete skinuti rolu Admin poslednjem aktivnom administratoru.");
    }

    [Fact]
    public void Skidanje_Admin_role_prolazi_kad_postoji_drugi_aktivan_admin()
        => FluentActions.Invoking(() => KorisnikGuards.ZabraniSkidanjeAdminaPoslednjem("admin1", true, true, ["admin1", "admin2"]))
            .Should().NotThrow();

    // ─── Guard 4: deaktivacija poslednjeg admina ────────────────────────────

    [Fact]
    public void Deaktivacija_poslednjeg_admina_vraca_422()
    {
        var act = () => KorisnikGuards.ZabraniDeaktivacijuPoslednjegAdmina("admin1", ciljaniJeAdmin: true, ciljaniJeAktivan: true, ["admin1"]);

        act.Should().Throw<BusinessRuleException>()
           .Which.Greske.Should().ContainSingle().Which.Should().Be("Ne možete deaktivirati poslednjeg aktivnog administratora.");
    }

    [Fact]
    public void Deaktivacija_prolazi_kad_cilj_nije_admin_ili_je_vec_neaktivan()
    {
        FluentActions.Invoking(() => KorisnikGuards.ZabraniDeaktivacijuPoslednjegAdmina("k1", ciljaniJeAdmin: false, ciljaniJeAktivan: true, ["admin1"]))
            .Should().NotThrow("cilj je Korisnik — ne dira admin populaciju");
        FluentActions.Invoking(() => KorisnikGuards.ZabraniDeaktivacijuPoslednjegAdmina("admin1", ciljaniJeAdmin: true, ciljaniJeAktivan: false, []))
            .Should().NotThrow("već neaktivan admin ne menja broj aktivnih");
    }
}
