using TpmPregledi.Application.Common.Interfaces;

namespace TpmPregledi.Application.UnitTests.Common.Fakes;

/// <summary>Deterministički sat; <see cref="UtcNow"/> se može pomeriti u testu.</summary>
public sealed class FakeClock(DateTime? utcNow = null) : IDateTimeProvider
{
    public static readonly DateTime Podrazumevano = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; } = utcNow ?? Podrazumevano;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}
