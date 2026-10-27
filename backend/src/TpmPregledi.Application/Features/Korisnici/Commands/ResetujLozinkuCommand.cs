using TpmPregledi.Application.Common;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Korisnici.Commands;

/// <summary>Admin postavlja novu lozinku korisniku bez znanja stare (nema „zaboravljena lozinka“ mejla).</summary>
/// <param name="Id">GUID korisnika.</param>
/// <param name="NovaLozinka">Nova lozinka.</param>
public sealed record ResetujLozinkuCommand(string Id, string NovaLozinka) : IRequest<Unit>;

/// <summary>Oblik ulaza; politiku lozinke proverava Identity.</summary>
public sealed class ResetujLozinkuValidator : AbstractValidator<ResetujLozinkuCommand>
{
    /// <summary>Pravila validacije.</summary>
    public ResetujLozinkuValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Identifikator korisnika je obavezan.");
        RuleFor(x => x.NovaLozinka)
            .NotEmpty().WithMessage("Nova lozinka je obavezna.")
            .MinimumLength(8).WithMessage("Lozinka mora imati najmanje 8 karaktera.");
    }
}

/// <summary>
/// Reset kroz <c>GeneratePasswordResetTokenAsync</c> → <c>ResetPasswordAsync</c> (prolazi sve
/// validatore lozinke, bez prolaznog stanja „bez lozinke“ kao remove+add). Posle uspeha revokuje
/// sve refresh tokene — ko god je imao sesiju sa starom lozinkom, ispada.
/// </summary>
public sealed class ResetujLozinkuHandler(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwt)
    : IRequestHandler<ResetujLozinkuCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(ResetujLozinkuCommand req, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(req.Id)
            ?? throw new NotFoundException("Korisnik ne postoji.");

        var token  = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, req.NovaLozinka);
        if (!result.Succeeded)
            throw IdentityGreske.KaoValidacija(result, "novaLozinka");

        await jwt.RevokeAllAsync(user.Id, ct);

        return Unit.Value;
    }
}
