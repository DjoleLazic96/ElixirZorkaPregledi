using System.Reflection;
using System.Text.Json;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Common;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TpmPregledi.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Pri <c>SaveChanges</c>, u istoj transakciji kao poslovna izmena: (1) puni
/// <see cref="AuditableEntity"/> kolone (<c>CreatedAt/By</c>, <c>UpdatedAt/By</c>), (2) upisuje
/// <c>audit_log</c> red za svaki <c>[Auditable]</c> entitet u stanju Added/Modified/Deleted.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>CREATE</b> — pun snapshot novih vrednosti</item>
///   <item><b>UPDATE</b> — pun snapshot pre i posle (frontend računa diff); soft delete je UPDATE polja <c>Aktivan</c></item>
///   <item><b>DELETE</b> — pun snapshot starih vrednosti</item>
/// </list>
/// <para>
/// Osetljiva polja (<c>PasswordHash</c>, <c>SecurityStamp</c>, sve što sadrži <c>Secret</c>,
/// <c>Password</c>, <c>Lozinka</c>, <c>Token</c>…) se <b>nikad</b> ne serijalizuju. <see cref="AuditLog"/>
/// sam se preskače (anti-rekurzija).
/// </para>
/// <para>
/// <b>Ključevi koje generiše baza (identity):</b> pre INSERT-a EF drži privremenu (negativnu)
/// vrednost ključa, pa bi CREATE red dobio pogrešan <c>entity_id</c>. Zato se za takve entitete
/// <c>EntityId</c>/<c>NewValues</c> dopisuju u <c>SavedChanges</c>, kad su pravi ključevi poznati,
/// drugim malim UPDATE-om nad već upisanim audit redovima. GUID ključevi (korisnik) nisu pogođeni.
/// </para>
/// </remarks>
public sealed class AuditSaveChangesInterceptor(
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : SaveChangesInterceptor
{
    /// <summary>CREATE redovi čiji entitet ima privremeni ključ — dopunjavaju se posle INSERT-a.</summary>
    private readonly List<(AuditLog Log, EntityEntry Entry)> _odlozeni = [];

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } ctx)
            PreUpisa(ctx);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } ctx)
            PreUpisa(ctx);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } ctx && DopuniOdlozene())
            await ctx.SaveChangesAsync(cancellationToken);   // ulazi ponovo u interceptor, ali bez [Auditable] izmena
        return result;
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } ctx && DopuniOdlozene())
            ctx.SaveChanges();
        return result;
    }

    // ─── Pre upisa ─────────────────────────────────────────────────────────

    private void PreUpisa(DbContext ctx)
    {
        PopuniAuditKolone(ctx);
        DodajAuditRedove(ctx);
    }

    /// <summary>
    /// <c>ApplicationUser</c> nije <see cref="AuditableEntity"/> (ima svoj <c>CreatedAt</c>), pa ga
    /// ovo ne dira — njegove izmene idu samo u audit_log.
    /// </summary>
    private void PopuniAuditKolone(DbContext ctx)
    {
        var now = clock.UtcNow;
        var who = currentUser.UserId ?? "system";

        foreach (var entry in ctx.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = who;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = who;
            }
        }
    }

    private void DodajAuditRedove(DbContext ctx)
    {
        var redovi = new List<AuditLog>();

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue;
            if (entry.Entity.GetType().GetCustomAttribute<AuditableAttribute>() is null) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var (userId, userName) = OdrediKorisnika(entry);
            var privremenKljuc = entry.State == EntityState.Added && ImaPrivremenKljuc(entry);

            var log = new AuditLog
            {
                UserId    = userId,
                UserName  = userName,
                Action    = entry.State switch
                {
                    EntityState.Added    => "CREATE",
                    EntityState.Modified => "UPDATE",
                    _                    => "DELETE",
                },
                Entity    = entry.Entity.GetType().Name,
                EntityId  = privremenKljuc ? null : PrimarniKljuc(entry),
                OldValues = entry.State == EntityState.Added ? null : Serijalizuj(entry, p => p.OriginalValue),
                NewValues = entry.State == EntityState.Deleted || privremenKljuc ? null : Serijalizuj(entry, p => p.CurrentValue),
                CreatedAt = clock.UtcNow,
                IpAddress = currentUser.IpAddress,
            };

            redovi.Add(log);
            if (privremenKljuc)
                _odlozeni.Add((log, entry));
        }

        if (redovi.Count > 0)
            ctx.Set<AuditLog>().AddRange(redovi);
    }

    // ─── Posle upisa ───────────────────────────────────────────────────────

    /// <summary>Upisuje prave ključeve i snapshot u odložene CREATE redove; <c>true</c> ako ima šta da se sačuva.</summary>
    private bool DopuniOdlozene()
    {
        if (_odlozeni.Count == 0) return false;

        foreach (var (log, entry) in _odlozeni)
        {
            log.EntityId  = PrimarniKljuc(entry);
            log.NewValues = Serijalizuj(entry, p => p.CurrentValue);
        }

        _odlozeni.Clear();   // pre ugnježdenog SaveChanges — da se ne vrtimo u krug
        return true;
    }

    // ─── Pomoćne ───────────────────────────────────────────────────────────

    /// <summary>
    /// (1) iz JWT-a kroz <see cref="ICurrentUser"/>; (2) ako nema zahteva a menja se sam
    /// <see cref="ApplicationUser"/> (login ažurira <c>LastLoginAt</c> pre nego što postoji JWT),
    /// iz tog entiteta; (3) inače <c>"system"</c> (seed, START).
    /// </summary>
    private (string? UserId, string? UserName) OdrediKorisnika(EntityEntry entry)
    {
        if (currentUser.UserId is not null)
            return (currentUser.UserId, currentUser.UserName);

        if (entry.Entity is ApplicationUser user)
            return (user.Id, user.UserName);

        return (null, "system");
    }

    private static bool ImaPrivremenKljuc(EntityEntry entry) =>
        entry.Properties.Any(p => p.Metadata.IsPrimaryKey() && p.IsTemporary);

    /// <summary>PK vrednosti spojene sa <c>|</c> (složeni ključevi).</summary>
    private static string? PrimarniKljuc(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return null;

        var values = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
            .Where(v => v is not null);

        return string.Join("|", values);
    }

    private static readonly HashSet<string> OsetljivaTacno = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "AccessFailedCount", "LockoutEnd",
    };

    private static readonly string[] OsetljiviDelovi = ["Secret", "Password", "Lozinka", "Token"];

    /// <summary>Tačno ime iz liste ili ime koje sadrži neku od osetljivih reči.</summary>
    internal static bool JeOsetljivo(string ime) =>
        OsetljivaTacno.Contains(ime)
        || OsetljiviDelovi.Any(d => ime.Contains(d, StringComparison.OrdinalIgnoreCase));

    private static string Serijalizuj(EntityEntry entry, Func<PropertyEntry, object?> vrednost) =>
        JsonSerializer.Serialize(entry.Properties
            .Where(p => !p.Metadata.IsShadowProperty() && !JeOsetljivo(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, vrednost));
}
