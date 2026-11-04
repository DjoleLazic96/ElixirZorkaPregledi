namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Sekcija pogona kao na obrascima („10“…„70“). Seed u migraciji; menja se retko, pa nema ekrana za izmenu.
/// </summary>
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
}
