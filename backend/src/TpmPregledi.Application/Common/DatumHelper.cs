namespace TpmPregledi.Application.Common;

/// <summary>Pomoćne funkcije za vreme koje dele svi upiti sa datumskim filterima.</summary>
public static class DatumHelper
{
    /// <summary>
    /// Normalizuje <see cref="DateTime"/> na <see cref="DateTimeKind.Utc"/> pre upita nad
    /// <c>timestamptz</c> kolonom. Npgsql odbija <c>Kind=Unspecified</c> (zone-less query string
    /// kao <c>?od=2026-07-24T00:00:00</c> se binduje baš tako) i bez ovoga upit puca sa 500.
    /// Unspecified tretiramo kao UTC (API ugovor: granice su UTC), Local prevodimo u UTC.
    /// </summary>
    public static DateTime KaoUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc   => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _                  => DateTime.SpecifyKind(v, DateTimeKind.Utc),
    };
}
