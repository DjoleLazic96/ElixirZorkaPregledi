using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TpmPregledi.Application.Features.Auth.Dtos;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace TpmPregledi.Api.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> nad pravim Postgres-om (Testcontainers
/// <c>postgres:16-alpine</c>). Migracije, seed (role + admin) i audit START prolaze kroz
/// <c>Program</c> pri startu — isto kao u produkciji. Jedan kontejner po test kolekciji.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rate limiter i IP:</b> TestServer ne postavlja <c>RemoteIpAddress</c> (null), pa bi svi
/// klijenti delili jednu particiju („unknown“) i <c>auth-login</c> limit od 10/min bi oborio
/// testove. Zato svaki <see cref="HttpClient"/> dobija jedinstven <c>X-Forwarded-For</c>:
/// ForwardedHeaders middleware prihvata heder kad je remote IP null, pa je svaki klijent „svoj IP“
/// — i audit <c>ip_address</c> dobija vrednost.
/// </para>
/// <para>
/// Admin lozinka je <c>TestAdmin2026!</c> (ne <c>Admin123!</c> koji seed odbija).
/// </para>
/// </remarks>
public sealed class CustomWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "TestAdmin2026!";
    public const string KorisnikUserName = "korisnik-test";
    public const string KorisnikPassword = "Korisnik2026!";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("tpm-pregledi")
        .WithUsername("tpm-pregledi")
        .WithPassword("tpm-pregledi-test")
        .Build();

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "tpm-pregledi-it-" + Guid.NewGuid().ToString("N"));
    private int _ipBrojac;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Development: RequireHttpsMetadata=false, _diag rute mapirane, CORS fail-fast preskočen.
        builder.UseEnvironment("Development");

        builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"]  = _db.GetConnectionString(),
            ["Jwt:Secret"]                 = "TEST_ONLY_tpm-pregledi_secret_key_min_32_bytes_XXXXX",
            ["Cors:Origins:0"]             = "http://localhost:5173",
            ["Admin:InitialUsername"]      = AdminUserName,
            ["Admin:InitialPassword"]      = AdminPassword,
            // Ništa na disku pored bin foldera: DP ključevi, log fajl i backup folder idu u temp.
            ["DataProtection:KeysPath"]    = Path.Combine(_tempRoot, "dp-keys"),
            ["Serilog:WriteTo:1:Args:path"] = Path.Combine(_tempRoot, "logs", "log-.txt"),
            ["Backup:Path"]                = Path.Combine(_tempRoot, "backups-ne-postoji"),
        }));

        return base.CreateHost(builder);
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        var n = Interlocked.Increment(ref _ipBrojac);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}");
    }

    // ─── Klijenti ──────────────────────────────────────────────────────────

    /// <summary>Klijent koji čuva cookie-je između zahteva (login → refresh → logout tok).</summary>
    public HttpClient CreateClientWithCookies() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    /// <summary>Prijavljuje korisnika i vraća klijent sa <c>Authorization: Bearer</c> hederom (i cookie-jem).</summary>
    public async Task<HttpClient> AuthenticatedClientAsync(string userName, string password)
    {
        var client = CreateClientWithCookies();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<LoginResponse>()
                   ?? throw new InvalidOperationException("Login nije vratio LoginResponse.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        return client;
    }

    /// <summary>Seed-ovani admin.</summary>
    public Task<HttpClient> AdminClientAsync() => AuthenticatedClientAsync(AdminUserName, AdminPassword);

    /// <summary>Korisnik sa rolom <c>Korisnik</c> (seed-uje ga pri prvom pozivu).</summary>
    public async Task<HttpClient> KorisnikClientAsync()
    {
        await SeedUserAsync(KorisnikUserName, KorisnikPassword, Roles.Korisnik);
        return await AuthenticatedClientAsync(KorisnikUserName, KorisnikPassword);
    }

    // ─── Podaci ────────────────────────────────────────────────────────────

    /// <summary>Kreira korisnika sa rolom kroz <see cref="UserManager{TUser}"/>; idempotentno.</summary>
    public async Task<ApplicationUser> SeedUserAsync(string userName, string password, string role)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roles.RoleExistsAsync(role))
            await roles.CreateAsync(new ApplicationRole { Name = role });

        var user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName       = userName,
                Email          = $"{userName}@test.local",
                EmailConfirmed = true,
                FullName       = userName,
                IsActive       = true,
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("SeedUserAsync: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await users.IsInRoleAsync(user, role))
            await users.AddToRoleAsync(user, role);

        return user;
    }

    /// <summary>Direktan rad sa bazom u sopstvenom DI scope-u (provere redova audit loga, tokena, šeme).</summary>
    public async Task<T> SaBazomAsync<T>(Func<AppDbContext, Task<T>> rad)
    {
        await using var scope = Services.CreateAsyncScope();
        return await rad(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Novi DI scope (za servise kao <c>IJwtTokenService</c>); pozivalac ga dispose-uje.</summary>
    public AsyncServiceScope NoviScope() => Services.CreateAsyncScope();

    // ─── Životni ciklus ────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort čišćenje temp-a */ }
    }
}

/// <summary>Sve integracione klase dele JEDAN Postgres kontejner (dizanje po klasi bi bilo presporo).</summary>
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<CustomWebAppFactory>;
