using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Potrebna akcija (R7.1–R7.13): šta treba uraditi na stavci (uređaj + aktivnost). Broj <c>A-2026-0031</c>; zatvara je
/// inženjer kroz pop-up na sledećem pregledu, Supervizor/Direktor sa ekrana ili odobravanjem zahteva inženjera — uz podatke
/// šta je urađeno, kada i ko je uradio (zamerka 49).
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

    /// <summary>Šta je urađeno (zatvaranje van pregleda, zamerka 49).</summary>
    public string? Uradjeno { get; set; }

    /// <summary>Kada je urađeno.</summary>
    public DateOnly? UradjenoDatum { get; set; }

    /// <summary>Ko je uradio: inženjer, drugi inženjer ili služba (tekst za prikaz).</summary>
    public string? Izvrsilac { get; set; }

    /// <summary>Slika uz zatvaranje (opciono).</summary>
    public long? ZatvaranjeFajlId { get; set; }
}

/// <summary>
/// Zahtev inženjera za zatvaranje akcije van pregleda (zamerka 49): šta je urađeno, kada, ko i po želji slika. Odlučuju
/// Supervizor ili Direktor (pravo <c>akcije.zatvaranje</c>); dok čeka odluku podnosilac može da ga otkaže.
/// </summary>
[Auditable]
public class ZahtevZatvaranjaAkcije
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Akcija.</summary>
    public int AkcijaId { get; set; }

    /// <summary>ID sa telefona — ponovljeno slanje (rad bez interneta) ne pravi duplikat.</summary>
    public Guid KlijentId { get; set; }

    /// <summary>Ko traži.</summary>
    public string PodneoId { get; set; } = string.Empty;

    /// <summary>Šta je urađeno.</summary>
    public string Uradjeno { get; set; } = string.Empty;

    /// <summary>Kada je urađeno.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Ko je uradio.</summary>
    public string Izvrsilac { get; set; } = string.Empty;

    /// <summary>Slika (opciono).</summary>
    public long? FajlId { get; set; }

    /// <summary><c>zahtev</c>, <c>odobreno</c>, <c>odbijeno</c>, <c>otkazano</c>.</summary>
    public string Status { get; set; } = "zahtev";

    /// <summary>Razlog odbijanja.</summary>
    public string? Razlog { get; set; }

    /// <summary>Ko je odlučio.</summary>
    public string? OdlucioId { get; set; }

    /// <summary>Kada je podnet (UTC).</summary>
    public DateTime KreiranoAt { get; set; }

    /// <summary>Kada je odlučeno ili otkazano (UTC).</summary>
    public DateTime? OdlucenoAt { get; set; }
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
