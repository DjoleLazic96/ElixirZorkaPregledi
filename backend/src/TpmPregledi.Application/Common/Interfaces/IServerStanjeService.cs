using TpmPregledi.Application.Features.ServerStanje.Dtos;

namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Čita trenutno stanje servera (aplikacija, baza, host) u jedan <see cref="ServerStanjeDto"/>.
/// Handler je tanak wrapper; sva logika (hardver, Postgres meta upit, backup folder) je u
/// implementaciji u Infrastructure sloju.
/// </summary>
public interface IServerStanjeService
{
    /// <summary>Jedan vremenski koherentan snapshot; svaka grupa ima svoj try/catch.</summary>
    Task<ServerStanjeDto> DajStanjeAsync(CancellationToken ct);
}
