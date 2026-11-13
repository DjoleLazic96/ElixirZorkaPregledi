using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Sekcija pogona kao na obrascima („10“…„70“). Početne su seed u migraciji; nove dodaje i uređuje Admin (zamerka 33).
/// </summary>
[Auditable]
public class Sekcija
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Šifra sa obrasca, npr. „21“ (jedinstvena).</summary>
    public string Sifra { get; set; } = string.Empty;

    /// <summary>Naziv za prikaz; za sada „Sekcija 21“.</summary>
    public string Naziv { get; set; } = string.Empty;

    /// <summary>Redosled prikaza.</summary>
    public int Redosled { get; set; }

    /// <summary>
    /// Neaktivna sekcija se ne nudi za nove uređaje (zamerka 33); ne briše se, jer na nju pokazuju uređaji i stari pregledi.
    /// </summary>
    public bool Aktivna { get; set; } = true;
}
