using System.ComponentModel.DataAnnotations;

namespace TpmPregledi.Infrastructure.Identity;

/// <summary>
/// Sekcija <c>Jwt</c> iz konfiguracije (Options pattern, <c>ValidateDataAnnotations + ValidateOnStart</c>).
/// Dužinu tajne (≥ 32 bajta, bez <c>REPLACE_ME</c>) proverava fail-fast u <c>Program.cs</c> pre
/// podizanja hosta — DataAnnotations ovde su drugi sloj za ostala polja.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Ime sekcije u konfiguraciji.</summary>
    public const string SectionName = "Jwt";

    /// <summary>HMAC tajna (≥ 32 bajta; preporuka 64 random bajta base64). Produkcija: <c>Jwt__Secret</c>.</summary>
    [Required]
    public string Secret { get; init; } = string.Empty;

    /// <summary><c>iss</c> claim — slug aplikacije.</summary>
    [Required]
    public string Issuer { get; init; } = "tpm-pregledi";

    /// <summary><c>aud</c> claim — <c>&lt;slug&gt;-clients</c>.</summary>
    [Required]
    public string Audience { get; init; } = "tpm-pregledi-clients";

    /// <summary>Trajanje access tokena u minutima (standard: 15).</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Trajanje refresh tokena u danima (standard: 7).</summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 7;
}
