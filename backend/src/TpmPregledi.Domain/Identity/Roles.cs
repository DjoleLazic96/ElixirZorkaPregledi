namespace TpmPregledi.Domain.Identity;

/// <summary>
/// Imena rola — fiksne konstante (ASCII u bazi i na žici; labele sa dijakritikama su u
/// frontend mapi). Seed-uju se pri startu, nema dinamičkih rola u bazi.
/// </summary>
/// <remarks>
/// Svaki korisnik ima <b>tačno jednu</b> rolu. Rola određuje samo podrazumevani skup prava
/// (<see cref="Prava.Podrazumevana"/>); šta korisnik sme proverava se po pravu (odluka 2026-11-04).
/// </remarks>
public static class Roles
{
    /// <summary>Radi preglede na telefonu; jedini koji popunjava pregled (R1.8).</summary>
    public const string Inzenjer = "Inzenjer";

    /// <summary>Raspored, overa, dorada, akcije.</summary>
    public const string Supervizor = "Supervizor";

    /// <summary>Sve što i Supervizor, plus šabloni i objava verzija.</summary>
    public const string Direktor = "Direktor";

    /// <summary>Korisnici, prava, podešavanja, audit log, stanje servera.</summary>
    public const string Admin = "Admin";

    /// <summary>Sve role u sistemu — koristi ih seed i validacija ulaza.</summary>
    public static readonly IReadOnlyList<string> All = [Inzenjer, Supervizor, Direktor, Admin];
}
