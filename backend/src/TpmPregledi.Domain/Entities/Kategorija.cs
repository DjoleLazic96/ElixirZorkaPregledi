using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>
/// PRIMER feature-a — kopiraj ili obriši. Generički šifarnik „Kategorija“: šifra + naziv + opis,
/// soft-delete, audit. Pokazuje ceo put entitet → EF konfiguracija → komande/upiti → endpointi → testovi.
/// </summary>
/// <remarks>
/// Šifra je jedinstvena među <b>aktivnim</b> kategorijama (filtrirani indeks u <c>AppDbContext</c>);
/// deaktivirana kategorija sme da deli šifru sa novom. Poslovno pravilo duplikata proverava
/// handler (422), indeks je poslednja linija odbrane.
/// </remarks>
[Auditable]
public class Kategorija : SoftDeletableEntity
{
    /// <summary>PK (int identity).</summary>
    public int Id { get; set; }

    /// <summary>Kratka šifra (do 32 znaka), jedinstvena među aktivnim.</summary>
    public string Sifra { get; set; } = string.Empty;

    /// <summary>Naziv za prikaz (do 200 znakova).</summary>
    public string Naziv { get; set; } = string.Empty;

    /// <summary>Slobodan opis (do 1000 znakova), opciono.</summary>
    public string? Opis { get; set; }
}
