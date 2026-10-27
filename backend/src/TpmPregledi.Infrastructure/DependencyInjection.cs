using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Identity;
using TpmPregledi.Infrastructure.Persistence;
using TpmPregledi.Infrastructure.Persistence.Interceptors;
using TpmPregledi.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TpmPregledi.Infrastructure;

/// <summary>
/// Composition root Infrastructure sloja: EF Core (Postgres, snake_case, audit interceptor),
/// Identity, JWT izdavanje, trenutni korisnik, sat, verzija, stanje servera, Data Protection.
/// JWT Bearer <i>validacija</i> i politike autorizacije su u <c>Program.cs</c> (zavise od okruženja).
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registruje sve servise Infrastructure sloja.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // ─── Options (fail-fast pri startu) ─────────────────────────────────
        // Dužinu JWT tajne proverava Program.cs pre Build-a; ovde DataAnnotations za ostalo.
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AdminOptions>()
            .Bind(config.GetSection(AdminOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ─── Sat, verzija, audit ────────────────────────────────────────────
        services.AddSingleton<IDateTimeProvider, Time.SystemDateTimeProvider>();
        services.AddSingleton<IAppVersion, Version.AppVersion>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditDogadjaji, Audit.AuditDogadjaji>();

        // ─── DbContext ──────────────────────────────────────────────────────
        // Model deo (provider + snake_case) deli se sa design-time factory-jem kroz
        // KonfigurisiPostgres; interceptor ne utiče na model, pa je samo ovde.
        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.KonfigurisiPostgres(config.GetConnectionString("Default") ?? string.Empty)
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ─── Identity ───────────────────────────────────────────────────────
        // Politika lozinke je standard (blueprint 07): min 8, cifra, veliko, malo; bez obaveznog
        // specijalnog znaka. NE slabiti. Lockout 5 pokušaja → 10 min.
        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength         = 8;
                o.Password.RequireDigit           = true;
                o.Password.RequireUppercase       = true;
                o.Password.RequireLowercase       = true;
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail         = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(10);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<SrpskiIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        // ─── JWT izdavanje/rotacija ─────────────────────────────────────────
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // ─── Trenutni korisnik ──────────────────────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // ─── Stanje servera ─────────────────────────────────────────────────
        // IHardwareInfo po platformi (Windows/Linux/Null) kroz factory; singleton jer je stateless.
        services.AddSingleton<ServerStanje.IHardwareInfo>(_ => ServerStanje.HardwareInfoFactory.Napravi());
        services.AddScoped<IServerStanjeService, ServerStanje.ServerStanjeService>();

        // ─── Data Protection ────────────────────────────────────────────────
        // Ključevi na disku (bind mount, u backup-u) i fiksno ime aplikacije — i kad danas ništa ne
        // šifrujemo: Identity token provideri (reset lozinke) zavise od DP-a, a bez trajnih
        // ključeva svaki restart kontejnera poništava sve što je ikad bilo zaštićeno.
        var keysPath = config["DataProtection:KeysPath"] ?? "data-protection-keys";
        services.AddDataProtection()
            .SetApplicationName("tpm-pregledi")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        return services;
    }
}
