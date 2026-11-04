using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Sabloni;
using TpmPregledi.Application.Features.Sekcije;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Api.IntegrationTests.Endpoints;

/// <summary>Faza 01 kroz ceo API: sekcije iz migracije, podešavanja, prava i njihovo dejstvo na pristup.</summary>
[Collection(nameof(PostgresCollection))]
public class OsnovaEndpointsTests(CustomWebAppFactory factory)
{
    [Fact]
    public async Task Sekcije_iz_migracije_su_sve_osam_po_redu()
    {
        var admin = await factory.AdminClientAsync();
        var lista = await admin.GetFromJsonAsync<List<SekcijaDto>>("/api/v1/sekcije");
        lista!.Select(s => s.Sifra).Should().Equal("10", "20", "21", "30", "40", "50", "60", "70");
    }

    [Fact]
    public async Task Podesavanja_se_menjaju_i_citaju_nazad()
    {
        var admin = await factory.AdminClientAsync();
        var novo = PodesavanjaDto.Podrazumevana with { GranicaPauzeMin = 12 };

        (await admin.PutAsJsonAsync("/api/v1/podesavanja", novo)).EnsureSuccessStatusCode();

        (await admin.GetFromJsonAsync<PodesavanjaDto>("/api/v1/podesavanja"))!.GranicaPauzeMin.Should().Be(12);
        (await admin.PutAsJsonAsync("/api/v1/podesavanja", PodesavanjaDto.Podrazumevana)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Prijava_vraca_prava_a_nova_prava_vaze_posle_ponovne_prijave()
    {
        var user  = await factory.SeedUserAsync("sup-prava", "Supervizor2026!", Roles.Supervizor);
        var admin = await factory.AdminClientAsync();

        // Supervizor podrazumevano nema pravo „podesavanja“ → 403.
        var sup = await factory.AuthenticatedClientAsync("sup-prava", "Supervizor2026!");
        (await sup.GetAsync("/api/v1/podesavanja")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var res = await admin.PutAsJsonAsync($"/api/v1/korisnici/{user.Id}/prava",
            new PostaviPravaInput([.. Prava.Podrazumevana(Roles.Supervizor), Prava.Podesavanja]));
        res.EnsureSuccessStatusCode();
        (await res.Content.ReadFromJsonAsync<KorisnikDto>())!.Prava.Should().Contain(Prava.Podesavanja);

        var ponovo = await factory.AuthenticatedClientAsync("sup-prava", "Supervizor2026!");
        (await ponovo.GetAsync("/api/v1/podesavanja")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Popunjavanje_pregleda_se_ne_dodeljuje_Direktoru()
    {
        var user  = await factory.SeedUserAsync("dir-prava", "Direktor2026!", Roles.Direktor);
        var admin = await factory.AdminClientAsync();

        var res = await admin.PutAsJsonAsync($"/api/v1/korisnici/{user.Id}/prava",
            new PostaviPravaInput([Prava.PreglediPopunjavanje]));

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

/// <summary>Uvoz šablona kroz API (multipart) nad pravim fajlom iz repoa.</summary>
[Collection(nameof(PostgresCollection))]
public class UvozSablonaEndpointsTests(CustomWebAppFactory factory)
{
    [Fact]
    public async Task Pravi_fajl_vraca_422_samo_za_red_PZ1_bez_naziva_i_ne_upisuje_nista()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "docs", "sabloni", "sabloni-za-uvoz.xlsx"))) dir = dir.Parent;
        var admin = await factory.AdminClientAsync();

        using var telo = new MultipartFormDataContent();
        telo.Add(new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(dir.FullName, "docs", "sabloni", "sabloni-za-uvoz.xlsx"))),
            "fajl", "sabloni-za-uvoz.xlsx");
        var res = await admin.PostAsync("/api/v1/sabloni/uvoz", telo);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("nedostaje naziv ure");
        var sabloni = await admin.GetFromJsonAsync<List<SablonPregledDto>>("/api/v1/sabloni");
        sabloni!.Should().OnlyContain(s => s.VazecaVerzija == null);
    }

    [Fact]
    public async Task Bez_fajla_vraca_400()
    {
        var admin = await factory.AdminClientAsync();
        using var telo = new MultipartFormDataContent();
        (await admin.PostAsync("/api/v1/sabloni/uvoz", telo)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
