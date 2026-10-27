using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Auth.Commands;

/// <summary>
/// Korisnik menja sopstvenu lozinku (<c>POST /auth/change-password</c>). Telo se vezuje direktno
/// na komandu; korisnik dolazi iz <see cref="ICurrentUser"/>, nikad iz tela.
/// </summary>
/// <param name="CurrentPassword">Trenutna lozinka — dokaz da je pravi vlasnik naloga.</param>
/// <param name="NewPassword">Nova lozinka (politika: min 8, cifra, veliko i malo slovo).</param>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Unit>;

/// <summary>Oblik ulaza; snagu lozinke proverava Identity (srpske poruke iz <c>SrpskiIdentityErrorDescriber</c>).</summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>Pravila validacije.</summary>
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Trenutna lozinka je obavezna.");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Nova lozinka je obavezna.")
            .MinimumLength(8).WithMessage("Lozinka mora imati najmanje 8 karaktera.");
    }
}

/// <summary>
/// Menja lozinku kroz Identity i revokuje <b>sve</b> refresh tokene korisnika — svaka druga
/// sesija (drugi uređaj, ukraden cookie) pada; trenutni access token važi još najviše 15 min.
/// </summary>
/// <remarks>
/// Pogrešna trenutna lozinka → 400 pod <c>currentPassword</c>; politika nove lozinke → 400 pod
/// <c>newPassword</c>. Oba su greške oblika ulaza, ne poslovna pravila.
/// </remarks>
public sealed class ChangePasswordHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser,
    IJwtTokenService jwt)
    : IRequestHandler<ChangePasswordCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(ChangePasswordCommand req, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new AuthenticationFailedException();

        var user = await userManager.FindByIdAsync(currentUser.UserId)
                   ?? throw new AuthenticationFailedException();

        var result = await userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            var greske = new Dictionary<string, string[]>();

            var pogresnaTrenutna = result.Errors
                .Where(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                .Select(e => e.Description).ToArray();
            var politikaNove = result.Errors
                .Where(e => e.Code != nameof(IdentityErrorDescriber.PasswordMismatch))
                .Select(e => e.Description).ToArray();

            if (pogresnaTrenutna.Length > 0) greske["currentPassword"] = pogresnaTrenutna;
            if (politikaNove.Length > 0)     greske["newPassword"]     = politikaNove;

            throw new ValidationFailedException(greske);
        }

        await jwt.RevokeAllAsync(user.Id, ct);

        return Unit.Value;
    }
}
