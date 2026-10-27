using FluentAssertions;
using TpmPregledi.Infrastructure.Version;

namespace TpmPregledi.Application.UnitTests.Version;

/// <summary>Parsiranje InformationalVersion ("1.2.3+sha") i BuildUtc, sa fallback-ovima i skraćivanjem SHA na 7.</summary>
public class AppVersionTests
{
    [Fact]
    public void Verzija_i_kratki_sha_se_razdvajaju_na_plus()
    {
        var v = new AppVersion("1.2.3+abc1234", null);
        v.Verzija.Should().Be("1.2.3");
        v.GitSha.Should().Be("abc1234");
    }

    [Fact]
    public void Pun_sha_od_40_znakova_se_skracuje_na_7()
    {
        var v = new AppVersion("1.2.3+abc1234def5678abc1234def5678abc1234de", null);
        v.GitSha.Should().Be("abc1234");
    }

    [Fact]
    public void Bez_plusa_sha_je_dev()
    {
        var v = new AppVersion("1.2.3", null);
        v.Verzija.Should().Be("1.2.3");
        v.GitSha.Should().Be("dev");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Bez_verzije_fallback_je_0_0_0_dev_i_dev(string? iv)
    {
        var v = new AppVersion(iv, null);
        v.Verzija.Should().Be("0.0.0-dev");
        v.GitSha.Should().Be("dev");
        v.BuildUtc.Should().BeNull();
    }

    [Fact]
    public void BuildUtc_se_parsira_kao_UTC()
    {
        var v = new AppVersion("1.0.0+abc", "2026-10-12T09:30:00Z");
        v.BuildUtc.Should().Be(new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Utc));
        v.BuildUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nije datum")]
    public void Neispravan_BuildUtc_je_null(string? s)
        => new AppVersion("1.0.0", s).BuildUtc.Should().BeNull();

    [Fact]
    public void Runtime_konstruktor_cita_assembly_i_ne_puca()
    {
        var v = new AppVersion();
        v.Verzija.Should().NotBeNullOrWhiteSpace();
        v.GitSha.Should().NotBeNullOrWhiteSpace();
        v.GitSha.Length.Should().BeLessThanOrEqualTo(7);
    }
}
