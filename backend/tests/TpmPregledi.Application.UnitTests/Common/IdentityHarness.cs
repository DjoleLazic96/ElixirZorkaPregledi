using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Identity;
using TpmPregledi.Infrastructure.Persistence;

namespace TpmPregledi.Application.UnitTests.Common;

/// <summary>
/// Pravi <see cref="UserManager{TUser}"/> i <see cref="RoleManager{TRole}"/> nad InMemory bazom sa
/// ISTOM politikom lozinke, lockout-om i srpskim porukama kao produkcija — da unit testovi handlera
/// korisnika i prijave vide stvarno Identity ponašanje bez Postgres-a.
/// </summary>
public sealed class IdentityHarness : IAsyncDisposable
{
    private readonly ServiceProvider _sp;

    public AppDbContext Db { get; }
    public UserManager<ApplicationUser> Users { get; }
    public RoleManager<ApplicationRole> Roles { get; }

    private IdentityHarness(ServiceProvider sp)
    {
        _sp   = sp;
        Db    = sp.GetRequiredService<AppDbContext>();
        Users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        Roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
    }

    public static IdentityHarness New()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider(); // token provideri (reset lozinke)
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength         = 8;
                o.Password.RequireDigit           = true;
                o.Password.RequireUppercase       = true;
                o.Password.RequireLowercase       = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(10);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<SrpskiIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        return new IdentityHarness(services.BuildServiceProvider());
    }

    /// <summary>Kreira korisnika (i rolu ako treba). Lozinka mora da prođe politiku.</summary>
    public async Task<ApplicationUser> SeedUserAsync(
        string userName, string password, string? rola = null,
        bool active = true, DateTimeOffset? lockoutEnd = null)
    {
        var user = new ApplicationUser
        {
            UserName       = userName,
            Email          = $"{userName}@test.local",
            EmailConfirmed = true,
            FullName       = userName,
            IsActive       = active,
            LockoutEnabled = true,
            LockoutEnd     = lockoutEnd,
        };

        var result = await Users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (rola is not null)
        {
            if (!await Roles.RoleExistsAsync(rola))
                await Roles.CreateAsync(new ApplicationRole { Name = rola });
            await Users.AddToRoleAsync(user, rola);
        }

        return user;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _sp.DisposeAsync();
    }
}
