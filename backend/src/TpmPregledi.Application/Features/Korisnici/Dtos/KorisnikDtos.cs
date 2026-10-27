using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.Features.Korisnici.Dtos;

/// <summary>
/// Prikaz korisnika u admin sekciji „Korisnici“. Polja su srpska (domenski ugovor); frontend ih
/// prikazuje 1:1. Korisnik ima <b>tačno jednu</b> rolu (<see cref="Rola"/>).
/// </summary>
/// <param name="Id">GUID korisnika.</param>
/// <param name="KorisnickoIme">Korisničko ime (jedinstveno, nepromenljivo).</param>
/// <param name="Ime">Puno ime i prezime.</param>
/// <param name="Email">E-mail (opciono).</param>
/// <param name="Rola">Rola: <c>Admin</c> ili <c>Korisnik</c>.</param>
/// <param name="Aktivan">Deaktiviran korisnik ne može da se prijavi.</param>
/// <param name="PoslednjaPrijava">UTC trenutak poslednje uspešne prijave (null ako se nikad nije prijavio).</param>
public sealed record KorisnikDto(
    string Id,
    string KorisnickoIme,
    string Ime,
    string? Email,
    string Rola,
    bool Aktivan,
    DateTime? PoslednjaPrijava)
{
    /// <summary>Jedno mesto za mapiranje entitet → DTO (koriste ga svi handleri).</summary>
    public static KorisnikDto Od(ApplicationUser u, string rola) => new(
        Id:               u.Id,
        KorisnickoIme:    u.UserName ?? string.Empty,
        Ime:              u.FullName,
        Email:            u.Email,
        Rola:             rola,
        Aktivan:          u.IsActive,
        PoslednjaPrijava: u.LastLoginAt);
}

/// <summary>Telo <c>POST /api/v1/korisnici</c>.</summary>
/// <param name="KorisnickoIme">Korisničko ime (3–256 znakova; slova, cifre, <c>-._@+</c>).</param>
/// <param name="Ime">Puno ime i prezime.</param>
/// <param name="Email">E-mail (opciono).</param>
/// <param name="Lozinka">Početna lozinka (min 8, cifra, veliko i malo slovo) — odmah važi.</param>
/// <param name="Rola"><c>Admin</c> ili <c>Korisnik</c>.</param>
public sealed record KreirajKorisnikaInput(
    string KorisnickoIme,
    string Ime,
    string? Email,
    string Lozinka,
    string Rola);

/// <summary>
/// Telo <c>PUT /api/v1/korisnici/{id}</c>. Korisničko ime se ne menja (novi nalog + deaktivacija
/// starog), status aktivnosti ima zasebne akcije, lozinka zaseban reset.
/// </summary>
/// <param name="Ime">Puno ime i prezime.</param>
/// <param name="Email">E-mail (opciono).</param>
/// <param name="Rola">Nova rola — zamenjuje postojeću.</param>
public sealed record IzmeniKorisnikaInput(
    string Ime,
    string? Email,
    string Rola);

/// <summary>Telo <c>POST /api/v1/korisnici/{id}/reset-lozinka</c>. Lozinka ide u telo, nikad u URL.</summary>
/// <param name="NovaLozinka">Nova lozinka (ista politika kao pri kreiranju).</param>
public sealed record ResetLozinkeInput(string NovaLozinka);
