using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Application.Features.Korisnici.Commands;

/// <summary>
/// Admin štiklira prava korisnika (R1.6). Šalje se ceo novi skup. Revokuje refresh tokene, pa nova
/// prava važe najkasnije kad istekne trenutni access token (15 min) — isto kao promena role.
/// </summary>
/// <param name="Id">GUID korisnika.</param>
/// <param name="Prava">Novi skup prava.</param>
public sealed record PostaviPravaCommand(string Id, IReadOnlyList<string> Prava) : IRequest<KorisnikDto>;

/// <summary>Oblik ulaza: samo poznata prava.</summary>
public sealed class PostaviPravaValidator : AbstractValidator<PostaviPravaCommand>
{
    /// <summary>Pravila validacije.</summary>
    public PostaviPravaValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Identifikator korisnika je obavezan.");
        RuleFor(x => x.Prava).NotNull().WithMessage("Spisak prava je obavezan.");
        RuleForEach(x => x.Prava)
            .Must(p => Prava.Sva.Contains(p))
            .WithMessage("Nepoznato pravo: {PropertyValue}.");
    }
}

/// <summary>Primenjuje guardove (samo Inženjer popunjava; ne oduzima se sebi upravljanje korisnicima) i menja skup.</summary>
public sealed class PostaviPravaHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser,
    IAppDbContext db,
    IJwtTokenService jwt)
    : IRequestHandler<PostaviPravaCommand, KorisnikDto>
{
    /// <inheritdoc />
    public async Task<KorisnikDto> Handle(PostaviPravaCommand req, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(req.Id)
            ?? throw new NotFoundException("Korisnik ne postoji.");
        var rola = await KorisnikHelpers.RolaAsync(userManager, user);

        KorisnikGuards.PopunjavanjeSamoInzenjer(rola, req.Prava);
        KorisnikGuards.ZabraniSamooduzimanjeKorisnika(user.Id, currentUser.UserId, req.Prava);

        await KorisnikPrava.ZameniAsync(db, user.Id, req.Prava, ct);
        await db.SaveChangesAsync(ct);
        await jwt.RevokeAllAsync(user.Id, ct);

        return KorisnikDto.Od(user, rola, await KorisnikPrava.ProcitajAsync(db, user.Id, ct));
    }
}
