using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Features.Auth.Dtos;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Api.IntegrationTests.Endpoints;

/// <summary>
/// Upravljanje korisnicima kroz API: kreiranje (201 + Location), 422/400 oblici, lista i filter,
/// prijava novog korisnika sa tačno jednom rolom, deaktivacija/aktivacija i reset koji revokuju
/// sesije, izmena role, četiri guarda → 422 sa tačnim porukama, 404, Korisnik → 403.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class KorisniciEndpointsTests(CustomWebAppFactory factory)
{
    private const string Lozinka = "Korisnik2026!";

    private static string Ime(string prefix = "kor") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static KreirajKorisnikaInput Novi(string ime, string rola = Roles.Korisnik, string lozinka = Lozinka) =>
        new(ime, "Test Korisnik", $"{ime}@test.local", lozinka, rola);

    private static async Task<KorisnikDto> KreirajAsync(HttpClient admin, KreirajKorisnikaInput input)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/korisnici", input);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<KorisnikDto>())!;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string ime, string lozinka) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { userName = ime, password = lozinka });

    private async Task<KorisnikDto> SeedAdminDtoAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<KorisnikDto>>("/api/v1/korisnici"))!.Single(k => k.KorisnickoIme == CustomWebAppFactory.AdminUserName);

    // ─── Kreiranje i lista ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_kreira_korisnika_sa_Location_i_pojavljuje_se_u_listi()
    {
        var admin = await factory.AdminClientAsync();
        var ime = Ime();

        var res = await admin.PostAsJsonAsync("/api/v1/korisnici", Novi(ime));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<KorisnikDto>();
        res.Headers.Location!.ToString().Should().Be($"/api/v1/korisnici/{dto!.Id}");
        dto.KorisnickoIme.Should().Be(ime);
        dto.Rola.Should().Be(Roles.Korisnik);
        dto.Email.Should().Be($"{ime}@test.local");
        dto.Aktivan.Should().BeTrue();
        dto.PoslednjaPrijava.Should().BeNull();

        var svi = await admin.GetFromJsonAsync<List<KorisnikDto>>("/api/v1/korisnici");
        svi!.Should().Contain(k => k.KorisnickoIme == ime);
        var neaktivni = await admin.GetFromJsonAsync<List<KorisnikDto>>("/api/v1/korisnici?aktivan=false");
        neaktivni!.Should().NotContain(k => k.KorisnickoIme == ime);
    }

    [Fact]
    public async Task Post_sa_zauzetim_imenom_vraca_422()
    {
        var admin = await factory.AdminClientAsync();
        var ime = Ime();
        await KreirajAsync(admin, Novi(ime));

        var dup = await admin.PostAsJsonAsync("/api/v1/korisnici", Novi(ime));

        dup.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_sa_slabom_lozinkom_vraca_400_pod_lozinka_na_srpskom()
    {
        var admin = await factory.AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/v1/korisnici", Novi(Ime(), lozinka: "samomalaslova"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").GetProperty("lozinka").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("Lozinka mora sadržati najmanje jedno veliko slovo (A–Z).");
    }

    [Fact]
    public async Task Post_sa_nepoznatom_rolom_vraca_400_pod_rola()
    {
        var admin = await factory.AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/v1/korisnici", Novi(Ime(), rola: "Superadmin"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").GetProperty("rola")[0].GetString().Should().StartWith("Nepoznata rola.");
    }

    [Fact]
    public async Task Kreiran_korisnik_moze_da_se_prijavi_i_ima_tacno_jednu_rolu()
    {
        var admin = await factory.AdminClientAsync();
        var ime = Ime("preg");
        await KreirajAsync(admin, Novi(ime));

        var login = await LoginAsync(factory.CreateClientWithCookies(), ime, Lozinka);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Roles.Should().Equal(Roles.Korisnik);

        var korisnik = await factory.AuthenticatedClientAsync(ime, Lozinka);
        (await korisnik.GetAsync("/api/v1/kategorije")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await korisnik.GetAsync("/api/v1/korisnici")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Deaktivacija / aktivacija / reset ──────────────────────────────────

    [Fact]
    public async Task Deaktivacija_vraca_dto_blokira_prijavu_i_revokuje_refresh_a_aktivacija_vraca()
    {
        var admin = await factory.AdminClientAsync();
        var ime = Ime("deakt");
        var dto = await KreirajAsync(admin, Novi(ime));

        // Otvorena sesija korisnika pre deaktivacije.
        var sesija = factory.CreateClientWithCookies();
        (await LoginAsync(sesija, ime, Lozinka)).EnsureSuccessStatusCode();

        var deakt = await admin.PostAsync($"/api/v1/korisnici/{dto.Id}/deaktiviraj", null);
        deakt.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deakt.Content.ReadFromJsonAsync<KorisnikDto>())!.Aktivan.Should().BeFalse();

        (await LoginAsync(factory.CreateClient(), ime, Lozinka)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await sesija.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "refresh tokeni su revokovani");

        var akt = await admin.PostAsync($"/api/v1/korisnici/{dto.Id}/aktiviraj", null);
        akt.StatusCode.Should().Be(HttpStatusCode.OK);
        (await akt.Content.ReadFromJsonAsync<KorisnikDto>())!.Aktivan.Should().BeTrue();
        (await LoginAsync(factory.CreateClient(), ime, Lozinka)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_lozinke_vraca_204_menja_lozinku_i_revokuje_refresh()
    {
        var admin = await factory.AdminClientAsync();
        var ime = Ime("reset");
        var dto = await KreirajAsync(admin, Novi(ime));
        var sesija = factory.CreateClientWithCookies();
        (await LoginAsync(sesija, ime, Lozinka)).EnsureSuccessStatusCode();

        var res = await admin.PostAsJsonAsync($"/api/v1/korisnici/{dto.Id}/reset-lozinka", new ResetLozinkeInput("NovaLozinka9!"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sesija.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(factory.CreateClient(), ime, Lozinka)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(factory.CreateClient(), ime, "NovaLozinka9!")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── Izmena ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_menja_ime_email_i_rolu_i_revokuje_refresh_pri_promeni_role()
    {
        var admin = await factory.AdminClientAsync();
        var ime = Ime("upd");
        var dto = await KreirajAsync(admin, Novi(ime));
        var sesija = factory.CreateClientWithCookies();
        (await LoginAsync(sesija, ime, Lozinka)).EnsureSuccessStatusCode();

        var res = await admin.PutAsJsonAsync($"/api/v1/korisnici/{dto.Id}", new IzmeniKorisnikaInput("Promenjeno Ime", "novi@test.local", Roles.Admin));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<KorisnikDto>();
        updated!.Ime.Should().Be("Promenjeno Ime");
        updated.Email.Should().Be("novi@test.local");
        updated.Rola.Should().Be(Roles.Admin);
        (await sesija.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "promena role revokuje sesije");

        var login = await LoginAsync(factory.CreateClient(), ime, Lozinka);
        (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Roles.Should().ContainSingle("tačno jedna rola, stara je skinuta").Which.Should().Be(Roles.Admin);
    }

    [Fact]
    public async Task Put_nepostojeceg_vraca_404()
    {
        var admin = await factory.AdminClientAsync();
        var res = await admin.PutAsJsonAsync($"/api/v1/korisnici/{Guid.NewGuid()}", new IzmeniKorisnikaInput("Neko", null, Roles.Korisnik));
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Guardovi → 422 ─────────────────────────────────────────────────────

    private static async Task<string> Greska422Async(HttpResponseMessage res)
    {
        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("greske")[0].GetString()!;
    }

    [Fact]
    public async Task Guard_1_promena_sopstvene_role_vraca_422()
    {
        var admin = await factory.AdminClientAsync();
        var ja = await SeedAdminDtoAsync(admin);

        var res = await admin.PutAsJsonAsync($"/api/v1/korisnici/{ja.Id}", new IzmeniKorisnikaInput(ja.Ime, ja.Email, Roles.Korisnik));

        (await Greska422Async(res)).Should().Be("Ne možete promeniti sopstvenu rolu.");
    }

    [Fact]
    public async Task Guard_2_samodeaktivacija_vraca_422()
    {
        var admin = await factory.AdminClientAsync();
        var ja = await SeedAdminDtoAsync(admin);

        var res = await admin.PostAsync($"/api/v1/korisnici/{ja.Id}/deaktiviraj", null);

        (await Greska422Async(res)).Should().Be("Ne možete deaktivirati sami sebe.");
    }

    [Fact]
    public async Task Guard_3_i_4_stale_token_deaktiviranog_admina_ne_moze_da_ukloni_poslednjeg_admina()
    {
        // Jedini način da aktivan Admin pokuša da ukloni DRUGOG poslednjeg admina je da sam već bude
        // deaktiviran, a JWT mu još važi (do 15 min). Guardovi moraju da drže i tada.
        var admin = await factory.AdminClientAsync();
        var drugi = await KreirajAsync(admin, Novi(Ime("adm2"), rola: Roles.Admin));
        var drugiClient = await factory.AuthenticatedClientAsync(drugi.KorisnickoIme, Lozinka);   // token izdat dok je aktivan
        (await admin.PostAsync($"/api/v1/korisnici/{drugi.Id}/deaktiviraj", null)).EnsureSuccessStatusCode();
        var seedAdmin = await SeedAdminDtoAsync(admin);

        var deakt = await drugiClient.PostAsync($"/api/v1/korisnici/{seedAdmin.Id}/deaktiviraj", null);
        (await Greska422Async(deakt)).Should().Be("Ne možete deaktivirati poslednjeg aktivnog administratora.");

        var degrad = await drugiClient.PutAsJsonAsync($"/api/v1/korisnici/{seedAdmin.Id}", new IzmeniKorisnikaInput(seedAdmin.Ime, seedAdmin.Email, Roles.Korisnik));
        (await Greska422Async(degrad)).Should().Be("Ne možete skinuti rolu Admin poslednjem aktivnom administratoru.");

        (await SeedAdminDtoAsync(admin)).Aktivan.Should().BeTrue("seed admin je netaknut");
    }

    // ─── Autorizacija ───────────────────────────────────────────────────────

    [Fact]
    public async Task Korisnik_dobija_403_na_listi_i_kreiranju()
    {
        var korisnik = await factory.KorisnikClientAsync();

        (await korisnik.GetAsync("/api/v1/korisnici")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await korisnik.PostAsJsonAsync("/api/v1/korisnici", Novi(Ime()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
