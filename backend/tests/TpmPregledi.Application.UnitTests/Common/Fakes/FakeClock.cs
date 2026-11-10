using TpmPregledi.Application.Common.Interfaces;

namespace TpmPregledi.Application.UnitTests.Common.Fakes;

/// <summary>Deterministički sat; <see cref="UtcNow"/> se može pomeriti u testu.</summary>
public sealed class FakeClock(DateTime? utcNow = null) : IDateTimeProvider
{
    public static readonly DateTime Podrazumevano = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Dan posle svih probnih pregleda u testovima — pregled se započinje najranije na planirani dan.</summary>
    public static readonly DateTime PosleRasporeda = new(2026, 12, 31, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; } = utcNow ?? Podrazumevano;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}
