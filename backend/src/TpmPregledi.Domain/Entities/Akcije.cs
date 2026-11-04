using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Potrebna akcija (R7.1–R7.13): šta treba uraditi na stavci (uređaj + aktivnost). Broj <c>A-2026-0031</c>; zatvara je
/// inženjer kroz pop-up na sledećem pregledu ili Supervizor/Direktor sa ekrana, uz razlog.
/// </summary>
[Auditable]
public class Akcija
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Broj, npr. <c>A-2026-0031</c>.</summary>
    public string Broj { get; set; } = string.Empty;

    /// <summary>Pregled iz kog je nastala.</summary>
    public int PregledId { get; set; }

    /// <summary>Uređaj.</summary>
    public int UredjajId { get; set; }

    /// <summary>Aktivnost.</summary>
    public int AktivnostId { get; set; }

    /// <summary>Stalni ključ stavke kroz verzije (veza sa kvarom, R7.13).</summary>
    public Guid StavkaKljuc { get; set; }

    /// <summary><c>ocistiti</c>, <c>podmazati</c>, <c>zameniti</c>, <c>popraviti</c>, <c>ostalo</c>.</summary>
    public string Vrsta { get; set; } = "ostalo";

    /// <summary><c>hitno</c>, <c>7_dana</c>, <c>sledeci_pz</c>, <c>pratiti</c>.</summary>
    public string Hitnost { get; set; } = "pratiti";

    /// <summary>Kratak opis (opciono).</summary>
    public string? Opis { get; set; }

    /// <summary>Rok (iz hitnosti); null = bez roka.</summary>
    public DateOnly? Rok { get; set; }

    /// <summary><c>otvorena</c>, <c>zatvorena</c>, <c>otkazana</c>.</summary>
    public string Status { get; set; } = "otvorena";

    /// <summary>Zaduženi: osoba ili služba (opciono, R7.12).</summary>
    public string? Zaduzen { get; set; }

    /// <summary>Ko je otvorio.</summary>
    public string KreiraoId { get; set; } = string.Empty;

    /// <summary>Kada (UTC).</summary>
    public DateTime KreiranoAt { get; set; }

    /// <summary>Ko je zatvorio ili otkazao.</summary>
    public string? ZatvorioId { get; set; }

    /// <summary>Kada je zatvorena (UTC).</summary>
    public DateTime? ZatvorenoAt { get; set; }

    /// <summary>Razlog zatvaranja ili otkazivanja.</summary>
    public string? Razlog { get; set; }

    /// <summary>Kvar na stavci je zatvoren dok je akcija još otvorena (R7.15).</summary>
    public bool StavkaSadaIspravna { get; set; }
}

/// <summary>Događaj u istoriji akcije: otvorena, „ostaje otvorena“, dodela zaduženog, zatvaranje, otkazivanje.</summary>
public class AkcijaDogadjaj
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Akcija.</summary>
    public int AkcijaId { get; set; }

    /// <summary>Navigacija.</summary>
    public Akcija? Akcija { get; set; }

    /// <summary>Vrsta.</summary>
    public string Vrsta { get; set; } = string.Empty;

    /// <summary>Tekst za prikaz.</summary>
    public string Tekst { get; set; } = string.Empty;

    /// <summary>Ko.</summary>
    public string? KorisnikId { get; set; }

    /// <summary>Pregled kroz koji je nastao događaj (pop-up).</summary>
    public int? PregledId { get; set; }

    /// <summary>Kada (UTC).</summary>
    public DateTime At { get; set; }
}

/// <summary>
/// Kvar na stavci (R8.1, R8.7, R8.15): otvoren od pregleda koji ga je prijavio do prvog pregleda sa „ispravno“. Izveden iz
/// odgovora, ali materijalizovan zbog broja i analitike.
/// </summary>
public class Kvar
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Broj, npr. <c>K-2026-0087</c>.</summary>
    public string Broj { get; set; } = string.Empty;

    /// <summary>Uređaj.</summary>
    public int UredjajId { get; set; }

    /// <summary>Aktivnost.</summary>
    public int AktivnostId { get; set; }

    /// <summary>Stalni ključ stavke.</summary>
    public Guid StavkaKljuc { get; set; }

    /// <summary>Pregled koji je prijavio kvar.</summary>
    public int OtvorioPregledId { get; set; }

    /// <summary>Pregled koji je rekao „ispravno“.</summary>
    public int? ZatvorioPregledId { get; set; }

    /// <summary>Poslednji pregled koji je potvrdio kvar (da dorada istog pregleda ne broji prijavu dva puta).</summary>
    public int PoslednjiPregledId { get; set; }

    /// <summary>Dan otvaranja (planirani dan pregleda).</summary>
    public DateOnly Otvoren { get; set; }

    /// <summary>Dan zatvaranja.</summary>
    public DateOnly? Zatvoren { get; set; }

    /// <summary>Koliko pregleda je prijavilo kvar.</summary>
    public int Prijava { get; set; }

    /// <summary>Poslednja napomena.</summary>
    public string? PoslednjaNapomena { get; set; }
}
