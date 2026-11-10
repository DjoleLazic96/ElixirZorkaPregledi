using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Raspored;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TpmPregledi.Infrastructure.Poslovi;

/// <summary>
/// Prenos propuštenog DP-a (R3.3): jednom dnevno, posle „Kraja radnog dana“ (podešavanje, po beogradskom vremenu),
/// današnji DP koji nije započet ide na sledeći radni dan, a raspored se pomera. Idempotentno: poslednji obrađen dan
/// se pamti u <c>podesavanje._poslednjiPrenos</c>, a i sam prenos dira samo DP koji je i dalje na današnjem danu.
/// Bez Hangfire/Quartz (spec §5.2).
/// </summary>
public sealed class PrenosPropustenogDpPosao(IServiceScopeFactory scopes, ILogger<PrenosPropustenogDpPosao> log) : BackgroundService
{
    /// <summary>Ključ u <c>podesavanje</c> (nije deo <see cref="PodesavanjaDto"/>, pa ga forma ne prikazuje).</summary>
    public const string KljucPoslednji = "_poslednjiPrenos";

    private static readonly TimeZoneInfo Beograd = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Belgrade");

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var takt = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await takt.WaitForNextTickAsync(stop))
        {
            try
            {
                await JednomAsync(stop);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Posao ne sme da obori aplikaciju; sledeći takt pokušava ponovo.
                log.LogError(ex, "Prenos propuštenog DP-a nije uspeo.");
            }
        }
    }

    /// <summary>Jedan prolaz: ako je prošao kraj radnog dana i danas još nije obrađen, prenosi.</summary>
    public async Task JednomAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var podesavanja = await new PodesavanjaHandler(db).Handle(new GetPodesavanjaQuery(), ct);
        var lokalno = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, Beograd);
        if (TimeOnly.FromDateTime(lokalno) < podesavanja.KrajRadnogDana) return;

        var danas = clock.Today;
        var oznaka = $"\"{danas:yyyy-MM-dd}\"";
        var red = await db.Podesavanja.FirstOrDefaultAsync(p => p.Kljuc == KljucPoslednji, ct);
        if (red?.Vrednost == oznaka) return;

        var broj = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new PrenesiCommand(danas), ct);
        if (red is null) db.Podesavanja.Add(new Podesavanje { Kljuc = KljucPoslednji, Vrednost = oznaka });
        else red.Vrednost = oznaka;
        await db.SaveChangesAsync(ct);

        if (broj > 0) log.LogInformation("Prenos propuštenog DP-a za {Dan}: pomereno {Broj} pregleda.", danas, broj);
    }
}
