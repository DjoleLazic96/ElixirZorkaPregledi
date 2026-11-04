using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Uređaj iz centralnog registra opreme (R2.2). Identitet je par (naziv, oznaka) tačno kako piše na obrascu:
/// šabloni DP1, DP2 i PZ1 su prepisani doslovno, pa ista oznaka ponegde ima dva naziva, a oznaka može biti
/// prazna (npr. „HL1“). Uređaj se ne briše, nego dobija status <see cref="StatusUredjaja.VanUpotrebe"/> (R2.3).
/// </summary>
[Auditable]
public class Uredjaj : AuditableEntity
{
    /// <summary>PK — stalni ID uređaja kroz sve verzije šablona.</summary>
    public int Id { get; set; }

    /// <summary>Naziv sa obrasca, npr. „Merna vaga Sautelma“.</summary>
    public string Naziv { get; set; } = string.Empty;

    /// <summary>Oznaka sa obrasca, npr. „10-C-07C“; prazan string ako je obrazac nema.</summary>
    public string Oznaka { get; set; } = string.Empty;

    /// <summary>Sekcija pogona.</summary>
    public int SekcijaId { get; set; }

    /// <summary>Navigacija na sekciju.</summary>
    public Sekcija? Sekcija { get; set; }

    /// <summary>Vrsta (Transporter, Redler, Ventilator…); pri uvozu prva reč naziva, menja se u registru.</summary>
    public string Vrsta { get; set; } = string.Empty;

    /// <summary><see cref="StatusUredjaja.Aktivan"/> ili <see cref="StatusUredjaja.VanUpotrebe"/>.</summary>
    public string Status { get; set; } = StatusUredjaja.Aktivan;

    /// <summary>Token za QR nalepnicu (R5.6); QR otvara ekran uređaja.</summary>
    public Guid QrToken { get; set; } = Guid.NewGuid();

    /// <summary>Prikaz „naziv oznaka“ kao na obrascu.</summary>
    public string Prikaz => string.IsNullOrEmpty(Oznaka) ? Naziv : $"{Naziv} {Oznaka}";
}

/// <summary>Statusi uređaja u registru.</summary>
public static class StatusUredjaja
{
    /// <summary>U upotrebi; nudi se za nove šablone.</summary>
    public const string Aktivan = "aktivan";

    /// <summary>Istorija ostaje, ali se ne nudi za nove šablone (R2.3).</summary>
    public const string VanUpotrebe = "van_upotrebe";
}

/// <summary>
/// Aktivnost iz kataloga (R2.7): naziv i tačno dva odgovora. Ista aktivnost iz više šablona je jedan red,
/// pa se ne mogu pojaviti različito napisane iste aktivnosti.
/// </summary>
[Auditable]
public class Aktivnost : AuditableEntity
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Naziv, npr. „Pregled rolni“.</summary>
    public string Naziv { get; set; } = string.Empty;

    /// <summary>Odgovor koji znači „u redu“, npr. „Okreće“.</summary>
    public string OdgovorIspravan { get; set; } = string.Empty;

    /// <summary>Odgovor koji znači kvar, npr. „Ne okreće“ (uz njega je napomena obavezna).</summary>
    public string OdgovorNeispravan { get; set; } = string.Empty;

    /// <summary>Uputstvo (R2.4) — unosi se u fazi 10.</summary>
    public string? UputstvoTekst { get; set; }

    /// <summary>Ko je poslednji izmenio uputstvo.</summary>
    public string? UputstvoAzuriraoId { get; set; }

    /// <summary>Kada je uputstvo poslednji put izmenjeno (UTC).</summary>
    public DateTime? UputstvoAzuriranoAt { get; set; }
}

/// <summary>
/// Dodatno polje uz aktivnost (R2.11): broj rolni koje ne okreću, temperatura ležaja, broj nedostajućih komada,
/// i ono što Admin doda (R2.13).
/// </summary>
[Auditable]
public class DodatnoPolje : AuditableEntity
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Naziv (jedinstven), npr. „Broj rolni koje ne okreću“.</summary>
    public string Naziv { get; set; } = string.Empty;

    /// <summary><c>broj</c>, <c>merenje</c>, <c>dve_vrednosti</c> ili <c>da_ne</c>.</summary>
    public string Tip { get; set; } = "broj";

    /// <summary>Jedinica, npr. „kom.“ ili „°C“.</summary>
    public string? Jedinica { get; set; }

    /// <summary>Oznake vrednosti za <c>dve_vrednosti</c> (npr. „ležaj do rotora“).</summary>
    public string[] Oznake { get; set; } = [];

    /// <summary><c>uvek</c> ili <c>uz_neispravan</c>.</summary>
    public string Prikaz { get; set; } = "uz_neispravan";

    /// <summary>Da li se mora popuniti kad se prikaže (tri početna polja su opciona).</summary>
    public bool Obavezno { get; set; }
}
