using TpmPregledi.Application.Common.Interfaces;

namespace TpmPregledi.Infrastructure.Time;

/// <summary>
/// Pravi sat. <see cref="Today"/> je lokalni datum u <c>Europe/Belgrade</c> zoni (Windows ID:
/// „Central European Standard Time“), jer „današnji“ poslovni dan zavisi od lokalnog kalendara, ne od UTC-a.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo Belgrade =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Belgrade");

    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Belgrade));
}
