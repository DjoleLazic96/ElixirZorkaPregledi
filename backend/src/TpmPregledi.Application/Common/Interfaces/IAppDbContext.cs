using Microsoft.EntityFrameworkCore;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Apstrakcija nad <c>AppDbContext</c>-om za Application sloj — handleri ne zavise od
/// Infrastructure-a. Svaki novi entitet dobija <c>DbSet</c> ovde i u <c>AppDbContext</c>.
/// </summary>
public interface IAppDbContext
{
    /// <summary>Korisnici (Identity).</summary>
    DbSet<ApplicationUser> Users { get; }

    /// <summary>Refresh tokeni (heševi).</summary>
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>Audit log (append-only).</summary>
    DbSet<AuditLog> AuditLog { get; }

    /// <summary>PRIMER feature-a — kopiraj ili obriši: šifarnik kategorija.</summary>
    DbSet<Kategorija> Kategorije { get; }

    /// <summary>Snima promene (audit interceptor radi u istoj transakciji).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
