using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Auth.Dtos;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Auth.Commands;

/// <summary>Prijava korisničkim imenom i lozinkom. Telo zahteva se vezuje direktno na komandu.</summary>
/// <param name="UserName">Korisničko ime.</param>
/// <param name="Password">Lozinka.</param>
public sealed record LoginCommand(string UserName, string Password) : IRequest<LoginResult>;

/// <summary>
/// Rezultat prijave: <see cref="Response"/> ide u telo odgovora, a sirovi refresh token u
/// HttpOnly cookie koji postavlja Api sloj.
/// </summary>
/// <param name="Response">Telo odgovora (access token, korisnik, role).</param>
/// <param name="RefreshToken">Sirovi refresh token za cookie.</param>
/// <param name="RefreshExpiresAt">UTC istek refresh tokena (= <c>Expires</c> cookie-ja).</param>
public sealed record LoginResult(LoginResponse Response, string RefreshToken, DateTime RefreshExpiresAt);

/// <summary>Oblik ulaza: oba polja obavezna. Ne otkriva ništa o nalogu.</summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    /// <summary>Pravila validacije.</summary>
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Korisničko ime je obavezno.").MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Lozinka je obavezna.");
    }
}

/// <summary>
/// Proverava kredencijale kroz ASP.NET Identity, izdaje par tokena i upisuje
/// <see cref="ApplicationUser.LastLoginAt"/>.
/// </summary>
/// <remarks>
/// <para>
/// Sve tačke neuspeha (nepostojeći ili deaktiviran korisnik, lockout, pogrešna lozinka) bacaju
/// <see cref="AuthenticationFailedException"/> sa <b>istom</b> porukom — ne otkrivamo da li
/// nalog postoji. <c>catch</c> beleži <c>LOGIN_FAIL</c> i ponovo baca isti izuzetak, pa 401
/// ostaje identičan.
/// </para>
/// <para>
/// Lockout (5 promašaja → 10 min) radi samo ako handler eksplicitno zove
/// <c>IsLockedOutAsync</c> / <c>AccessFailedAsync</c> / <c>ResetAccessFailedCountAsync</c> —
/// <c>CheckPasswordAsync</c> sam ne broji promašaje.
/// </para>
/// </remarks>
public sealed class LoginCommandHandler(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwt,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IAuditDogadjaji audit)
    : IRequestHandler<LoginCommand, LoginResult>
{
    /// <inheritdoc />
    public async Task<LoginResult> Handle(LoginCommand req, CancellationToken ct)
    {
        try
        {
            var user = await userManager.FindByNameAsync(req.UserName.Trim());

            if (user is null || !user.IsActive)
                throw new AuthenticationFailedException();

            if (await userManager.IsLockedOutAsync(user))
                throw new AuthenticationFailedException();

            if (!await userManager.CheckPasswordAsync(user, req.Password))
            {
                await userManager.AccessFailedAsync(user);
                throw new AuthenticationFailedException();
            }

            await userManager.ResetAccessFailedCountAsync(user);

            // Nema pozadinskog joba za čišćenje — uspešna prijava je trenutak kad se
            // korisnikovi istekli/revokovani tokeni brišu.
            await jwt.DeleteInactiveAsync(user.Id, ct);

            var roles = (await userManager.GetRolesAsync(user)).ToList();
            var pair  = await jwt.IssueTokenPairAsync(user, roles, currentUser.IpAddress, ct);

            // Interceptor ovde upisuje UPDATE red za korisnika („jedna prijava = dva reda“).
            user.LastLoginAt = clock.UtcNow;
            await userManager.UpdateAsync(user);

            var response = new LoginResponse(
                AccessToken: pair.AccessToken,
                ExpiresAt:   pair.AccessTokenExpiresAt,
                User:        new UserDto(user.Id, user.UserName ?? "", user.FullName, user.Email),
                Roles:       roles);

            await audit.PrijavaUspehAsync(user.Id, user.UserName ?? req.UserName, currentUser.IpAddress, ct);

            return new LoginResult(response, pair.RefreshToken, pair.RefreshTokenExpiresAt);
        }
        catch (AuthenticationFailedException)
        {
            await audit.PrijavaNeuspehAsync(req.UserName, currentUser.IpAddress, ct);
            throw;
        }
    }
}
