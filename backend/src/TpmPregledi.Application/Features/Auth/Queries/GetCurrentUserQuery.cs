using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Auth.Dtos;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Auth.Queries;

/// <summary>
/// Trenutno prijavljen korisnik (<c>GET /auth/me</c>) — frontend ga zove pri pokretanju da
/// popuni header/sidebar bez ponovne prijave.
/// </summary>
public sealed record GetCurrentUserQuery : IRequest<CurrentUserResponse>;

/// <summary>Podaci o trenutnom korisniku za UI.</summary>
/// <param name="User">Osnovni podaci.</param>
/// <param name="Roles">Role (uvek tačno jedna; ugovor ostaje niz).</param>
public sealed record CurrentUserResponse(UserDto User, IReadOnlyList<string> Roles);

/// <summary>Čita korisnika iz baze po ID-u iz JWT-a (podaci mogu da se promene posle izdavanja tokena).</summary>
public sealed class GetCurrentUserHandler(
    ICurrentUser currentUser,
    IAppDbContext db,
    UserManager<ApplicationUser> userManager)
    : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    /// <inheritdoc />
    public async Task<CurrentUserResponse> Handle(GetCurrentUserQuery _, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new AuthenticationFailedException();

        var user = await db.Users
                       .AsNoTracking()
                       .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
                   ?? throw new AuthenticationFailedException();

        var roles = (await userManager.GetRolesAsync(user)).ToList();

        return new CurrentUserResponse(
            new UserDto(user.Id, user.UserName ?? "", user.FullName, user.Email),
            roles);
    }
}
