using System.Security.Cryptography;
using System.Text;

namespace TpmPregledi.Infrastructure.Identity;

/// <summary>
/// SHA-256 heš refresh tokena za kolonu <c>refresh_token.token_hash</c>. Token je 256 bita
/// kriptografski slučajan, pa je običan SHA-256 (bez soli i bez sporog KDF-a) dovoljan — nema
/// šta da se pogađa rečnikom, a lookup po hešu mora biti brz i deterministički.
/// </summary>
public static class RefreshTokenHasher
{
    /// <summary>Hex (lowercase, 64 znaka) SHA-256 heš sirovog tokena.</summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
