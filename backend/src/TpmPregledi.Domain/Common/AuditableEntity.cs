namespace TpmPregledi.Domain.Common;

/// <summary>
/// Bazna klasa za sve poslovne entitete. Vremena su UTC (<c>timestamptz</c>), autori su
/// snapshot UserId-a (ili <c>"system"</c>) <b>bez FK</b> ka AspNetUsers — da zapis preživi sve.
/// Popunjava ih <c>AuditSaveChangesInterceptor</c> pri <c>SaveChanges</c>, nikad handler.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>UTC trenutak kreiranja zapisa.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UserId (ili "system") ko je kreirao zapis.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>UTC trenutak poslednje izmene (null dok se zapis ne izmeni).</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>UserId (ili "system") ko je poslednji izmenio zapis (null dok se ne izmeni).</summary>
    public string? UpdatedBy { get; set; }
}
