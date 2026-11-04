using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>Jedan od šest šablona (R2.1): DP1, DP2, PZ1–PZ4. Seed u migraciji.</summary>
public class Sablon
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Tip: <c>DP1</c>…<c>PZ4</c> (jedinstven).</summary>
    public string Tip { get; set; } = string.Empty;

    /// <summary>Svi tipovi, redom kao na ekranu.</summary>
    public static readonly IReadOnlyList<string> Tipovi = ["DP1", "DP2", "PZ1", "PZ2", "PZ3", "PZ4"];
}

/// <summary>
/// Verzija šablona (R2.5). Objavljena verzija se nikad ne menja; izmena pravi novu (faza 10), a započet
/// pregled se završava po svojoj verziji.
/// </summary>
[Auditable]
public class SablonVerzija
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Šablon.</summary>
    public int SablonId { get; set; }

    /// <summary>Navigacija na šablon.</summary>
    public Sablon? Sablon { get; set; }

    /// <summary>Redni broj verzije u šablonu (1, 2, …).</summary>
    public int Broj { get; set; }

    /// <summary><see cref="StatusVerzije"/>.</summary>
    public string Status { get; set; } = StatusVerzije.Nacrt;

    /// <summary>Datum od kog važi (objavljena verzija).</summary>
    public DateOnly? VaziOd { get; set; }

    /// <summary>Ko je objavio.</summary>
    public string? ObjavioId { get; set; }

    /// <summary>Kada je objavljena (UTC).</summary>
    public DateTime? ObjavljenoAt { get; set; }

    /// <summary>Stavke verzije.</summary>
    public List<SablonStavka> Stavke { get; set; } = [];
}

/// <summary>Statusi verzije šablona.</summary>
public static class StatusVerzije
{
    /// <summary>U pripremi; menja se.</summary>
    public const string Nacrt = "nacrt";

    /// <summary>Važeća; ne menja se nikad.</summary>
    public const string Objavljena = "objavljena";

    /// <summary>Bila je važeća, zamenila ju je novija.</summary>
    public const string Zamenjena = "zamenjena";
}

/// <summary>
/// Stavka verzije: sekcija → uređaj → aktivnost, sa redosledom. <see cref="Kljuc"/> je stalni ID stavke kroz verzije
/// (ista stavka u verziji 2 ima isti ključ kao u verziji 1), da analitika prati stavku i kad se šablon menja.
/// </summary>
public class SablonStavka
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Verzija.</summary>
    public int VerzijaId { get; set; }

    /// <summary>Navigacija na verziju.</summary>
    public SablonVerzija? Verzija { get; set; }

    /// <summary>Sekcija u kojoj stavka stoji na obrascu.</summary>
    public int SekcijaId { get; set; }

    /// <summary>Uređaj iz registra.</summary>
    public int UredjajId { get; set; }

    /// <summary>Navigacija na uređaj.</summary>
    public Uredjaj? Uredjaj { get; set; }

    /// <summary>Aktivnost iz kataloga.</summary>
    public int AktivnostId { get; set; }

    /// <summary>Navigacija na aktivnost.</summary>
    public Aktivnost? Aktivnost { get; set; }

    /// <summary>Redosled u šablonu (kao na obrascu).</summary>
    public int Redosled { get; set; }

    /// <summary>Stalni ID stavke kroz verzije.</summary>
    public Guid Kljuc { get; set; } = Guid.NewGuid();

    /// <summary>Dodatna polja koja se prikazuju uz ovu stavku.</summary>
    public List<SablonStavkaPolje> Polja { get; set; } = [];
}

/// <summary>Dodatno polje na stavci (kolona „Dodatno polje“ šablona za uvoz).</summary>
public class SablonStavkaPolje
{
    /// <summary>Stavka.</summary>
    public int StavkaId { get; set; }

    /// <summary>Dodatno polje.</summary>
    public int DodatnoPoljeId { get; set; }

    /// <summary>Navigacija na polje.</summary>
    public DodatnoPolje? DodatnoPolje { get; set; }
}

/// <summary>Događaj u „Istorijatu šablona“ (R2.9): prvi uvoz, izmene nacrta, predlozi, objave.</summary>
public class SablonDogadjaj
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Šablon.</summary>
    public int SablonId { get; set; }

    /// <summary>Verzija na koju se odnosi (ako postoji).</summary>
    public int? VerzijaId { get; set; }

    /// <summary>Vrsta, npr. <c>prvi_uvoz</c>.</summary>
    public string Vrsta { get; set; } = string.Empty;

    /// <summary>Opis za prikaz.</summary>
    public string Opis { get; set; } = string.Empty;

    /// <summary>Ko (null = sistem).</summary>
    public string? KorisnikId { get; set; }

    /// <summary>Kada (UTC).</summary>
    public DateTime At { get; set; }
}

/// <summary>Fajl na volumenu (slike pregleda i uputstava, faze 04 i 10).</summary>
public class Fajl
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Relativna putanja na volumenu.</summary>
    public string Putanja { get; set; } = string.Empty;

    /// <summary>MIME tip.</summary>
    public string Mime { get; set; } = string.Empty;

    /// <summary>Veličina u bajtovima.</summary>
    public long Velicina { get; set; }

    /// <summary>SHA-256 sadržaja (hex).</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Kada je upisan (UTC).</summary>
    public DateTime KreiranoAt { get; set; }
}
