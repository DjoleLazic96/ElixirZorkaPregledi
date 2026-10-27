using TpmPregledi.Application.Common;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Korisnici.Commands;

/// <summary>
/// Admin menja ime, e-mail i rolu korisnika. Korisničko ime je nepromenljivo; status ima
/// zasebne akcije (deaktiviraj/aktiviraj); lozinka zaseban reset.
/// </summary>
/// <param name="Id">GUID korisnika.</param>
/// <param name="Ime">Novo puno ime.</param>
/// <param name="Email">Novi e-mail (opciono).</param>
/// <param name="Rola">Nova jedina rola.</param>
public sealed record IzmeniKorisnikaCommand(
    string Id,
    string Ime,
    string? Email,
    string Rola)
    : IRequest<KorisnikDto>;

/// <summary>Oblik ulaza.</summary>
public sealed class IzmeniKorisnikaValidator : AbstractValidator<IzmeniKorisnikaCommand>
{
    /// <summary>Pravila validacije.</summary>
    public IzmeniKorisnikaValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Identifikator korisnika je obavezan.");

        RuleFor(x => x.Ime)
            .NotEmpty().WithMessage("Ime je obavezno.")
            .MaximumLength(200).WithMessage("Ime može imati najviše 200 karaktera.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("E-mail nije ispravan.")
            .MaximumLength(256).WithMessage("E-mail može imati najviše 256 karaktera.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Rola)
            .NotEmpty().WithMessage("Rola je obavezna.")
            .Must(r => Roles.All.Contains(r))
            .WithMessage($"Nepoznata rola. Dozvoljene: {string.Join(", ", Roles.All)}.");
    }
}

/// <summary>
/// Primenjuje guardove 1 i 3, menja podatke, sinhronizuje rolu i — ako se rola promenila —
/// revokuje sve refresh tokene (stari JWT sa starom rolom važi još najviše 15 min).
/// </summary>
public sealed class IzmeniKorisnikaHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser,
    IJwtTokenService jwt)
    : IRequestHandler<IzmeniKorisnikaCommand, KorisnikDto>
{
    /// <inheritdoc />
    public async Task<KorisnikDto> Handle(IzmeniKorisnikaCommand req, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(req.Id)
            ?? throw new NotFoundException("Korisnik ne postoji.");

        var trenutneRole = await userManager.GetRolesAsync(user);
        var staraRola    = trenutneRole.FirstOrDefault() ?? string.Empty;
        var menjaRolu    = staraRola != req.Rola;

        if (menjaRolu)
        {
            KorisnikGuards.ZabraniPromenuSopstveneRole(user.Id, currentUser.UserId);

            if (staraRola == Roles.Admin)
            {
                var aktivniAdmini = await KorisnikHelpers.AktivniAdminIdsAsync(userManager);
                KorisnikGuards.ZabraniSkidanjeAdminaPoslednjem(
                    user.Id, ciljaniJeAdmin: true, ciljaniJeAktivan: user.IsActive, aktivniAdmini);
            }
        }

        user.FullName = req.Ime.Trim();
        user.Email    = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw IdentityGreske.KaoValidacija(updateResult, "korisnik");

        if (menjaRolu)
        {
            // PRVO dodaj novu, TEK ONDA skini staru — da korisnik ni na tren ne ostane bez role
            // ako drugi korak pukne.
            var addResult = await userManager.AddToRoleAsync(user, req.Rola);
            if (!addResult.Succeeded)
                throw IdentityGreske.KaoValidacija(addResult, "rola");

            var zaSkidanje = trenutneRole.Where(r => r != req.Rola).ToList();
            if (zaSkidanje.Count > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, zaSkidanje);
                if (!removeResult.Succeeded)
                    throw IdentityGreske.KaoValidacija(removeResult, "rola");
            }

            await jwt.RevokeAllAsync(user.Id, ct);
        }

        return KorisnikDto.Od(user, req.Rola);
    }
}
