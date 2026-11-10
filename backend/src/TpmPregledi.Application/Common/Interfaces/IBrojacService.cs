namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Dodela sledećeg broja (pregled <c>DP1-2026-0142</c>, akcija <c>A-2026-0031</c>, kvar <c>K-2026-0087</c>).
/// Poziva se u transakciji koja pravi zapis; ako ona padne, broj se ne troši.
/// </summary>
public interface IBrojacService
{
    /// <summary>Sledeći broj za vrstu u godini, već formatiran.</summary>
    /// <param name="vrsta"><c>DP1</c>…<c>PZ4</c>, <c>A</c> ili <c>K</c>.</param>
    /// <param name="godina">Godina (po datumu zapisa, ne po današnjem danu).</param>
    /// <param name="ct">Token za otkazivanje.</param>
    Task<string> SledeciAsync(string vrsta, int godina, CancellationToken ct = default);
}
