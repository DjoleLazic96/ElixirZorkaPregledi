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
/// Admin kreira korisnika. Lozinka koju admin postavi odmah važi — nema „promeni pri prvoj
/// prijavi“ mehanizma (komplikuje tok, a admin lozinku svakako saopštava lično).
/// </summary>
/// <param name="KorisnickoIme">Korisničko ime.</param>
/// <param name="Ime">Puno ime.</param>
/// <param name="Email">E-mail (opciono).</param>
/// <param name="Lozinka">Početna lozinka.</param>
/// <param name="Rola">Jedina rola.</param>
public sealed record KreirajKorisnikaCommand(
    string KorisnickoIme,
    string Ime,
    string? Email,
    string Lozinka,
    string Rola)
    : IRequest<KorisnikDto>;

/// <summary>
/// Oblik ulaza. Snagu lozinke (cifra, veliko/malo) proverava Identity sa srpskim porukama;
/// ovde je samo brza provera dužine.
/// </summary>
public sealed class KreirajKorisnikaValidator : AbstractValidator<KreirajKorisnikaCommand>
{
    /// <summary>Pravila validacije.</summary>
    public KreirajKorisnikaValidator()
    {
        RuleFor(x => x.KorisnickoIme)
            .NotEmpty().WithMessage("Korisničko ime je obavezno.")
            .MinimumLength(3).WithMessage("Korisničko ime mora imati najmanje 3 karaktera.")
            .MaximumLength(256).WithMessage("Korisničko ime može imati najviše 256 karaktera.");

        RuleFor(x => x.Ime)
            .NotEmpty().WithMessage("Ime je obavezno.")
            .MaximumLength(200).WithMessage("Ime može imati najviše 200 karaktera.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("E-mail nije ispravan.")
            .MaximumLength(256).WithMessage("E-mail može imati najviše 256 karaktera.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Lozinka)
            .NotEmpty().WithMessage("Lozinka je obavezna.")
            .MinimumLength(8).WithMessage("Lozinka mora imati najmanje 8 karaktera.");

        RuleFor(x => x.Rola)
            .NotEmpty().WithMessage("Rola je obavezna.")
            .Must(r => Roles.All.Contains(r))
            .WithMessage($"Nepoznata rola. Dozvoljene: {string.Join(", ", Roles.All)}.");
    }
}

/// <summary>Kreira nalog kroz Identity i dodeljuje rolu; zauzeto korisničko ime → 409.</summary>
public sealed class KreirajKorisnikaHandler(
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider clock)
    : IRequestHandler<KreirajKorisnikaCommand, KorisnikDto>
{
    /// <inheritdoc />
    public async Task<KorisnikDto> Handle(KreirajKorisnikaCommand req, CancellationToken ct)
    {
        var korisnickoIme = req.KorisnickoIme.Trim();

        // Jedno pravilo za sve duplikate (blueprint 06 §6): „već postoji" je poslovno pravilo → 422 sa
        // gotovom rečenicom; 409 je rezervisan za konkurentnu izmenu istog zapisa.
        if (await userManager.FindByNameAsync(korisnickoIme) is not null)
            throw new BusinessRuleException($"Korisnik sa korisničkim imenom „{korisnickoIme}“ već postoji.");

        var user = new ApplicationUser
        {
            UserName       = korisnickoIme,
            Email          = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            EmailConfirmed = true,
            FullName       = req.Ime.Trim(),
            IsActive       = true,
            CreatedAt      = clock.UtcNow,
        };

        var createResult = await userManager.CreateAsync(user, req.Lozinka);
        if (!createResult.Succeeded)
        {
            // CreateAsync validira i lozinku (politika) i korisničko ime (dozvoljeni znakovi).
            // Razdvajamo po Identity kodu da greška imena ne stigne pod polje lozinke.
            var greske = new Dictionary<string, string[]>();
            var lozinka  = createResult.Errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                                              .Select(e => e.Description).ToArray();
            var korisnik = createResult.Errors.Where(e => !e.Code.StartsWith("Password", StringComparison.Ordinal))
                                              .Select(e => e.Description).ToArray();
            if (lozinka.Length  > 0) greske["lozinka"]       = lozinka;
            if (korisnik.Length > 0) greske["korisnickoIme"] = korisnik;
            throw new ValidationFailedException(greske);
        }

        var roleResult = await userManager.AddToRoleAsync(user, req.Rola);
        if (!roleResult.Succeeded)
        {
            // Bez kompenzacije bi ostao nalog bez role koji 409 provera iznad više ne dozvoljava
            // da se ponovo kreira — zato best-effort brišemo tek kreiranog korisnika.
            await userManager.DeleteAsync(user);
            throw IdentityGreske.KaoValidacija(roleResult, "rola");
        }

        return KorisnikDto.Od(user, req.Rola);
    }
}
