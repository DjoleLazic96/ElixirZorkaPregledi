namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Poslednji dodeljen redni broj po vrsti i godini (R6.9, R7.11, R8.15). Red se zaključava
/// (<c>FOR UPDATE</c>) pri dodeli, pa dva zahteva ne dobiju isti broj; poništen broj se ne vraća.
/// </summary>
public class Brojac
{
    /// <summary>Vrsta: <c>DP1</c>…<c>PZ4</c>, <c>A</c> (akcija), <c>K</c> (kvar).</summary>
    public string Vrsta { get; set; } = string.Empty;

    /// <summary>Godina (brojanje kreće od 1 svake godine).</summary>
    public int Godina { get; set; }

    /// <summary>Poslednji dodeljen redni broj.</summary>
    public int Poslednji { get; set; }

    /// <summary>Format broja za prikaz: <c>DP1-2026-0142</c>, <c>A-2026-0031</c>.</summary>
    public static string Formatiraj(string vrsta, int godina, int redni) => $"{vrsta}-{godina}-{redni:D4}";
}
