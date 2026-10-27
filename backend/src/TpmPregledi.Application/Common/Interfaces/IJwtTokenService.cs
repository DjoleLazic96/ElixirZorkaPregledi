using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Izdavanje, rotacija i opoziv tokena (access JWT + slučajan refresh token). Implementira
/// ga <c>JwtTokenService</c> u Infrastructure sloju. Metode primaju i vraćaju <b>sirovi</b>
/// refresh token; heširanje za bazu je detalj implementacije.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Izdaje access (kratkoživući JWT) i refresh (dugoživući, opaque) token.</summary>
    /// <param name="user">Korisnik za kog se izdaje par.</param>
    /// <param name="roles">Role korisnika (ulaze u JWT kao <c>role</c> claim).</param>
    /// <param name="ipAddress">IP adresa zahteva (upisuje se uz refresh token).</param>
    /// <param name="ct">Token za otkazivanje.</param>
    Task<TokenPair> IssueTokenPairAsync(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        string? ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Razmenjuje važeći refresh token za nov par (rotacija: stari se revokuje, novi upisuje,
    /// u istom <c>SaveChanges</c>-u). Vraća <c>null</c> ako je token nepoznat, revokovan ili istekao.
    /// Korisnik se vraća uz tokene da handler ne radi drugi upit.
    /// </summary>
    Task<RotateResult?> RotateAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);

    /// <summary>Revokuje jedan refresh token (odjava). Idempotentno — nepoznat token se ignoriše.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokuje <b>sve</b> važeće refresh tokene korisnika. Zove se pri resetu/promeni lozinke,
    /// deaktivaciji i promeni role — sve postojeće sesije moraju da padnu.
    /// </summary>
    Task RevokeAllAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Briše istekle i revokovane tokene korisnika. Zove se pri uspešnoj prijavi — nema
    /// pozadinskog joba, pa tabela ne raste neograničeno.
    /// </summary>
    Task DeleteInactiveAsync(string userId, CancellationToken ct = default);
}

/// <summary>Rezultat rotacije: nov par tokena + korisnik kome je izdat.</summary>
/// <param name="Tokens">Nov par tokena.</param>
/// <param name="User">Vlasnik tokena (već učitan, bez dodatnog upita).</param>
public sealed record RotateResult(TokenPair Tokens, ApplicationUser User);

/// <summary>Par tokena koji se vraća klijentu nakon prijave / refresh-a.</summary>
/// <param name="AccessToken">JWT (sub, jti, preferred_username, role).</param>
/// <param name="AccessTokenExpiresAt">UTC istek access tokena.</param>
/// <param name="RefreshToken">Sirovi refresh token — ide samo u HttpOnly cookie.</param>
/// <param name="RefreshTokenExpiresAt">UTC istek refresh tokena.</param>
public sealed record TokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
