using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Common.Pagination;
using TpmPregledi.Application.Features.Kategorije.Dtos;

namespace TpmPregledi.Api.IntegrationTests.Endpoints;

/// <summary>
/// PRIMER feature-a kroz API: CRUD (201 + Location, 200, 404), 400 oblik, 422 duplikat šifre,
/// soft delete sa filtriranim unikatom, paginacija/pretraga/sort, Korisnik čita ali ne piše (403).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class KategorijeEndpointsTests(CustomWebAppFactory factory)
{
    private static string Sifra(string prefix = "K") => $"{prefix}-{Guid.NewGuid():N}"[..12];

    private static async Task<KategorijaDto> KreirajAsync(HttpClient admin, string sifra, string naziv, string? opis = null)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/kategorije", new KategorijaInput(sifra, naziv, opis));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<KategorijaDto>())!;
    }

    [Fact]
    public async Task Post_vraca_201_sa_Location_i_Get_po_id_vraca_isto()
    {
        var admin = await factory.AdminClientAsync();
        var sifra = Sifra();

        var res = await admin.PostAsJsonAsync("/api/v1/kategorije", new KategorijaInput(sifra, "Prva", "Opis"));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<KategorijaDto>();
        res.Headers.Location!.ToString().Should().Be($"/api/v1/kategorije/{dto!.Id}");
        dto.Sifra.Should().Be(sifra);
        dto.Aktivan.Should().BeTrue();
        dto.CreatedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));
        dto.UpdatedAt.Should().BeNull();

        var get = await admin.GetFromJsonAsync<KategorijaDto>($"/api/v1/kategorije/{dto.Id}");
        // Postgres čuva mikrosekunde, .NET tikove (100 ns) — zato tolerancija od 1 ms na datumima.
        get.Should().BeEquivalentTo(dto, o => o
            .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
            .WhenTypeIs<DateTime>());
    }

    [Fact]
    public async Task Post_sa_praznom_sifrom_vraca_400_pod_sifra()
    {
        var admin = await factory.AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/v1/kategorije", new KategorijaInput("", "Naziv", null));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").GetProperty("sifra")[0].GetString().Should().Be("Šifra je obavezna.");
    }

    [Fact]
    public async Task Post_duplikata_aktivne_sifre_vraca_422_sa_porukom()
    {
        var admin = await factory.AdminClientAsync();
        var sifra = Sifra("DUP");
        await KreirajAsync(admin, sifra, "Prva");

        var res = await admin.PostAsJsonAsync("/api/v1/kategorije", new KategorijaInput(sifra, "Druga", null));

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("greske")[0].GetString().Should().Be("Kategorija sa ovom šifrom već postoji.");
    }

    [Fact]
    public async Task Put_menja_sva_polja_i_puni_updatedAt()
    {
        var admin = await factory.AdminClientAsync();
        var dto = await KreirajAsync(admin, Sifra(), "Pre");
        var novaSifra = Sifra("PUT");

        var res = await admin.PutAsJsonAsync($"/api/v1/kategorije/{dto.Id}", new KategorijaInput(novaSifra, "Posle", "Novi opis"));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<KategorijaDto>();
        updated!.Sifra.Should().Be(novaSifra);
        updated.Naziv.Should().Be("Posle");
        updated.Opis.Should().Be("Novi opis");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Deaktivacija_oslobadja_sifru_a_aktivacija_stare_vraca_422()
    {
        var admin = await factory.AdminClientAsync();
        var sifra = Sifra("SD");
        var stara = await KreirajAsync(admin, sifra, "Stara");

        var deakt = await admin.PostAsync($"/api/v1/kategorije/{stara.Id}/deaktiviraj", null);
        deakt.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deakt.Content.ReadFromJsonAsync<KategorijaDto>())!.Aktivan.Should().BeFalse();

        var aktivne = await admin.GetFromJsonAsync<StranicaDto<KategorijaDto>>($"/api/v1/kategorije?pretraga={sifra}&aktivan=true");
        aktivne!.Stavke.Should().BeEmpty("deaktivirana nije u aktivnima");

        var nova = await KreirajAsync(admin, sifra, "Nova sa istom šifrom");   // filtrirani unikat dozvoljava
        nova.Id.Should().NotBe(stara.Id);

        var akt = await admin.PostAsync($"/api/v1/kategorije/{stara.Id}/aktiviraj", null);
        akt.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "aktivna sa istom šifrom već postoji");
    }

    [Fact]
    public async Task Lista_paginira_pretrazuje_i_sortira()
    {
        var admin = await factory.AdminClientAsync();
        var prefix = "PAG" + Guid.NewGuid().ToString("N")[..5];
        await KreirajAsync(admin, prefix + "-2", "Beta " + prefix);
        await KreirajAsync(admin, prefix + "-1", "Alfa " + prefix);
        await KreirajAsync(admin, prefix + "-3", "Gama " + prefix);

        var str1 = await admin.GetFromJsonAsync<StranicaDto<KategorijaDto>>($"/api/v1/kategorije?pretraga={prefix.ToLower()}&sort=naziv&smer=desc&velicina=2&stranica=1");
        str1!.Ukupno.Should().Be(3);
        str1.Stranica.Should().Be(1);
        str1.Velicina.Should().Be(2);
        str1.Stavke.Select(k => k.Naziv).Should().Equal("Gama " + prefix, "Beta " + prefix);

        var str2 = await admin.GetFromJsonAsync<StranicaDto<KategorijaDto>>($"/api/v1/kategorije?pretraga={prefix}&sort=naziv&smer=desc&velicina=2&stranica=2");
        str2!.Stavke.Select(k => k.Naziv).Should().Equal("Alfa " + prefix);

        var poSifri = await admin.GetFromJsonAsync<StranicaDto<KategorijaDto>>($"/api/v1/kategorije?pretraga={prefix}");
        poSifri!.Stavke.Select(k => k.Sifra).Should().Equal(prefix + "-1", prefix + "-2", prefix + "-3");

        var nepoznatSort = await admin.GetAsync($"/api/v1/kategorije?pretraga={prefix}&sort=nepostoji&velicina=9999");
        nepoznatSort.StatusCode.Should().Be(HttpStatusCode.OK, "nepoznat sort i prevelika veličina se tiho normalizuju");
        (await nepoznatSort.Content.ReadFromJsonAsync<StranicaDto<KategorijaDto>>())!.Velicina.Should().Be(200);
    }

    [Fact]
    public async Task Nepostojeca_vraca_404()
    {
        var admin = await factory.AdminClientAsync();
        (await admin.GetAsync("/api/v1/kategorije/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsync("/api/v1/kategorije/999999/deaktiviraj", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Korisnik_cita_ali_ne_moze_da_pise()
    {
        var admin = await factory.AdminClientAsync();
        var dto = await KreirajAsync(admin, Sifra("RO"), "Samo čitanje");
        var korisnik = await factory.KorisnikClientAsync();

        (await korisnik.GetAsync("/api/v1/kategorije")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await korisnik.GetAsync($"/api/v1/kategorije/{dto.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await korisnik.PostAsJsonAsync("/api/v1/kategorije", new KategorijaInput(Sifra(), "X", null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await korisnik.PutAsJsonAsync($"/api/v1/kategorije/{dto.Id}", new KategorijaInput(dto.Sifra, "Y", null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await korisnik.PostAsync($"/api/v1/kategorije/{dto.Id}/deaktiviraj", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
