namespace TpmPregledi.Domain.Identity;

/// <summary>
/// Dugoživeći refresh token kojim korisnik dobija nov access token (JWT). Čuva se u bazi
/// da bi mogao da se revokuje (odjava, promena lozinke, deaktivacija, promena role).
/// </summary>
/// <remarks>
/// <para>
/// U bazi je <b>samo SHA-256 heš</b> (<see cref="TokenHash"/>), nikad sirovi token — sirovi
/// token (32 slučajna bajta, base64url) putuje isključivo u HttpOnly cookie-ju. Ako baza
/// procuri, heševi se ne mogu iskoristiti za prijavu.
/// </para>
/// <para>
/// Rotacija: pri svakom refresh-u postojeći token dobija <see cref="RevokedAt"/>, a novi se
/// upisuje u istom <c>SaveChanges</c>-u. Ukradeni token prestaje da važi pri sledećem
/// refresh-u pravog korisnika.
/// </para>
/// </remarks>
public class RefreshToken
{
    /// <summary>PK (bigint identity — tabela realno raste).</summary>
    public long Id { get; set; }

    /// <summary>SHA-256 heš sirovog tokena, hex (64 znaka). Jedinstven.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>FK ka <see cref="ApplicationUser"/>.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Navigacija ka korisniku.</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>UTC trenutak isteka.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC trenutak izdavanja.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC trenutak revokacije (odjava, rotacija, promena lozinke…); null = važeći.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>IP adresa sa koje je token izdat (dijagnostika).</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Da li token važi u datom trenutku (nije revokovan i nije istekao). Vreme dolazi spolja zbog testova.</summary>
    public bool JeVazeci(DateTime utcNow) => RevokedAt is null && utcNow < ExpiresAt;
}
