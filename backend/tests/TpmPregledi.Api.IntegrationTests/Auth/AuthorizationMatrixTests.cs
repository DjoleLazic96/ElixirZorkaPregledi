using TpmPregledi.Domain.Identity;
using System.Net;
using System.Text;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;

namespace TpmPregledi.Api.IntegrationTests.Auth;

/// <summary>
/// Matrica prava za SVAKU zaštićenu rutu (blueprint 07 §4, 12 §3 #18): anoniman → 401, Inženjer (podrazumevana
/// prava) na Admin ruti → 403, Inženjer na dozvoljenoj ruti i Admin svuda → ni 401 ni 403 (400/404/2xx je OK —
/// ovde se testira samo autorizacija, ne telo). Nova ruta = novi red u <see cref="Rute"/>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AuthorizationMatrixTests(CustomWebAppFactory factory)
{
    private const string Admin = "RequireAdmin";
    private const string Korisnik = "RequireKorisnik";
    private const string BiloKo = "Authenticated";
    // Prava koja podrazumevano ima Supervizor (raspored, odsustva), a nemaju ni Inženjer ni Admin.
    private const string Supervizor = "Supervizor";
    // Pravo popunjavanja: ima ga samo Inženjer (zahtevi za odsustvo i prebacivanje).
    private const string Inzenjer = "Inzenjer";

    private static readonly Guid NemaId = Guid.NewGuid();

    /// <summary>(metod, ruta, politika). Anonimne rute (/health*, /version, /auth/login, /auth/refresh) nisu ovde.</summary>
    public static readonly (string Metod, string Ruta, string Politika)[] Rute =
    [
        ("GET",  "/api/v1/auth/me",                            BiloKo),
        ("POST", "/api/v1/auth/logout",                        BiloKo),
        ("POST", "/api/v1/auth/change-password",               BiloKo),

        ("GET",  "/api/v1/sekcije",                            Korisnik),
        ("POST", "/api/v1/sekcije",                            Admin),
        ("PUT",  "/api/v1/sekcije/999999",                     Admin),
        ("PUT",  "/api/v1/sekcije/redosled",                   Admin),
        ("GET",  "/api/v1/podesavanja",                        Admin),
        ("PUT",  "/api/v1/podesavanja",                        Admin),
        ("PUT",  $"/api/v1/korisnici/{NemaId}/prava",          Admin),

        ("GET",  "/api/v1/korisnici",                          Admin),
        ("POST", "/api/v1/korisnici",                          Admin),
        ("PUT",  $"/api/v1/korisnici/{NemaId}",                Admin),
        ("POST", $"/api/v1/korisnici/{NemaId}/deaktiviraj",    Admin),
        ("POST", $"/api/v1/korisnici/{NemaId}/aktiviraj",      Admin),
        ("POST", $"/api/v1/korisnici/{NemaId}/reset-lozinka",  Admin),

        ("GET",  "/api/v1/audit",                              Admin),
        ("GET",  "/api/v1/audit/entiteti",                     Admin),
        ("GET",  "/api/v1/stanje-servera",                     Admin),

        // Izmene registra i kataloga traže pravo sabloni.nacrt (Inženjer ga podrazumevano nema → 403).
        ("GET",  "/api/v1/uredjaji",                           Korisnik),
        ("GET",  "/api/v1/uredjaji/999999",                    Korisnik),
        ("POST", "/api/v1/uredjaji",                           Admin),
        ("PUT",  "/api/v1/uredjaji/999999",                    Admin),
        ("POST", "/api/v1/uredjaji/999999/van-upotrebe",       Admin),
        ("POST", "/api/v1/uredjaji/999999/u-upotrebi",         Admin),
        ("GET",  "/api/v1/aktivnosti",                         Korisnik),
        ("POST", "/api/v1/aktivnosti",                         Admin),
        ("PUT",  "/api/v1/aktivnosti/999999",                  Admin),
        ("GET",  "/api/v1/dodatna-polja",                      Korisnik),
        ("POST", "/api/v1/dodatna-polja",                      Admin),
        ("GET",  "/api/v1/sabloni",                            Korisnik),
        ("GET",  "/api/v1/sabloni/DP1",                        Korisnik),
        ("GET",  "/api/v1/sabloni/DP1/verzije/1",              Korisnik),
        ("POST", "/api/v1/sabloni/uvoz",                       Admin),

        ("GET",  "/api/v1/raspored?od=2026-11-09&do=2026-11-15", Korisnik),
        ("GET",  "/api/v1/raspored/inzenjeri",                 Korisnik),
        ("POST", "/api/v1/raspored/predlog",                   Supervizor),
        ("POST", "/api/v1/raspored/potvrdi",                   Supervizor),
        ("GET",  "/api/v1/raspored/pz/sledeci",                Supervizor),
        ("POST", "/api/v1/raspored/pz",                        Supervizor),
        ("POST", "/api/v1/raspored/prenesi",                   Supervizor),
        ("POST", "/api/v1/raspored/povuci",                    Supervizor),
        ("POST", "/api/v1/pregledi/999999/otkazi",             Supervizor),
        ("PUT",  "/api/v1/pregledi/999999/tip",                Supervizor),
        ("PUT",  "/api/v1/pregledi/999999/dodela",             Supervizor),
        ("POST", "/api/v1/pregledi/999999/ucesnici",           Supervizor),
        ("GET",  "/api/v1/neradni-dani?godina=2026",           Korisnik),
        ("POST", "/api/v1/neradni-dani",                       Admin),
        ("DELETE", "/api/v1/neradni-dani/999999",              Admin),
        ("GET",  "/api/v1/odsustva",                           Korisnik),
        ("POST", "/api/v1/odsustva",                           Supervizor),
        ("POST", "/api/v1/odsustva/zahtev",                    Inzenjer),
        ("POST", "/api/v1/odsustva/999999/odluka",             Supervizor),
        ("PUT",  "/api/v1/odsustva/999999",                    Supervizor),
        ("POST", "/api/v1/odsustva/999999/otkazi",             Korisnik),
        ("GET",  $"/api/v1/odsustva/{NemaId}/pregledi?od=2026-11-09&do=2026-11-15", Supervizor),
        ("GET",  "/api/v1/zahtevi-prebacivanja",               Korisnik),
        ("POST", "/api/v1/zahtevi-prebacivanja",               Inzenjer),
        ("POST", "/api/v1/zahtevi-prebacivanja/raniji-pocetak", Inzenjer),
        ("POST", "/api/v1/zahtevi-prebacivanja/999999/odluka", Supervizor),

        ("POST", "/api/v1/pregledi/999999/zapocni",            Inzenjer),
        ("GET",  "/api/v1/pregledi/999999/popunjavanje",       Inzenjer),
        ("POST", "/api/v1/sinhronizacija",                     Inzenjer),
        ("GET",  "/api/v1/moj-paket",                          Inzenjer),
        ("POST", "/api/v1/fotografije",                        Inzenjer),
        ("GET",  "/api/v1/fotografije/999999",                 Korisnik),

        ("GET",  "/api/v1/pregledi",                           Korisnik),
        ("GET",  "/api/v1/pregledi/999999",                    Korisnik),
        ("GET",  "/api/v1/pregledi/999999/izvoz?format=pdf",   Korisnik),
        ("GET",  "/api/v1/izvoz/pregledi.xlsx",                Korisnik),
        ("POST", "/api/v1/pregledi/999999/overi",              Supervizor),
        ("POST", "/api/v1/pregledi/999999/vrati-na-doradu",    Supervizor),
        ("POST", "/api/v1/pregledi/999999/zahtev-za-doradu",   Inzenjer),
        ("POST", "/api/v1/pregledi/999999/zahtev-za-doradu/odluka", Supervizor),
        ("POST", "/api/v1/pregledi/999999/ponisti",            Supervizor),
        ("POST", "/api/v1/pregledi/999999/otkljucaj",          Supervizor),
        ("POST", "/api/v1/pregledi/999999/sporan-unos/1/odluka", Supervizor),

        ("GET",  "/api/v1/akcije",                             Korisnik),
        ("GET",  "/api/v1/akcije/999999",                      Korisnik),
        ("POST", "/api/v1/akcije/999999/zatvori",              Supervizor),
        ("POST", "/api/v1/akcije/999999/otkazi",               Supervizor),
        ("PUT",  "/api/v1/akcije/999999/zaduzen",              Supervizor),
        ("GET",  "/api/v1/kvarovi",                            Korisnik),
        ("GET",  "/api/v1/kvarovi/999999",                     Korisnik),
        ("GET",  "/api/v1/izvoz/akcije.xlsx",                  Korisnik),
        ("GET",  "/api/v1/izvoz/kvarovi.xlsx",                 Korisnik),

        // Analitiku podrazumevano imaju sve četiri role.
        ("GET",  "/api/v1/uredjaji/999999/ekran?dana=90",      Korisnik),
        ("GET",  $"/api/v1/uredjaji/qr/{NemaId}",              Korisnik),
        ("GET",  "/api/v1/uredjaji/999999/pregledi.xlsx",      Korisnik),
        ("GET",  "/api/v1/analitika/opsta",                    Korisnik),
        ("GET",  "/api/v1/analitika/ispunjenje",               Korisnik),
        ("GET",  "/api/v1/dashboard",                          Korisnik),
        ("GET",  "/api/v1/pretraga?q=10",                      Korisnik),

        ("GET",  "/api/v1/obavestenja",                        Korisnik),
        ("POST", "/api/v1/obavestenja/procitano",              Korisnik),
        ("POST", "/api/v1/obavestenja/probno",                 Korisnik),
        ("GET",  "/api/v1/push/kljuc",                         Korisnik),
        ("POST", "/api/v1/push/pretplata",                     Korisnik),
        ("DELETE", "/api/v1/push/pretplata",                   Korisnik),
        ("GET",  "/api/v1/podesavanja/primaoci",               Admin),
        ("PUT",  "/api/v1/podesavanja/primaoci",               Admin),

        // Nacrt, uputstvo i odluka o predlogu: sabloni.nacrt (Direktor, Admin; Inženjer → 403). Objava (sabloni.objava, samo Direktor)
        // ne staje ni u jednu kategoriju matrice, pa je ovde nema.
        ("GET",  "/api/v1/sabloni/DP1/verzije",                Korisnik),
        ("GET",  "/api/v1/sabloni/DP1/nacrt",                  Admin),
        ("POST", "/api/v1/sabloni/DP1/nacrt",                  Admin),
        ("DELETE", "/api/v1/sabloni/DP1/nacrt",                Admin),
        ("POST", "/api/v1/sabloni/DP1/nacrt/stavke",           Admin),
        ("PUT",  "/api/v1/sabloni/DP1/nacrt/stavke/999999",    Admin),
        ("DELETE", "/api/v1/sabloni/DP1/nacrt/stavke/999999",  Admin),
        ("POST", "/api/v1/sabloni/DP1/nacrt/stavke/999999/pomeri", Admin),
        ("GET",  "/api/v1/sabloni/istorijat",                  Korisnik),
        ("GET",  "/api/v1/predlozi-izmena",                    Korisnik),
        ("POST", "/api/v1/predlozi-izmena",                    Korisnik),
        ("POST", "/api/v1/predlozi-izmena/999999/odluka",      Admin),
        ("POST", "/api/v1/predlozi-izmena/999999/slazem-se",   Korisnik),
        ("GET",  "/api/v1/aktivnosti/999999/uputstvo",         Korisnik),
        ("PUT",  "/api/v1/aktivnosti/999999/uputstvo",         Admin),
        ("DELETE", "/api/v1/aktivnosti/999999/uputstvo/slike/1", Admin),
        ("GET",  "/api/v1/aktivnosti/999999/uputstvo/slike/1", Korisnik),

        ("GET",  "/api/v1/_diag/admin-only",                   Admin),
        ("GET",  "/api/v1/_diag/korisnik",                     Korisnik),
    ];

    public static IEnumerable<object[]> SveRute => Rute.Select(r => new object[] { r.Metod, r.Ruta });
    public static IEnumerable<object[]> AdminRute => Rute.Where(r => r.Politika is Admin or Supervizor).Select(r => new object[] { r.Metod, r.Ruta });
    public static IEnumerable<object[]> KorisnikRute => Rute.Where(r => r.Politika is not (Admin or Supervizor)).Select(r => new object[] { r.Metod, r.Ruta });
    public static IEnumerable<object[]> AdminDozvoljene => Rute.Where(r => r.Politika is not (Supervizor or Inzenjer)).Select(r => new object[] { r.Metod, r.Ruta });
    public static IEnumerable<object[]> SupervizorRute => Rute.Where(r => r.Politika == Supervizor).Select(r => new object[] { r.Metod, r.Ruta });
    public static IEnumerable<object[]> AdminZabranjene => Rute.Where(r => r.Politika is Supervizor or Inzenjer).Select(r => new object[] { r.Metod, r.Ruta });

    private static HttpRequestMessage Zahtev(string metod, string ruta)
    {
        var req = new HttpRequestMessage(new HttpMethod(metod), ruta);
        if (metod is "POST" or "PUT")
            // Rute za fajlove primaju samo multipart; JSON bi ruting odbio sa 415 pre provere prava.
            req.Content = ruta.EndsWith("/uvoz")
                ? new MultipartFormDataContent()
                : new StringContent("{}", Encoding.UTF8, "application/json");   // prazno telo → najviše 400
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
    [MemberData(nameof(SupervizorRute))]
    public async Task Supervizor_na_svojim_rutama_ne_dobija_401_ni_403(string metod, string ruta)
    {
        await factory.SeedUserAsync("supervizor-matrica", "Supervizor2026!", Roles.Supervizor);
        var sup = await factory.AuthenticatedClientAsync("supervizor-matrica", "Supervizor2026!");
        var res = await sup.SendAsync(Zahtev(metod, ruta));
        res.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AdminZabranjene))]
    public async Task Admin_bez_prava_rasporeda_i_popunjavanja_dobija_403(string metod, string ruta)
    {
        var admin = await factory.AdminClientAsync();
        (await admin.SendAsync(Zahtev(metod, ruta))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AdminDozvoljene))]
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
