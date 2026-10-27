using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Auth.Dtos;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Auth.Commands;

/// <summary>
/// Razmena važećeg refresh tokena za nov par (rotacija). Nepoznat, revokovan ili istekao
/// token → 401; deaktiviran korisnik → 401 (ista generička poruka).
/// </summary>
/// <param name="RefreshToken">Sirovi refresh token iz cookie-ja.</param>
public sealed record RefreshCommand(string RefreshToken) : IRequest<LoginResult>;

/// <summary>Rotira token kroz <see cref="IJwtTokenService"/> i vraća isti oblik kao prijava.</summary>
public sealed class RefreshCommandHandler(
    IJwtTokenService jwt,
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser)
    : IRequestHandler<RefreshCommand, LoginResult>
{
    /// <inheritdoc />
    public async Task<LoginResult> Handle(RefreshCommand req, CancellationToken ct)
    {
        var result = await jwt.RotateAsync(req.RefreshToken, currentUser.IpAddress, ct);
        if (result is null)
            throw new AuthenticationFailedException("Sesija je istekla. Prijavite se ponovo.");

        var user = result.User;
        if (!user.IsActive)
            throw new AuthenticationFailedException();

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var pair  = result.Tokens;

        var response = new LoginResponse(
            AccessToken: pair.AccessToken,
            ExpiresAt:   pair.AccessTokenExpiresAt,
            User:        new UserDto(user.Id, user.UserName ?? "", user.FullName, user.Email),
            Roles:       roles);

        return new LoginResult(response, pair.RefreshToken, pair.RefreshTokenExpiresAt);
    }
}
