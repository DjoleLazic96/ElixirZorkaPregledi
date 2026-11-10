using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;

namespace TpmPregledi.Application.UnitTests.Common.Fakes;

/// <summary>Brojač u memoriji (pravi koristi Postgres <c>ON CONFLICT … RETURNING</c>).</summary>
public sealed class FakeBrojac : IBrojacService
{
    private readonly Dictionary<(string, int), int> _n = [];

    public Task<string> SledeciAsync(string vrsta, int godina, CancellationToken ct = default)
    {
        _n[(vrsta, godina)] = _n.GetValueOrDefault((vrsta, godina)) + 1;
        return Task.FromResult(Brojac.Formatiraj(vrsta, godina, _n[(vrsta, godina)]));
    }
}
