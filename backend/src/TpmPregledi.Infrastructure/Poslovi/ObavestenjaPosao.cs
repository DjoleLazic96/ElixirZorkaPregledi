using TpmPregledi.Application.Features.Obavestenja;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TpmPregledi.Infrastructure.Poslovi;

/// <summary>
/// Obaveštenja u pozadini (spec §5.2): svakih 15 sekundi isporuka push-a i mejla, a jednom u minutu zakazane provere
/// (nije započet, rok overe, rok akcije, dugo van funkcije, rezime, raspored za sledeću nedelju). Provere su idempotentne
/// kroz ključ obaveštenja, pa restart ili dva prolaza u istom minutu ne šalju isto dva puta.
/// </summary>
public sealed class ObavestenjaPosao(IServiceScopeFactory scopes, ILogger<ObavestenjaPosao> log) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var takt = new PeriodicTimer(TimeSpan.FromSeconds(15));
        var poslednjaProvera = DateTime.MinValue;
        while (await takt.WaitForNextTickAsync(stop))
        {
            if (DateTime.UtcNow - poslednjaProvera >= TimeSpan.FromMinutes(1))
            {
                poslednjaProvera = DateTime.UtcNow;
                await PokreniAsync(new PokreniProvereCommand(), "Zakazane provere", stop);
            }
            await PokreniAsync(new IsporuciObavestenjaCommand(), "Isporuka obaveštenja", stop);
        }
    }

    private async Task PokreniAsync(IRequest<int> zahtev, string naziv, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var broj = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(zahtev, ct);
            if (broj > 0) log.LogInformation("{Naziv}: {Broj}.", naziv, broj);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Posao ne sme da obori aplikaciju; sledeći takt pokušava ponovo.
            log.LogError(ex, "{Naziv} nije uspela.", naziv);
        }
    }
}
