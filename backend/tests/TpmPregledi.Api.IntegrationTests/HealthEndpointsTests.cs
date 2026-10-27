using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TpmPregledi.Api.IntegrationTests.Infrastructure;

namespace TpmPregledi.Api.IntegrationTests;

/// <summary>Aplikacija se diže nad pravim Postgres-om, migracije prolaze, sva tri health endpointa rade anonimno.</summary>
[Collection(nameof(PostgresCollection))]
public class HealthEndpointsTests(CustomWebAppFactory factory)
{
    [Theory]
    [InlineData("/api/v1/health", "ok")]
    [InlineData("/api/v1/health/live", "live")]
    [InlineData("/api/v1/health/ready", "ready")]
    public async Task Health_endpoint_vraca_200_i_status(string ruta, string status)
    {
        var res = await factory.CreateClient().GetAsync(ruta);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["status"].Should().Be(status);
    }

    [Fact]
    public async Task Health_nosi_bezbednosne_hedere()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/health");

        res.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        res.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        res.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
    }
}
