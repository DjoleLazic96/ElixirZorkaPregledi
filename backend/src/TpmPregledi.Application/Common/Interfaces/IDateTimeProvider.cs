namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Apstrakcija sata — handleri nikad ne zovu <c>DateTime.UtcNow</c> direktno, da bi testovi
/// bili deterministički (<c>FakeClock</c>).
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Trenutni UTC trenutak.</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Današnji datum u <c>Europe/Belgrade</c> zoni, ne UTC — „današnji“ poslovni dan zavisi
    /// od lokalnog kalendara.
    /// </summary>
    DateOnly Today { get; }
}
