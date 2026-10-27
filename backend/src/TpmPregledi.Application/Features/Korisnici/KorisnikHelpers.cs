using TpmPregledi.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Korisnici;

/// <summary>Zajednički upiti ka Identity-ju koje dele handleri korisnika.</summary>
internal static class KorisnikHelpers
{
    /// <summary>Jedina rola korisnika; prazan string ako je nalog ostao bez role (ne bi smelo da se desi).</summary>
    public static async Task<string> RolaAsync(UserManager<ApplicationUser> users, ApplicationUser user) =>
        (await users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// ID-jevi svih aktivnih admina — „read“ deo read-then-mutate guarda za poslednjeg admina.
    /// Nije serijalizovan sa upisom (poznato TOCTOU ograničenje, vidi <see cref="KorisnikGuards"/>).
    /// </summary>
    public static async Task<IReadOnlyCollection<string>> AktivniAdminIdsAsync(UserManager<ApplicationUser> users)
    {
        var admini = await users.GetUsersInRoleAsync(Roles.Admin);
        return admini.Where(u => u.IsActive).Select(u => u.Id).ToList();
    }
}
