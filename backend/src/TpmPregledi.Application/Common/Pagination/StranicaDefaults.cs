namespace TpmPregledi.Application.Common.Pagination;

/// <summary>
/// Pravila paginacije zajednička za sve liste: podrazumevana i maksimalna veličina stranice.
/// Maksimum postoji da jedan zahtev ne može da izvuče celu tabelu.
/// </summary>
public static class StranicaDefaults
{
    /// <summary>Podrazumevana veličina stranice kad klijent ne pošalje <c>velicina</c>.</summary>
    public const int Default = 50;

    /// <summary>Gornja granica; veće vrednosti se tiho seku na ovo.</summary>
    public const int Max = 200;

    /// <summary>
    /// Normalizuje ulazne parametre: stranica &lt; 1 → 1; veličina &lt; 1 → <see cref="Default"/>,
    /// veličina &gt; <see cref="Max"/> → <see cref="Max"/>. Tiho, bez greške — tipo na frontu
    /// ne sme da obori prikaz.
    /// </summary>
    public static (int Stranica, int Velicina) Normalizuj(int stranica, int velicina)
    {
        var s = stranica < 1 ? 1 : stranica;
        var v = velicina < 1 ? Default : Math.Min(velicina, Max);
        return (s, v);
    }
}
