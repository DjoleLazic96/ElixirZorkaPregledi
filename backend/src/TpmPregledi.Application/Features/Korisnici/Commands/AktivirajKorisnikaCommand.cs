using TpmPregledi.Application.Common;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Korisnici.Commands;

/// <summary>Ponovo aktivira deaktiviranog korisnika (<c>POST /korisnici/{id}/aktiviraj</c>). Idempotentno.</summary>
/// <param name="Id">GUID korisnika.</param>
public sealed record AktivirajKorisnikaCommand(string Id) : IRequest<KorisnikDto>;

/// <summary>Nema guardova — aktivacija ne može da zaključa sistem.</summary>
public sealed class AktivirajKorisnikaHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<AktivirajKorisnikaCommand, KorisnikDto>
{
    /// <inheritdoc />
    public async Task<KorisnikDto> Handle(AktivirajKorisnikaCommand req, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(req.Id)
            ?? throw new NotFoundException("Korisnik ne postoji.");

        var rola = await KorisnikHelpers.RolaAsync(userManager, user);

        if (user.IsActive)
            return KorisnikDto.Od(user, rola);

        user.IsActive = true;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw IdentityGreske.KaoValidacija(result, "korisnik");

        return KorisnikDto.Od(user, rola);
    }
}
