using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using TpmPregledi.Api.Endpoints;
using TpmPregledi.Api.Middleware;
using TpmPregledi.Application;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure;
using TpmPregledi.Infrastructure.Identity;
using TpmPregledi.Infrastructure.Persistence;
using TpmPregledi.Infrastructure.Persistence.Seed;

// =============================================================================
// TPM pregledi — API ulazna tačka (blueprint 04 §1). Redosled se ne menja.
// =============================================================================

const string NazivAplikacije = "TPM pregledi";

// Bootstrap logger: da se vidi i greška pri startu, pre nego što host pročita Serilog sekciju.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Logging ──────────────────────────────────────────────────────────
    // Konfiguracija je u appsettings.json ("Serilog"): konzola + dnevni fajl, 30 dana.
    // {TraceId} u outputTemplate puni sam Serilog iz Activity.Current — ništa se ne push-uje.
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ─── DI: Application + Infrastructure ─────────────────────────────────
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    // ─── JSON ─────────────────────────────────────────────────────────────
    // Enumi kao camelCase stringovi — frontend radi sa literalima, API ostaje stabilan ako se
    // enum vrednosti preurede u kodu.
    builder.Services.ConfigureHttpJsonOptions(opts =>
        opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

    // ─── JWT: fail-fast + validacija ──────────────────────────────────────
    // Options pattern (ValidateOnStart) je u AddInfrastructure; ovde dodatno pre Build-a: ako bi
    // tajna pala na placeholder iz git istorije, bilo ko sa pristupom repou kuje admin JWT.
    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
              ?? throw new InvalidOperationException("Nedostaje 'Jwt' sekcija u konfiguraciji.");

    if (string.IsNullOrWhiteSpace(jwt.Secret)
        || jwt.Secret.Contains("REPLACE_ME", StringComparison.OrdinalIgnoreCase)
        || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
    {
        throw new InvalidOperationException(
            "Jwt:Secret nije konfigurisan ili je prekratak. Postavite env varijablu Jwt__Secret na " +
            "slučajan string od najmanje 32 bajta (npr. `openssl rand -base64 64`).");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            // SaveToken=false: niko ne čita token kroz GetTokenAsync — manje izloženog materijala.
            opts.SaveToken            = false;
            opts.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                // Samo HS256 — ne koristimo RSA, pa „algorithm confusion“ nije primenljiv, ali
                // eksplicitna lista je defense-in-depth.
                ValidAlgorithms          = [SecurityAlgorithms.HmacSha256],
                RequireSignedTokens      = true,
                RequireExpirationTime    = true,
                ValidIssuer              = jwt.Issuer,
                ValidAudience            = jwt.Audience,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                ClockSkew                = TimeSpan.FromSeconds(30),
            };
        });

    // ─── Autorizacija ─────────────────────────────────────────────────────
    // Politika nosi ime NAJSLABIJE role koju pušta; Admin je implicitno u svakoj. Plain
    // .RequireAuthorization() (bilo ko ulogovan) samo za /auth/me, /auth/logout, /auth/change-password.
    builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy("RequireAdmin",    p => p.RequireRole(Roles.Admin));
        opts.AddPolicy("RequireKorisnik", p => p.RequireRole(Roles.Admin, Roles.Korisnik));
    });

    // ─── CORS ─────────────────────────────────────────────────────────────
    // Frontend i API su isti origin u produkciji, ali politika je eksplicitna i Cors:Origins je
    // obavezan (fail-fast) — da pogrešna konfiguracija bude vidljiva pri startu, ne u browseru.
    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (builder.Environment.IsProduction() && corsOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "Cors:Origins MORA biti postavljen u produkciji. Postavite Cors__Origins__0 na pun HTTPS URL frontenda.");
    }

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(corsOrigins)
        .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
        .AllowAnyHeader()
        .WithExposedHeaders("Content-Disposition")   // ime fajla pri izvozu
        .AllowCredentials()));

    // ─── Forwarded headers ────────────────────────────────────────────────
    // Zahtev prolazi kroz DVA proxy-ja: Traefik (host) → nginx (frontend kontejner) → API.
    // ForwardLimit = 2 (MORA ostati 2, nikad null) da se oba hop-a razmotaju i RemoteIpAddress
    // bude pravi klijent (audit, rate limiter), a X-Forwarded-Proto da Request.IsHttps bude true
    // (refresh cookie Secure). Verujemo samo privatnim mrežama u kojima ti proxy-ji žive.
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit     = 2;
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
    });

    // ─── Rate limiting ────────────────────────────────────────────────────
    // Po IP-u: auth-login 10/min, auth-refresh 30/min, globalno 200/min → 429. Health i version
    // su izuzeti (DisableRateLimiting). In-memory: resetuje se pri restartu (prihvaćen rizik).
    builder.Services.AddRateLimiter(opts =>
    {
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        opts.AddPolicy("auth-login",   ctx => PoIpu(ctx, permitLimit: 10));
        opts.AddPolicy("auth-refresh", ctx => PoIpu(ctx, permitLimit: 30));
        opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => PoIpu(ctx, permitLimit: 200));

        static RateLimitPartition<string> PoIpu(HttpContext ctx, int permitLimit) =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window      = TimeSpan.FromMinutes(1),
                    QueueLimit  = 0,
                });
    });

    // ─── OpenAPI (samo Development) ───────────────────────────────────────
    builder.Services.AddOpenApi("v1", o =>
    {
        o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title       = $"{NazivAplikacije} — API";
            doc.Info.Version     = "v1";
            doc.Info.Description = "Backend aplikacije TPM pregledi: autentifikacija, korisnici, audit log, stanje servera i primer šifarnika (kategorije).";
            return Task.CompletedTask;
        });
    });

    var app = builder.Build();

    // ─── Migracije + seed + audit START ──────────────────────────────────
    await MigrateAndSeedAsync(app);

    // ─── Pipeline (redosled iz blueprint-a 04 §1) ─────────────────────────
    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"]        = "DENY";
        ctx.Response.Headers["Referrer-Policy"]        = "no-referrer";
        await next();
    });

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseCors();

    // PRE autentifikacije: neautentifikovan flood udara u limiter, ne u 401. Politike po
    // endpointu rade jer je routing implicitno na početku pipeline-a.
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();                          // /openapi/v1.json
        app.MapScalarApiReference("/swagger");     // UI za ručno testiranje
        // _diag: trivijalni endpointi po jednoj politici autorizacije. Služe integracionim
        // testovima kao dokaz da politike propuštaju tačno one role koje treba, nezavisno od
        // poslovnih ruta. Nisu deo API ugovora i ne postoje van Development-a.
        app.MapDiagEndpoints();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthEndpoints();
    app.MapVersionEndpoints();
    app.MapAuthEndpoints();
    app.MapKorisniciEndpoints();
    app.MapKategorijeEndpoints();                 // PRIMER feature-a — kopiraj ili obriši
    app.MapAuditEndpoints();
    app.MapServerStanjeEndpoints();

    // Jedan Information red pri startu (blueprint 09 §2) — IT iz loga vidi šta je deploy-ovano.
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var v = app.Services.GetRequiredService<IAppVersion>();
        Log.Information("Aplikacija {Naziv} v{Verzija} ({GitSha}) pokrenuta, okruženje {Okruzenje}",
            NazivAplikacije, v.Verzija, v.GitSha, app.Environment.EnvironmentName);
    });

    await app.RunAsync();
    return 0;
}
catch (HostAbortedException)
{
    // EF alati (dotnet ef) namerno prekidaju host posle Build-a — nije greška, propusti dalje.
    throw;
}
catch (Exception ex)
{
    // Exit code MORA biti 1: sa nulom Docker (restart: unless-stopped) i monitoring vide uredan
    // završetak, kontejner se ne restartuje i aplikacija tiho ostane mrtva.
    Log.Fatal(ex, "Aplikacija nije uspela da se pokrene");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

// =============================================================================
// Migracije pri startu (jedini put primene šeme), seed (role + inicijalni admin) i audit START
// red — istorija deploy-a u samoj bazi. Greška ovde → Fatal + exit 1 (catch iznad).
// =============================================================================
static async Task MigrateAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var sp     = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    var db    = sp.GetRequiredService<AppDbContext>();
    var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
    var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
    var admin = sp.GetRequiredService<IOptions<AdminOptions>>().Value;

    logger.LogInformation("Primenjujem EF migracije…");
    await db.Database.MigrateAsync();

    logger.LogInformation("Seed (role + inicijalni admin)…");
    await SeedData.RunAsync(users, roles, admin, logger);

    // Best-effort (IAuditDogadjaji guta greške) — audit nikad ne obara start.
    var version = sp.GetRequiredService<IAppVersion>();
    await sp.GetRequiredService<IAuditDogadjaji>()
        .StartAsync(version.Verzija, version.GitSha, app.Environment.EnvironmentName);

    logger.LogInformation("Migracije i seed završeni.");
}

/// <summary>Parcijalna klasa da <c>WebApplicationFactory&lt;Program&gt;</c> u integracionim testovima vidi ulaznu tačku.</summary>
public partial class Program;
