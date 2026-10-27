namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Trenutni korisnik iz HTTP konteksta (JWT claims). Handleri ne znaju za <c>HttpContext</c> —
/// ovo je jedini put do „ko zove“. Implementira ga <c>CurrentUserService</c> u Infrastructure sloju.
/// </summary>
public interface ICurrentUser
{
    /// <summary>ID korisnika iz JWT-a (<c>sub</c>); null ako nije prijavljen.</summary>
    string? UserId { get; }

    /// <summary>Korisničko ime iz JWT-a (<c>preferred_username</c>).</summary>
    string? UserName { get; }

    /// <summary>Role iz JWT-a.</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>IP adresa klijenta (posle forwarded hedera).</summary>
    string? IpAddress { get; }

    /// <summary><c>true</c> ako zahtev nosi važeći token.</summary>
    bool IsAuthenticated { get; }
}
