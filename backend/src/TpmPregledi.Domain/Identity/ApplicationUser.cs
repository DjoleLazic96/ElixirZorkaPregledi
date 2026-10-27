using TpmPregledi.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Domain.Identity;

/// <summary>
/// Korisnik aplikacije. Proširuje ASP.NET <see cref="IdentityUser{TKey}"/> poljima koja
/// Identity nema: puno ime, status aktivnosti, vreme poslednje prijave.
/// </summary>
/// <remarks>
/// <para>
/// PK je <c>string</c> (GUID) — Identity default. Sve referentne tabele (<c>audit_log.user_id</c>,
/// <c>refresh_token.user_id</c>) koriste isti string ključ.
/// </para>
/// <para>
/// Generička forma <see cref="IdentityUser{TKey}"/> dolazi iz <c>Microsoft.Extensions.Identity.Stores</c>
/// da Domain sloj ne zavisi od EF Core-a ni ASP.NET runtime-a.
/// </para>
/// <para>
/// Korisnici se <b>nikad ne brišu</b> fizički (audit log i <c>created_by</c> moraju da prežive) —
/// samo <see cref="IsActive"/> = false.
/// </para>
/// </remarks>
[Auditable]
public class ApplicationUser : IdentityUser<string>
{
    /// <summary>GUID PK se generiše ovde jer generički <c>IdentityUser</c> to ne radi sam.</summary>
    public ApplicationUser()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>Puno ime i prezime korisnika (prikazuje se u UI).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Soft-delete flag: deaktiviran korisnik ne može da se prijavi, ali ostaje u bazi.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC trenutak kreiranja naloga.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC trenutak poslednje uspešne prijave (null ako se nikad nije prijavio).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Refresh tokeni koje korisnik trenutno ima (po uređaju/sesiji).</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
