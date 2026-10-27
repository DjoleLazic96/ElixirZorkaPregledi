using TpmPregledi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace TpmPregledi.Infrastructure.Persistence.Seed;

/// <summary>
/// Inicijalni podaci posle migracije, idempotentno: (1) role iz <see cref="Roles.All"/>,
/// (2) inicijalni admin ako ne postoji nijedan admin. Šifarnici ovde nema — kad ih aplikacija
/// dobije, seed ide iz C# klase <c>Application/Common/Seed/&lt;Sta&gt;SeedSource.cs</c> samo u praznu tabelu.
/// </summary>
public static class SeedData
{
    /// <summary>Pokreće se iz <c>Program.cs</c> (<c>MigrateAndSeedAsync</c>) pri svakom startu.</summary>
    public static async Task RunAsync(
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        AdminOptions admin,
        ILogger logger,
        CancellationToken ct = default)
    {
        await SeedRolesAsync(roles, logger);
        await SeedAdminAsync(users, admin, logger);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roles, ILogger logger)
    {
        foreach (var role in Roles.All)
        {
            if (await roles.RoleExistsAsync(role)) continue;

            await roles.CreateAsync(new ApplicationRole { Name = role });
            logger.LogInformation("Seed: kreirana rola {Rola}.", role);
        }
    }

    /// <summary>
    /// Kreira admina samo ako ne postoji <b>nijedan</b> korisnik u roli Admin. Odbija praznu ili
    /// default lozinku (<c>Admin123!</c>) sa <c>LogCritical</c> — aplikacija radi, ali se niko ne
    /// može prijaviti, što je namerno vidljivo: bolje nego produkcija sa lozinkom iz git istorije.
    /// </summary>
    private static async Task SeedAdminAsync(UserManager<ApplicationUser> users, AdminOptions admin, ILogger logger)
    {
        var postojeciAdmini = await users.GetUsersInRoleAsync(Roles.Admin);
        if (postojeciAdmini.Count > 0)
            return;

        if (string.IsNullOrWhiteSpace(admin.InitialPassword) || admin.InitialPassword == "Admin123!")
        {
            logger.LogCritical(
                "Admin nalog NIJE kreiran: Admin:InitialPassword je prazan ili default. " +
                "Postavite Admin__InitialPassword pre pokretanja.");
            return;
        }

        var user = new ApplicationUser
        {
            UserName       = admin.InitialUsername,
            Email          = admin.InitialEmail,
            EmailConfirmed = true,
            FullName       = "Inicijalni administrator",
            IsActive       = true,
        };

        var result = await users.CreateAsync(user, admin.InitialPassword);
        if (!result.Succeeded)
        {
            logger.LogError("Seed admina nije uspeo: {Greske}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRoleAsync(user, Roles.Admin);
        logger.LogWarning("Seed: kreiran inicijalni admin {KorisnickoIme}. Promenite lozinku pri prvoj prijavi.",
            admin.InitialUsername);
    }
}
