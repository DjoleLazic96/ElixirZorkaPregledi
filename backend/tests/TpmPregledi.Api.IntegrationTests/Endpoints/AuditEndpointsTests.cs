using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Common.Pagination;
using TpmPregledi.Application.Features.Audit.Dtos;
using TpmPregledi.Application.Features.Kategorije.Dtos;

namespace TpmPregledi.Api.IntegrationTests.Endpoints;

/// <summary>
/// Audit log kroz API i bazu: START red postoji posle starta, LOGIN_FAIL nosi uneto ime i
/// <c>user_id = NULL</c> (plus IP iz forwarded hedera), LOGIN red uz prijavu, CREATE red za
/// <c>[Auditable]</c> entitet sa imenom korisnika, filteri i lista entiteta.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AuditEndpointsTests(CustomWebAppFactory factory)
{
    [Fact]
    public async Task START_red_postoji_posle_starta_sa_verzijom_i_system_korisnikom()
    {
        var admin = await factory.AdminClientAsync();

        var page = await admin.GetFromJsonAsync<StranicaDto<AuditRedDto>>("/api/v1/audit?akcija=START&entitet=Sistem");

        page!.Stavke.Should().NotBeEmpty();
        var start = page.Stavke.First();
        start.Korisnik.Should().Be("system");
        start.EntitetLabela.Should().Be("Sistem");
        start.EntitetId.Should().Be("start");
        var novo = JsonDocument.Parse(start.NoveVrednosti!).RootElement;
        novo.GetProperty("verzija").GetString().Should().NotBeNullOrWhiteSpace();
        novo.GetProperty("gitSha").GetString().Should().NotBeNullOrWhiteSpace();
        novo.GetProperty("okruzenje").GetString().Should().Be("Development");

        var uBazi = await factory.SaBazomAsync(db => db.AuditLog.Where(a => a.Action == "START").OrderBy(a => a.Id).FirstAsync());
        uBazi.UserId.Should().BeNull();
    }

    [Fact]
    public async Task Neuspesna_prijava_pise_LOGIN_FAIL_sa_unetim_imenom_bez_user_id_i_sa_ip_adresom()
    {
        var ime = "nepostojeci-" + Guid.NewGuid().ToString("N")[..8];
        var anon = factory.CreateClient();
        var login = await anon.PostAsJsonAsync("/api/v1/auth/login", new { userName = ime, password = "SigurnoPogresna9!" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var red = await factory.SaBazomAsync(db => db.AuditLog.SingleAsync(a => a.Action == "LOGIN_FAIL" && a.UserName == ime));

        red.UserId.Should().BeNull();
        red.Entity.Should().Be("Auth");
        red.IpAddress.Should().StartWith("10.", "RemoteIpAddress dolazi iz X-Forwarded-For kroz ForwardedHeaders middleware");

        var admin = await factory.AdminClientAsync();
        var page = await admin.GetFromJsonAsync<StranicaDto<AuditRedDto>>($"/api/v1/audit?akcija=LOGIN_FAIL&korisnik={ime}");
        page!.Stavke.Should().ContainSingle().Which.EntitetLabela.Should().Be("Prijava");
    }

    [Fact]
    public async Task Uspesna_prijava_pise_LOGIN_red_sa_user_id()
    {
        await factory.AdminClientAsync();

        var red = await factory.SaBazomAsync(db =>
            db.AuditLog.Where(a => a.Action == "LOGIN" && a.UserName == CustomWebAppFactory.AdminUserName).OrderByDescending(a => a.Id).FirstAsync());

        red.UserId.Should().NotBeNullOrWhiteSpace();
        red.Entity.Should().Be("Auth");
    }

    [Fact]
    public async Task Kreiranje_kategorije_ostavlja_CREATE_red_sa_imenom_korisnika()
    {
        var admin = await factory.AdminClientAsync();
        var sifra = "AUD-" + Guid.NewGuid().ToString("N")[..6];
        var dto = await (await admin.PostAsJsonAsync("/api/v1/kategorije", new KategorijaInput(sifra, "Audit test", null)))
            .Content.ReadFromJsonAsync<KategorijaDto>();

        var page = await admin.GetFromJsonAsync<StranicaDto<AuditRedDto>>("/api/v1/audit?entitet=Kategorija&akcija=CREATE");

        var red = page!.Stavke.Should().Contain(s => s.EntitetId == dto!.Id.ToString()).Subject;
        red.Korisnik.Should().Be("admin");
        red.EntitetLabela.Should().Be("Kategorija");
        red.StareVrednosti.Should().BeNull();
        red.NoveVrednosti.Should().Contain(sifra);
    }

    [Fact]
    public async Task Lista_je_paginirana_i_sortirana_najnovije_prvo()
    {
        var admin = await factory.AdminClientAsync();

        var page = await admin.GetFromJsonAsync<StranicaDto<AuditRedDto>>("/api/v1/audit?stranica=1&velicina=5");

        page!.Stranica.Should().Be(1);
        page.Velicina.Should().Be(5);
        page.Stavke.Should().HaveCountLessThanOrEqualTo(5);
        page.Stavke.Select(s => s.Vreme).Should().BeInDescendingOrder();
        page.Ukupno.Should().BeGreaterThanOrEqualTo(page.Stavke.Count);
    }

    [Fact]
    public async Task Entiteti_vracaju_srpske_labele()
    {
        var admin = await factory.AdminClientAsync();

        var lista = await admin.GetFromJsonAsync<List<EntitetLabelaDto>>("/api/v1/audit/entiteti");

        lista.Should().Contain(e => e.Entitet == "Kategorija" && e.Labela == "Kategorija");
        lista.Should().Contain(e => e.Entitet == "ApplicationUser" && e.Labela == "Korisnik");
        lista.Should().Contain(e => e.Entitet == "Auth" && e.Labela == "Prijava");
        lista.Should().Contain(e => e.Entitet == "Sistem" && e.Labela == "Sistem");
    }

    [Fact]
    public async Task Filter_od_do_prihvata_zone_less_datum_bez_500()
    {
        var admin = await factory.AdminClientAsync();

        var res = await admin.GetAsync("/api/v1/audit?od=2020-01-01T00:00:00&do=2099-12-31T23:59:59");

        res.StatusCode.Should().Be(HttpStatusCode.OK, "Kind=Unspecified se normalizuje na UTC pre Npgsql upita");
    }
}
