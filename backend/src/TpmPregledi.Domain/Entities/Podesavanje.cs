using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Jedno podešavanje aplikacije (ključ → JSON vrednost). Ključevi i tipovi su u
/// <c>Application/Features/Podesavanja/PodesavanjaKljucevi.cs</c>; API prima i vraća tipiziran DTO.
/// </summary>
[Auditable]
public class Podesavanje
{
    /// <summary>Ključ, npr. <c>granica_pauze_min</c>.</summary>
    public string Kljuc { get; set; } = string.Empty;

    /// <summary>Vrednost kao JSON (<c>jsonb</c>).</summary>
    public string Vrednost { get; set; } = "null";
}
