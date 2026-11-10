using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Persistence;

namespace TpmPregledi.Api.IntegrationTests.Endpoints;

/// <summary>Sinhronizacija telefona i istovremene odluke o pregledu nad pravim Postgres-om.</summary>
[Collection(nameof(PostgresCollection))]
public class SinhronizacijaEndpointsTests(CustomWebAppFactory factory)
{
    private async Task<int> PregledAsync(string dodeljenId, string status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = new Pregled
        {
            Broj = "IT-" + Guid.NewGuid().ToString("N")[..12], Tip = "DP1", Status = status, DodeljenId = dodeljenId,
            PlaniraniDatum = new DateOnly(2026, 11, 9), PrvobitniDatum = new DateOnly(2026, 11, 9),
        };
        db.Pregledi.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Neispravna_operacija_se_odbija_a_ne_vraca_500_koji_bi_zaustavio_telefon()
    {
        var inz = await factory.SeedUserAsync("inz-sinh", "Inzenjer2026!", Roles.Inzenjer);
        var pregledId = await PregledAsync(inz.Id, StatusPregleda.URadu);
        var klijent = await factory.AuthenticatedClientAsync("inz-sinh", "Inzenjer2026!");
        var neispravna = Guid.NewGuid();
        var nepostojeci = Guid.NewGuid();

        var res = await klijent.PostAsJsonAsync("/api/v1/sinhronizacija", new
        {
            uredjajKlijenta = "it-telefon",
            klijentSada = DateTime.UtcNow,
            operacije = new object[]
            {
                new { opId = neispravna, vrsta = "odgovor", pregledId, klijentAt = DateTime.UtcNow, podaci = new { stavkaId = 1 } },
                new { opId = nepostojeci, vrsta = "odgovor", pregledId = int.MaxValue, klijentAt = DateTime.UtcNow, podaci = new { stavkaId = 1, vrednost = 0 } },
            },
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var r = (await res.Content.ReadFromJsonAsync<SinhronizacijaRezultat>())!;
        r.Odbijene.Select(o => o.OpId).Should().Equal(neispravna, nepostojeci);
        r.Odbijene[0].Razlog.Should().Be(PopunjavanjeHandler.PorukaNeispravnaOperacija);
    }

    [Fact]
    public async Task Istovremena_izmena_pregleda_ne_prepisuje_prethodnu_odluku()
    {
        var inz = await factory.SeedUserAsync("inz-konflikt", "Inzenjer2026!", Roles.Inzenjer);
        var pregledId = await PregledAsync(inz.Id, StatusPregleda.CekaOveru);

        await using var s1 = factory.Services.CreateAsyncScope();
        await using var s2 = factory.Services.CreateAsyncScope();
        var db1 = s1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = s2.ServiceProvider.GetRequiredService<AppDbContext>();
        var p1 = await db1.Pregledi.SingleAsync(p => p.Id == pregledId);
        var p2 = await db2.Pregledi.SingleAsync(p => p.Id == pregledId);

        p1.Status = StatusPregleda.Overen;
        await db1.SaveChangesAsync();
        p2.Status = StatusPregleda.VracenNaDoradu;
        var drugi = () => db2.SaveChangesAsync();

        await drugi.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var s3 = factory.Services.CreateAsyncScope();
        (await s3.ServiceProvider.GetRequiredService<AppDbContext>().Pregledi.SingleAsync(p => p.Id == pregledId)).Status.Should().Be(StatusPregleda.Overen);
    }
}
