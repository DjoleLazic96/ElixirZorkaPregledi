namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Jedan red dnevnika: ko je, kada, šta i odakle promenio. Pune ga
/// <c>AuditSaveChangesInterceptor</c> (izmene <c>[Auditable]</c> entiteta) i
/// <c>IAuditDogadjaji</c> (prijave, odjave, start aplikacije).
/// </summary>
/// <remarks>
/// <para>
/// Tabela je <b>append-only</b>: nema UPDATE/DELETE endpointa ni UI-ja; čuva se sve.
/// Sam entitet <strong>nije</strong> <c>[Auditable]</c> (rekurzija).
/// </para>
/// <para>
/// <see cref="UserName"/> je snapshot i <see cref="UserId"/> je bez FK — red preživi
/// deaktivaciju i bilo kakvu promenu korisnika.
/// </para>
/// </remarks>
public class AuditLog
{
    /// <summary>PK (bigint identity).</summary>
    public long Id { get; set; }

    /// <summary>ID korisnika koji je izazvao događaj (null za sistem i neuspešne prijave). Bez FK.</summary>
    public string? UserId { get; set; }

    /// <summary>Snapshot korisničkog imena u trenutku događaja (<c>"system"</c> za seed/START).</summary>
    public string? UserName { get; set; }

    /// <summary>
    /// <c>CREATE</c>, <c>UPDATE</c>, <c>DELETE</c> (interceptor); <c>LOGIN</c>, <c>LOGIN_FAIL</c>,
    /// <c>LOGOUT</c>, <c>START</c> (događaji).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>CLR ime entiteta (npr. <c>Kategorija</c>) ili sintetičko <c>Auth</c> / <c>Sistem</c>.</summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>PK izmenjenog entiteta kao string (složeni ključevi spojeni sa <c>|</c>).</summary>
    public string? EntityId { get; set; }

    /// <summary>JSON snapshot pre izmene (null za CREATE i događaje).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot posle izmene (null za DELETE; za START nosi verziju).</summary>
    public string? NewValues { get; set; }

    /// <summary>UTC trenutak događaja.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>IP adresa klijenta (zahteva ispravne forwarded hedere iza proxy-ja).</summary>
    public string? IpAddress { get; set; }
}
