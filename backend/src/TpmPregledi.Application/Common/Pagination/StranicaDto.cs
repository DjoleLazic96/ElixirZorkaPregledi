namespace TpmPregledi.Application.Common.Pagination;

/// <summary>
/// Jedna stranica paginirane liste. Frontend računa broj strana iz <see cref="Ukupno"/> i
/// <see cref="Velicina"/>. Koristi se za sve liste koje mogu da narastu preko ~200 redova
/// (audit, istorija, veliki šifarnici).
/// </summary>
/// <typeparam name="T">Tip reda (DTO).</typeparam>
/// <param name="Stavke">Redovi na traženoj stranici.</param>
/// <param name="Ukupno">Ukupan broj redova koji zadovoljavaju filter (pre paginacije).</param>
/// <param name="Stranica">Redni broj stranice (1-baziran, posle normalizacije).</param>
/// <param name="Velicina">Efektivna veličina stranice (posle normalizacije na 1–200).</param>
public sealed record StranicaDto<T>(
    IReadOnlyList<T> Stavke,
    int Ukupno,
    int Stranica,
    int Velicina);
