using System.Net;
using System.Text;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;

namespace TpmPregledi.Api.IntegrationTests.Auth;

/// <summary>
/// Matrica prava za SVAKU zaštićenu rutu (blueprint 07 §4, 12 §3 #18): anoniman → 401, Korisnik na
/// Admin ruti → 403, Korisnik na dozvoljenoj ruti i Admin svuda → ni 401 ni 403 (400/404/2xx je OK —
/// ovde se testira samo autorizacija, ne telo). Nova ruta = novi red u <see cref="Rute"/>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AuthorizationMatrixTests(CustomWebAppFactory factory)
{
    private const string Admin = "RequireAdmin";
    private const string Korisnik = "RequireKorisnik";
    private const string BiloKo = "Authenticated";

    private static readonly Guid NemaId = Guid.NewGuid();

    /// <summary>(metod, ruta, politika). Anonimne rute (/health*, /version, /auth/login, /auth/refresh) nisu ovde.</summary>
    public static readonly (string Metod, string Ruta, string Politika)[] Rute =
    [
        ("GET",  "/api/v1/auth/me",                            BiloKo),
        ("POST", "/api/v1/auth/logout",                        BiloKo),
        ("POST", "/api/v1/auth/change-password",               BiloKo),

        ("GET",  "/api/v1/korisnici",                          Admin),
        ("POST", "/api/v1/korisnici",                          Admin),
        ("PUT",  $"/api/v1/korisnici/{NemaId}",                Admin),
        ("POST", $"/api/v1/korisnici/{NemaId}/deaktiviraj",    Admin),
        ("POST", $"/api/v1/korisnici/{NemaId}/aktiviraj",      Admin),
        ("POST", $"/api/v1/korisnici/{NemaId}/reset-lozinka",  Admin),

        ("GET",  "/api/v1/audit",                              Admin),
        ("GET",  "/api/v1/audit/entiteti",                     Admin),
        ("GET",  "/api/v1/stanje-servera",                     Admin),

        ("GET",  "/api/v1/kategorije",                         Korisnik),
        ("GET",  "/api/v1/kategorije/999999",                  Korisnik),
        ("POST", "/api/v1/kategorije",                         Admin),
        ("PUT",  "/api/v1/kategorije/999999",                  Admin),
        ("POST", "/api/v1/kategorije/999999/deaktiviraj",      Admin),
        ("POST", "/api/v1/kategorije/999999/aktiviraj",        Admin),

        ("GET",  "/api/v1/_diag/admin-only",                   Admin),
        ("GET",  "/api/v1/_diag/korisnik",                     Korisnik),
    ];

    public static IEnumerable<object[]> SveRute => Rute.Select(r => new object[] { r.Metod, r.Ruta });
    public static IEnumerable<object[]> AdminRute => Rute.Where(r => r.Politika == Admin).Select(r => new object[] { r.Metod, r.Ruta });
    public static IEnumerable<object[]> KorisnikRute => Rute.Where(r => r.Politika != Admin).Select(r => new object[] { r.Metod, r.Ruta });

    private static HttpRequestMessage Zahtev(string metod, string ruta)
    {
        var req = new HttpRequestMessage(new HttpMethod(metod), ruta);
        if (metod is "POST" or "PUT")
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");   // prazno telo → najviše 400
        return req;
    }

    [Theory]
    [MemberData(nameof(SveRute))]
    public async Task Anonimni_dobija_401(string metod, string ruta)
    {
        var res = await factory.CreateClient().SendAsync(Zahtev(metod, ruta));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AdminRute))]
    public async Task Korisnik_na_admin_ruti_dobija_403(string metod, string ruta)
    {
        var korisnik = await factory.KorisnikClientAsync();
        var res = await korisnik.SendAsync(Zahtev(metod, ruta));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(KorisnikRute))]
    public async Task Korisnik_na_dozvoljenoj_ruti_ne_dobija_401_ni_403(string metod, string ruta)
    {
        var korisnik = await factory.KorisnikClientAsync();
        var res = await korisnik.SendAsync(Zahtev(metod, ruta));
        res.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(SveRute))]
    public async Task Admin_ne_dobija_401_ni_403(string metod, string ruta)
    {
        var admin = await factory.AdminClientAsync();
        var res = await admin.SendAsync(Zahtev(metod, ruta));
        res.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Istekao_ili_izmisljen_token_vraca_401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "nije.pravi.jwt");
        (await client.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
