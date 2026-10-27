using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.ServerStanje.Dtos;
using MediatR;

namespace TpmPregledi.Application.Features.ServerStanje.Queries;

/// <summary>
/// Jedan snapshot stanja servera (Admin). Bez parametara. Handler je tanak wrapper nad
/// <see cref="IServerStanjeService"/>; autorizacija je na endpointu.
/// </summary>
public sealed record GetServerStanjeQuery : IRequest<ServerStanjeDto>;

/// <summary>Delegira na <see cref="IServerStanjeService"/>.</summary>
public sealed class GetServerStanjeHandler(IServerStanjeService svc)
    : IRequestHandler<GetServerStanjeQuery, ServerStanjeDto>
{
    /// <inheritdoc />
    public Task<ServerStanjeDto> Handle(GetServerStanjeQuery req, CancellationToken ct)
        => svc.DajStanjeAsync(ct);
}
