namespace TpmPregledi.Domain.Identity;

/// <summary>
/// Prava pristupa (R1.5, R1.6): Admin ih štiklira po korisniku, a rola daje samo podrazumevani
/// skup pri kreiranju naloga i promeni role. U JWT-u su claim <c>pravo</c>; politike se zovu
/// <c>Pravo:&lt;pravo&gt;</c>. Matrica je u <c>docs/04-roles-and-permissions.md</c>.
/// </summary>
public static class Prava
{
    /// <summary>Popunjavanje pregleda — samo Inženjer (R1.8), ne može se dodeliti drugima.</summary>
    public const string PreglediPopunjavanje = "pregledi.popunjavanje";

    /// <summary>Analitika.</summary>
    public const string Analitika = "analitika";

    /// <summary>Overa, vraćanje na doradu, poništavanje.</summary>
    public const string Overa = "overa";

    /// <summary>Raspored, PZ, prebacivanja.</summary>
    public const string RasporedUredjivanje = "raspored.uredjivanje";

    /// <summary>Unos i odobravanje odsustava.</summary>
    public const string OdsustvaOdobravanje = "odsustva.odobravanje";

    /// <summary>Zatvaranje, otkazivanje i dodela akcija.</summary>
    public const string AkcijeZatvaranje = "akcije.zatvaranje";

    /// <summary>Registar opreme, katalog aktivnosti, nacrt šablona.</summary>
    public const string SabloniNacrt = "sabloni.nacrt";

    /// <summary>Objava nove verzije šablona i odluka o predlozima.</summary>
    public const string SabloniObjava = "sabloni.objava";

    /// <summary>Neradni dani i praznici.</summary>
    public const string NeradniDani = "neradni_dani";

    /// <summary>Korisnici i prava.</summary>
    public const string Korisnici = "korisnici";

    /// <summary>Podešavanja aplikacije.</summary>
    public const string Podesavanja = "podesavanja";

    /// <summary>Sva prava, redom kao u matrici (i u dijalogu „Prava“).</summary>
    public static readonly IReadOnlyList<string> Sva =
    [
        PreglediPopunjavanje, Analitika, Overa, RasporedUredjivanje, OdsustvaOdobravanje, AkcijeZatvaranje,
        SabloniNacrt, SabloniObjava, NeradniDani, Korisnici, Podesavanja,
    ];

    /// <summary>Podrazumevani skup prava za rolu (R1.5).</summary>
    public static IReadOnlyList<string> Podrazumevana(string rola) => rola switch
    {
        Roles.Inzenjer   => [PreglediPopunjavanje, Analitika],
        Roles.Supervizor => [Analitika, Overa, RasporedUredjivanje, OdsustvaOdobravanje, AkcijeZatvaranje],
        Roles.Direktor   => [Analitika, Overa, RasporedUredjivanje, OdsustvaOdobravanje, AkcijeZatvaranje,
                             SabloniNacrt, SabloniObjava, NeradniDani],
        Roles.Admin      => [Analitika, SabloniNacrt, NeradniDani, Korisnici, Podesavanja],
        _                => [],
    };
}
