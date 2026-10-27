using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Features.Version.Dtos;

namespace TpmPregledi.Api.IntegrationTests;

/// <summary><c>GET /api/v1/version</c> je anoniman (deploy smoke test) i vraća verziju, SHA i okruženje.</summary>
[Collection(nameof(PostgresCollection))]
public class VersionEndpointsTests(CustomWebAppFactory factory)
{
    [Fact]
    public async Task Anonimni_dobija_200_sa_verzijom_i_sha()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/version");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<VerzijaDto>();
        dto!.Verzija.Should().NotBeNullOrWhiteSpace();
        dto.GitSha.Should().NotBeNullOrWhiteSpace();
        dto.GitSha.Length.Should().BeLessThanOrEqualTo(7);
        dto.Okruzenje.Should().Be("Development");
    }

    [Fact]
    public async Task Prijavljeni_dobija_isti_odgovor()
    {
        var admin = await factory.AdminClientAsync();
        var dto = await admin.GetFromJsonAsync<VerzijaDto>("/api/v1/version");
        dto!.Verzija.Should().NotBeNullOrWhiteSpace();
    }
}
