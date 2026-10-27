using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TpmPregledi.Infrastructure.Identity;

/// <summary>
/// Izdavanje, rotacija i opoziv tokena.
/// </summary>
/// <remarks>
/// <para><b>Access token</b>: JWT HS256 sa <c>sub</c>, <c>jti</c>, <c>preferred_username</c> i <c>role</c> claim-ovima.</para>
/// <para><b>Refresh token</b>: 32 slučajna bajta (base64url), ne JWT. U bazi je samo SHA-256 heš
/// (<see cref="RefreshTokenHasher"/>) — svaki lookup hešira vrednost iz cookie-ja.</para>
/// <para><b>Rotacija</b>: stari token dobija <c>RevokedAt</c>, novi se upisuje — u istom
/// <c>SaveChanges</c>-u, pa ne može da se desi „stari revokovan, novi nije izdat“.</para>
/// </remarks>
public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider clock)
    : IJwtTokenService
{
    private readonly JwtOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task<TokenPair> IssueTokenPairAsync(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var (access, accessExp) = CreateAccessToken(user, roles);
        var (raw, entity) = NoviRefreshToken(user.Id, ipAddress);

        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);

        return new TokenPair(access, accessExp, raw, entity.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<RotateResult?> RotateAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var hash = RefreshTokenHasher.Hash(refreshToken);
        var existing = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        var now = clock.UtcNow;
        if (existing is null || !existing.JeVazeci(now) || existing.User is null || !existing.User.IsActive)
            return null;

        // Rotacija: revoke starog + insert novog idu u ISTOM SaveChanges-u (atomarno).
        // Ako se ovo ikad refaktoriše, ne razbijati u dva poziva.
        existing.RevokedAt = now;
        var (raw, fresh) = NoviRefreshToken(existing.User.Id, ipAddress);
        db.RefreshTokens.Add(fresh);
        await db.SaveChangesAsync(ct);

        var roles = (await userManager.GetRolesAsync(existing.User)).ToList();
        var (access, accessExp) = CreateAccessToken(existing.User, roles);

        return new RotateResult(new TokenPair(access, accessExp, raw, fresh.ExpiresAt), existing.User);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenHasher.Hash(refreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (existing is null || existing.RevokedAt is not null) return;

        existing.RevokedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RevokeAllAsync(string userId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        // ExecuteUpdate: jedan UPDATE bez učitavanja redova; RefreshToken nije [Auditable],
        // pa zaobilaženje interceptora ništa ne gubi.
        await db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, now), ct);
    }

    /// <inheritdoc />
    public async Task DeleteInactiveAsync(string userId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await db.RefreshTokens
            .Where(r => r.UserId == userId && (r.RevokedAt != null || r.ExpiresAt < now))
            .ExecuteDeleteAsync(ct);
    }

    // ─── Pomoćne ───────────────────────────────────────────────────────────

    private (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user, IReadOnlyList<string> roles)
    {
        var now     = clock.UtcNow;
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("preferred_username", user.UserName ?? string.Empty),
        };
        // Kratko ime "role" eksplicitno: JwtSecurityTokenHandler-ov outbound map NE skraćuje ClaimTypes.Role
        // (ostao bi dugački URI u tokenu). JwtBearer ga pri validaciji vraća u ClaimTypes.Role, pa
        // RequireRole radi, a token na žici odgovara ugovoru (sub, jti, preferred_username, role).
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var signing = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _opts.Issuer,
            audience:           _opts.Audience,
            claims:             claims,
            notBefore:          now,
            expires:            expires,
            signingCredentials: signing);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>Generiše sirovi token i entitet sa njegovim hešom; sirovi token ide samo klijentu.</summary>
    private (string Raw, RefreshToken Entity) NoviRefreshToken(string userId, string? ipAddress)
    {
        var raw = GenerateSecureToken();
        var entity = new RefreshToken
        {
            TokenHash   = RefreshTokenHasher.Hash(raw),
            UserId      = userId,
            CreatedAt   = clock.UtcNow,
            ExpiresAt   = clock.UtcNow.AddDays(_opts.RefreshTokenDays),
            CreatedByIp = ipAddress,
        };
        return (raw, entity);
    }

    /// <summary>256 bita kriptografski sigurnog randoma, base64url bez padding-a (pogodno za cookie).</summary>
    private static string GenerateSecureToken()
    {
        Span<byte> buf = stackalloc byte[32];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
