using TpmPregledi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TpmPregledi.Infrastructure.Persistence.Seed;

/// <summary>
/// Inicijalni podaci posle migracije, idempotentno: (1) role iz <see cref="Roles.All"/>,
/// (2) inicijalni admin ako ne postoji nijedan admin, (3) prelaz sa role skeleta <c>Korisnik</c> na
/// <c>Inzenjer</c> i podrazumevana prava za naloge koji nemaju nijedno. Šifarnici ovde nema — kad ih aplikacija
/// dobije, seed ide iz C# klase <c>Application/Common/Seed/&lt;Sta&gt;SeedSource.cs</c> samo u praznu tabelu.
/// </summary>
public static class SeedData
{
    /// <summary>Pokreće se iz <c>Program.cs</c> (<c>MigrateAndSeedAsync</c>) pri svakom startu.</summary>
    public static async Task RunAsync(
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        AppDbContext db,
        AdminOptions admin,
        ILogger logger,
        CancellationToken ct = default)
    {
        await SeedRolesAsync(roles, logger);
        await SeedAdminAsync(users, admin, logger);
        await PrelazNaPravaAsync(users, roles, db, logger, ct);
    }

    /// <summary>Rola skeleta koja više ne postoji; nalozi sa njom postaju Inženjeri.</summary>
    private const string StaraRolaKorisnik = "Korisnik";

    /// <summary>
    /// Nalog bez ijednog prava dobija podrazumevana prava svoje role. Tako i admin iz seed-a i nalozi
    /// napravljeni pre faze 01 imaju prava, a Admin koji je nekom namerno skinuo sva prava…
    /// ostaje pokriven: takav nalog nema smisla, pa vraćanje podrazumevanih na startu nije šteta.
    /// </summary>
    private static async Task PrelazNaPravaAsync(
        UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles, AppDbContext db,
        ILogger logger, CancellationToken ct)
    {
        if (await roles.FindByNameAsync(StaraRolaKorisnik) is { } stara)
        {
            foreach (var u in await users.GetUsersInRoleAsync(StaraRolaKorisnik))
            {
                await users.AddToRoleAsync(u, Roles.Inzenjer);
                await users.RemoveFromRoleAsync(u, StaraRolaKorisnik);
            }
            await roles.DeleteAsync(stara);
            logger.LogInformation("Seed: rola {Stara} zamenjena rolom {Nova}.", StaraRolaKorisnik, Roles.Inzenjer);
        }

        var saPravima = await db.KorisnikPrava.Select(p => p.KorisnikId).Distinct().ToListAsync(ct);
        var bezPrava = await db.Users.Where(u => !saPravima.Contains(u.Id)).ToListAsync(ct);
        foreach (var u in bezPrava)
        {
            var rola = (await users.GetRolesAsync(u)).FirstOrDefault();
            if (rola is null) continue;
            foreach (var p in Prava.Podrazumevana(rola))
                db.KorisnikPrava.Add(new KorisnikPravo { KorisnikId = u.Id, Pravo = p });
        }
        if (bezPrava.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seed: podrazumevana prava za {Broj} naloga.", bezPrava.Count);
        }
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
