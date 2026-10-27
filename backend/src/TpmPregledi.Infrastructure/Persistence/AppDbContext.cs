using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Infrastructure.Persistence;

/// <summary>
/// Glavni EF Core kontekst. Nasleđuje <see cref="IdentityDbContext{TUser, TRole, TKey}"/> (Identity
/// tabele sa string PK) i implementira <see cref="IAppDbContext"/> za Application sloj.
/// </summary>
/// <remarks>
/// <para>
/// <b>Imenovanje:</b> <c>EFCore.NamingConventions</c> (<c>UseSnakeCaseNamingConvention</c>) samo
/// snake_case-uje imena koja EF izvede (iz imena <c>DbSet</c>-a, tj. množina: <c>Kategorije</c> →
/// <c>kategorije</c>). <b>Jednina</b> dolazi iz eksplicitnog <c>ToTable("kategorija")</c> — zato ga
/// svaki entitet ima. Kolone i indeksi ostaju na konvenciji (<c>created_at</c>, <c>ix_…</c>);
/// jedinstveni indeksi dobijaju eksplicitno <c>ux_…</c> ime.
/// </para>
/// <para>
/// <b>Identity tabele</b> zadržavaju <c>AspNetUsers</c>, <c>AspNetRoles</c>… (Identity ih zadaje
/// eksplicitnim <c>ToTable</c>, pa ih konvencija ne dira), dok su im kolone snake_case
/// (<c>normalized_user_name</c>). Sistemske tabele (Identity, <c>refresh_token</c>, <c>audit_log</c>)
/// imaju engleske kolone; domenske tabele srpske.
/// </para>
/// <para>
/// Konfiguracija je u <c>private static Configure*</c> metodama, ne u <c>IEntityTypeConfiguration</c>
/// klasama — manje fajlova, cela šema se čita na jednom mestu.
/// </para>
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options), IAppDbContext
{
    // ─── Sistem ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <inheritdoc />
    public DbSet<AuditLog> AuditLog => Set<AuditLog>();

    // ─── Domen ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public DbSet<Kategorija> Kategorije => Set<Kategorija>();   // PRIMER feature-a — kopiraj ili obriši

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        ConfigureIdentity(b);
        ConfigureSistem(b);
        ConfigureKategorija(b);
    }

    // ─── Identity + refresh tokeni ─────────────────────────────────────────

    private static void ConfigureIdentity(ModelBuilder b)
    {
        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_token");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.CreatedByIp).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_token_token_hash");
            e.HasIndex(x => x.UserId);
            // Cascade: korisnici se ne brišu fizički, ali ako bi se ikad obrisali, tokeni nemaju smisla bez njih.
            e.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // ─── Audit log ─────────────────────────────────────────────────────────

    private static void ConfigureSistem(ModelBuilder b)
    {
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).IsRequired().HasMaxLength(20);
            e.Property(x => x.Entity).IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.OldValues).HasColumnType("jsonb");
            e.Property(x => x.NewValues).HasColumnType("jsonb");
            e.Property(x => x.IpAddress).HasMaxLength(64);
            // user_id namerno BEZ FK ka AspNetUsers — red mora da preživi sve.
            e.HasIndex(x => new { x.Entity, x.EntityId });
            e.HasIndex(x => x.CreatedAt).IsDescending();
        });
    }

    // ─── Kategorija (PRIMER feature-a — kopiraj ili obriši) ────────────────

    private static void ConfigureKategorija(ModelBuilder b)
    {
        b.Entity<Kategorija>(e =>
        {
            e.ToTable("kategorija");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sifra).IsRequired().HasMaxLength(32);
            e.Property(x => x.Naziv).IsRequired().HasMaxLength(200);
            e.Property(x => x.Opis).HasMaxLength(1000);
            e.Property(x => x.CreatedBy).HasMaxLength(64);
            e.Property(x => x.UpdatedBy).HasMaxLength(64);
            // Šifra jedinstvena samo među aktivnima — deaktivirana sme da deli šifru sa novom.
            e.HasIndex(x => x.Sifra).IsUnique().HasFilter("aktivan = true").HasDatabaseName("ux_kategorija_sifra");
        });
    }
}
