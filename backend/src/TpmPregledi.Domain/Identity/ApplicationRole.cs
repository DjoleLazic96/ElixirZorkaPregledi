using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Domain.Identity;

/// <summary>
/// Rola u sistemu. Spisak validnih imena: <see cref="Roles"/>.
/// </summary>
/// <remarks>
/// Ne dodaje polja preko nasleđenog <see cref="IdentityRole{TKey}"/>. Klasa postoji radi
/// konzistentnog para (ApplicationUser + ApplicationRole) i mesta za buduća proširenja.
/// </remarks>
public class ApplicationRole : IdentityRole<string>
{
    /// <summary>
    /// Pošto je TKey = string, Identity ne generiše PK sam — generišemo GUID u konstruktoru
    /// da nijedan poziv (seed, test) ne mora da misli na to.
    /// </summary>
    public ApplicationRole()
    {
        Id = Guid.NewGuid().ToString();
    }
}
