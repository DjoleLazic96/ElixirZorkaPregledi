using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Features.ServerStanje.Dtos;

namespace TpmPregledi.Api.IntegrationTests.Endpoints;

/// <summary>
/// <c>GET /api/v1/stanje-servera</c> (Admin): sve tri grupe, baza.veza=true nad pravim Postgres-om sa
/// verzijom/veličinom/konekcijama/odzivom, backup null (folder ne postoji), Korisnik → 403.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ServerStanjeEndpointsTests(CustomWebAppFactory factory)
{
    [Fact]
    public async Task Admin_dobija_200_sa_sve_tri_grupe()
    {
        var admin = await factory.AdminClientAsync();

        var res = await admin.GetAsync("/api/v1/stanje-servera");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<ServerStanjeDto>();

        dto!.Aplikacija.Verzija.Should().NotBeNullOrWhiteSpace();
        dto.Aplikacija.GitSha.Should().NotBeNullOrWhiteSpace();
        dto.Aplikacija.DotnetVerzija.Should().StartWith("10.");
        dto.Aplikacija.Okruzenje.Should().Be("Development");
        dto.Aplikacija.UptimeSekundi.Should().BeGreaterThanOrEqualTo(0);
        dto.Aplikacija.VremeUtc.Kind.Should().Be(DateTimeKind.Utc);

        dto.Baza.Veza.Should().BeTrue();
        dto.Baza.Greska.Should().BeNull();
        dto.Baza.PostgresVerzija.Should().StartWith("16");
        dto.Baza.VelicinaBazeBytes.Should().BeGreaterThan(0);
        dto.Baza.AktivneKonekcije.Should().BeGreaterThanOrEqualTo(1, "bar naša konekcija");
        dto.Baza.OdzivMs.Should().BeGreaterThanOrEqualTo(0);
        dto.Baza.PoslednjiBackupUtc.Should().BeNull("backup folder u testu ne postoji");

        // Host zavisi od platforme (Windows/Linux imaju implementaciju, ostalo null) — ne fiksiramo.
        if (dto.Host is not null)
            dto.Host.OsOpis.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Korisnik_dobija_403()
    {
        var korisnik = await factory.KorisnikClientAsync();
        (await korisnik.GetAsync("/api/v1/stanje-servera")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
