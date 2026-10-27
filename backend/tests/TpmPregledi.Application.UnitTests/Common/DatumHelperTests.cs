using FluentAssertions;
using TpmPregledi.Application.Common;

namespace TpmPregledi.Application.UnitTests.Common;

/// <summary>KaoUtc: Utc ostaje, Local se prevodi, Unspecified se tretira kao UTC.</summary>
public class DatumHelperTests
{
    [Fact]
    public void Utc_vrednost_ostaje_ista()
    {
        var v = new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc);
        DatumHelper.KaoUtc(v).Should().Be(v).And.Subject.As<DateTime>().Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Unspecified_dobija_Kind_Utc_bez_pomeranja_sata()
    {
        var v = new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Unspecified);
        var r = DatumHelper.KaoUtc(v);
        r.Kind.Should().Be(DateTimeKind.Utc);
        r.Hour.Should().Be(10);
    }

    [Fact]
    public void Local_se_prevodi_u_isti_UTC_trenutak()
    {
        var utc   = new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();
        var r = DatumHelper.KaoUtc(local);
        r.Kind.Should().Be(DateTimeKind.Utc);
        r.Should().Be(utc);
    }
}
