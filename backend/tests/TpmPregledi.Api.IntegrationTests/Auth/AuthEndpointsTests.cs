using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Features.Auth.Dtos;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Api.IntegrationTests.Auth;

/// <summary>
/// Ceo auth tok nad pravom bazom: prijava, generična 401 poruka, lockout posle 5 promašaja,
/// rotacija refresh tokena (stari cookie odbijen), odjava briše cookie i revokuje, promena lozinke
/// (uspeh + pogrešna trenutna → 400 + stari refresh revokovan), rate limit 10/min po IP-u.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AuthEndpointsTests(CustomWebAppFactory factory)
{
    private const string Cookie = "tpm-pregledi.refresh";

    private static string VrednostCookieja(HttpResponseMessage res)
    {
        var setCookie = res.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(Cookie + "=", StringComparison.Ordinal));
        return setCookie.Split(';')[0][(Cookie.Length + 1)..];
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });

    /// <summary>Refresh sa ručno zadatim cookie-jem (klijent bez cookie container-a).</summary>
    private async Task<HttpResponseMessage> RefreshSaCookiejemAsync(string cookieVrednost)
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.Add("Cookie", $"{Cookie}={cookieVrednost}");
        return await client.SendAsync(req);
    }

    // ─── Prijava ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Prijava_admina_vraca_token_rolu_i_refresh_cookie()
    {
        var client = factory.CreateClientWithCookies();
        var res = await LoginAsync(client, CustomWebAppFactory.AdminUserName, CustomWebAppFactory.AdminPassword);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.Roles.Should().Equal(Roles.Admin);
        body.User.UserName.Should().Be("admin");
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var setCookie = res.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(Cookie + "="));
        setCookie.Should().Contain("httponly", "JS ne sme da vidi refresh token").And.Contain("samesite=strict").And.Contain("path=/api/v1/auth");
    }

    [Theory]
    [InlineData(CustomWebAppFactory.AdminUserName, "SigurnoPogresna9!")]
    [InlineData("nepostojeci-korisnik", "BiloKoja2026!")]
    public async Task Neuspesna_prijava_vraca_401_sa_istom_genericnom_porukom(string userName, string password)
    {
        var res = await LoginAsync(factory.CreateClient(), userName, password);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Be("Neispravno korisničko ime ili lozinka.");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Prijava_bez_polja_vraca_400_sa_errors()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { userName = "", password = "" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").GetProperty("userName")[0].GetString().Should().Be("Korisničko ime je obavezno.");
    }

    [Fact]
    public async Task Pet_promasaja_zakljucava_nalog_pa_ni_tacna_lozinka_ne_prolazi()
    {
        await factory.SeedUserAsync("lockout-user", "Lockout2026!", Roles.Korisnik);
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            (await LoginAsync(client, "lockout-user", "Pogresna9!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await LoginAsync(client, "lockout-user", "Lockout2026!")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "nalog je zaključan 10 minuta");
    }

    [Fact]
    public async Task Jedanaesta_prijava_sa_iste_adrese_u_minutu_dobija_429()
    {
        var client = factory.CreateClient();   // jedan klijent = jedan IP = jedna particija

        for (var i = 0; i < 10; i++)
            (await LoginAsync(client, "niko-" + i, "Bilo2026!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await LoginAsync(client, "niko-11", "Bilo2026!")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ─── Refresh ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_rotira_token_a_stari_cookie_je_odbijen()
    {
        var client = factory.CreateClientWithCookies();
        var login = await LoginAsync(client, CustomWebAppFactory.AdminUserName, CustomWebAppFactory.AdminPassword);
        var stariCookie = VrednostCookieja(login);
        var stariAccess = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var noviCookie = VrednostCookieja(refresh);
        var noviAccess = (await refresh.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        noviCookie.Should().NotBe(stariCookie);
        noviAccess.Should().NotBe(stariAccess);

        (await RefreshSaCookiejemAsync(stariCookie)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "rotacija revokuje stari token");
        (await RefreshSaCookiejemAsync(noviCookie)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_bez_cookieja_ili_sa_izmisljenim_vraca_401()
    {
        (await factory.CreateClient().PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RefreshSaCookiejemAsync("izmisljen-token")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Odjava ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Odjava_brise_cookie_na_istoj_putanji_i_revokuje_token()
    {
        var client = factory.CreateClientWithCookies();
        var login  = await LoginAsync(client, CustomWebAppFactory.AdminUserName, CustomWebAppFactory.AdminPassword);
        var cookie = VrednostCookieja(login);
        var access = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new("Bearer", access);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var setCookie = logout.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(Cookie + "="));
        setCookie.Should().Contain("path=/api/v1/auth", "brisanje mora biti na istoj putanji kao postavljanje");
        setCookie.Should().MatchRegex("expires=.*1970", "istekao datum = browser briše cookie");

        (await RefreshSaCookiejemAsync(cookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "token je revokovan");
    }

    // ─── Promena lozinke ────────────────────────────────────────────────────

    [Fact]
    public async Task Promena_lozinke_vraca_204_revokuje_stari_refresh_i_nova_lozinka_radi()
    {
        var ime = "promena-" + Guid.NewGuid().ToString("N")[..8];
        await factory.SeedUserAsync(ime, "Stara2026!", Roles.Korisnik);

        var client = factory.CreateClientWithCookies();
        var login  = await LoginAsync(client, ime, "Stara2026!");
        var cookie = VrednostCookieja(login);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);

        var res = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Stara2026!", newPassword = "Nova2026!" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RefreshSaCookiejemAsync(cookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "svi refresh tokeni su revokovani");
        (await LoginAsync(factory.CreateClient(), ime, "Stara2026!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(factory.CreateClient(), ime, "Nova2026!")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Promena_lozinke_sa_pogresnom_trenutnom_vraca_400_pod_currentPassword()
    {
        var admin = await factory.AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Pogresna9!", newPassword = "Nova2026!" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").GetProperty("currentPassword")[0].GetString().Should().Be("Trenutna lozinka nije ispravna.");
    }

    [Fact]
    public async Task Promena_lozinke_sa_slabom_novom_vraca_400_pod_newPassword_na_srpskom()
    {
        var admin = await factory.AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = CustomWebAppFactory.AdminPassword, newPassword = "samomalaslova" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        var poruke = problem.GetProperty("errors").GetProperty("newPassword").EnumerateArray().Select(e => e.GetString()).ToList();
        poruke.Should().Contain("Lozinka mora sadržati najmanje jednu cifru (0–9).");
    }

    // ─── Me ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_vraca_korisnika_i_tacno_jednu_rolu()
    {
        var admin = await factory.AdminClientAsync();
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        me.GetProperty("user").GetProperty("userName").GetString().Should().Be("admin");
        me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal(Roles.Admin);
    }
}
