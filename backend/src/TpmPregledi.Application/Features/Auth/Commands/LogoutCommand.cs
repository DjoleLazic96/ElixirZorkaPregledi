using TpmPregledi.Application.Common.Interfaces;
using MediatR;

namespace TpmPregledi.Application.Features.Auth.Commands;

/// <summary>
/// Odjava — revokuje refresh token iz cookie-ja (access token klijent sam briše iz memorije).
/// Idempotentno: nepostojeći ili već revokovan token ne pravi grešku.
/// </summary>
/// <param name="RefreshToken">Sirovi refresh token iz cookie-ja (null ako cookie nije stigao).</param>
public sealed record LogoutCommand(string? RefreshToken) : IRequest<Unit>;

/// <summary>Revokuje token i beleži <c>LOGOUT</c> u audit log (best-effort).</summary>
public sealed class LogoutCommandHandler(
    IJwtTokenService jwt,
    ICurrentUser currentUser,
    IAuditDogadjaji audit)
    : IRequestHandler<LogoutCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(LogoutCommand req, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.RefreshToken))
            await jwt.RevokeAsync(req.RefreshToken, ct);

        await audit.OdjavaAsync(currentUser.UserId, currentUser.UserName, currentUser.IpAddress, ct);

        return Unit.Value;
    }
}
