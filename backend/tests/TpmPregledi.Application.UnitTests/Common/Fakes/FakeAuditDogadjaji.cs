using TpmPregledi.Application.Common.Interfaces;

namespace TpmPregledi.Application.UnitTests.Common.Fakes;

/// <summary>Broji pozive po tipu događaja, bez pisanja u bazu.</summary>
public sealed class FakeAuditDogadjaji : IAuditDogadjaji
{
    public int Uspeh { get; private set; }
    public int Neuspeh { get; private set; }
    public int Odjava { get; private set; }
    public int Start { get; private set; }
    public string? PoslednjeNeuspehIme { get; private set; }
    public string? PoslednjiUspehUserId { get; private set; }

    public Task PrijavaUspehAsync(string userId, string userName, string? ip, CancellationToken ct = default)
    {
        Uspeh++;
        PoslednjiUspehUserId = userId;
        return Task.CompletedTask;
    }

    public Task PrijavaNeuspehAsync(string korisnickoIme, string? ip, CancellationToken ct = default)
    {
        Neuspeh++;
        PoslednjeNeuspehIme = korisnickoIme;
        return Task.CompletedTask;
    }

    public Task OdjavaAsync(string? userId, string? userName, string? ip, CancellationToken ct = default)
    {
        Odjava++;
        return Task.CompletedTask;
    }

    public Task StartAsync(string verzija, string gitSha, string okruzenje, CancellationToken ct = default)
    {
        Start++;
        return Task.CompletedTask;
    }
}
