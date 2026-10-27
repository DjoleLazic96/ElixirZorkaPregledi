using TpmPregledi.Application.Features.Audit.Dtos;

namespace TpmPregledi.Application.Features.Audit;

/// <summary>
/// Mapira ime entiteta iz <c>audit_log.entity</c> (CLR ime ili sintetičko <c>Auth</c>/<c>Sistem</c>)
/// na srpsku labelu za prikaz. Backend je izvor istine za labele — frontend ih prikazuje 1:1.
/// Svaki novi <c>[Auditable]</c> entitet dobija red ovde.
/// </summary>
public static class EntitetLabele
{
    private static readonly IReadOnlyDictionary<string, string> Mapa = new Dictionary<string, string>
    {
        ["ApplicationUser"] = "Korisnik",
        ["Kategorija"]      = "Kategorija",   // PRIMER feature-a — kopiraj ili obriši
        ["Auth"]            = "Prijava",
        ["Sistem"]          = "Sistem",
    };

    /// <summary>Labela za dati entitet; nepoznato → sam <paramref name="entitet"/> (bezbedan fallback).</summary>
    public static string Za(string entitet) =>
        Mapa.TryGetValue(entitet, out var labela) ? labela : entitet;

    /// <summary>Sve poznate vrednosti za filter „Entitet“ (<c>GET /audit/entiteti</c>), sortirane po labeli.</summary>
    public static IReadOnlyList<EntitetLabelaDto> Sve() =>
        Mapa.Select(p => new EntitetLabelaDto(p.Key, p.Value))
            .OrderBy(e => e.Labela, StringComparer.Create(new System.Globalization.CultureInfo("sr-Latn-RS"), ignoreCase: true))
            .ToList();
}
