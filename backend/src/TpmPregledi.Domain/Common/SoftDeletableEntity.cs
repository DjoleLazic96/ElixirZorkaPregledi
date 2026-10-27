namespace TpmPregledi.Domain.Common;

/// <summary>
/// Šifarnici i matični podaci se ne brišu fizički — samo <see cref="Aktivan"/> = false, jer ih
/// istorijski zapisi referenciraju. Jedinstvenost šifre važi samo među aktivnima (filtrirani
/// indeks <c>WHERE aktivan = true</c>), pa deaktiviran zapis sme da deli šifru sa novim.
/// </summary>
public abstract class SoftDeletableEntity : AuditableEntity
{
    /// <summary>Soft-delete flag; neaktivan zapis ostaje u bazi, ali se ne nudi u izboru.</summary>
    public bool Aktivan { get; set; } = true;
}
