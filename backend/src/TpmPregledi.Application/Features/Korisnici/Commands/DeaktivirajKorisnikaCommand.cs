using TpmPregledi.Application.Common;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Korisnici.Commands;

/// <summary>
/// Soft delete korisnika (<c>POST /korisnici/{id}/deaktiviraj</c>). Korisnici se nikad ne brišu
/// fizički — audit log i <c>created_by</c> moraju da prežive. Idempotentno.
/// </summary>
/// <param name="Id">GUID korisnika.</param>
public sealed record DeaktivirajKorisnikaCommand(string Id) : IRequest<KorisnikDto>;

/// <summary>
/// Primenjuje guardove 2 i 4, gasi nalog i revokuje sve refresh tokene — deaktiviran korisnik
/// ne sme da produži sesiju (login i refresh ga već odbijaju, ovo zatvara i otvorene sesije).
/// </summary>
public sealed class DeaktivirajKorisnikaHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser,
    IJwtTokenService jwt)
    : IRequestHandler<DeaktivirajKorisnikaCommand, KorisnikDto>
{
    /// <inheritdoc />
    public async Task<KorisnikDto> Handle(DeaktivirajKorisnikaCommand req, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(req.Id)
            ?? throw new NotFoundException("Korisnik ne postoji.");

        var rola = await KorisnikHelpers.RolaAsync(userManager, user);

        if (!user.IsActive)
            return KorisnikDto.Od(user, rola);

        KorisnikGuards.ZabraniSamodeaktivaciju(user.Id, currentUser.UserId);

        var aktivniAdmini = await KorisnikHelpers.AktivniAdminIdsAsync(userManager);
        KorisnikGuards.ZabraniDeaktivacijuPoslednjegAdmina(
            user.Id, ciljaniJeAdmin: rola == Roles.Admin, ciljaniJeAktivan: true, aktivniAdmini);

        user.IsActive = false;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw IdentityGreske.KaoValidacija(result, "korisnik");

        await jwt.RevokeAllAsync(user.Id, ct);

        return KorisnikDto.Od(user, rola);
    }
}
