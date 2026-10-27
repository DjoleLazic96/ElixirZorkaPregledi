namespace TpmPregledi.Domain.Identity;

/// <summary>
/// Imena rola — fiksne konstante (ASCII u bazi i na žici; labele sa dijakritikama su u
/// frontend mapi). Seed-uju se pri startu, nema dinamičkih rola ni permisija u bazi.
/// </summary>
/// <remarks>
/// Svaki korisnik ima <b>tačno jednu</b> rolu. Nova rola = konstanta ovde + politika u
/// <c>Program.cs</c> + red u matrici prava (<c>docs/04</c>) + testovi; migracija podataka nije potrebna.
/// </remarks>
public static class Roles
{
    /// <summary>Pun pristup: administracija korisnika, šifarnici, audit log, stanje servera.</summary>
    public const string Admin = "Admin";

    /// <summary>Operativna rola: čita šifarnike i radi sa poslovnim podacima, bez administracije.</summary>
    public const string Korisnik = "Korisnik";

    /// <summary>Sve role u sistemu — koristi ih seed i validacija ulaza.</summary>
    public static readonly IReadOnlyList<string> All = [Admin, Korisnik];
}
