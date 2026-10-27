using Microsoft.AspNetCore.Identity;

namespace TpmPregledi.Infrastructure.Identity;

/// <summary>
/// Srpske poruke za greške ASP.NET Identity-ja (politika lozinke, korisničko ime, e-mail…).
/// Registruje se kroz <c>AddErrorDescriber</c>, pa svaki <c>IdentityResult.Errors[].Description</c>
/// stiže na srpskom — handleri ne prevode ništa, samo prosleđuju. Kodovi grešaka (<c>e.Code</c>)
/// ostaju Identity-jevi, da handleri mogu da razvrstavaju po polju.
/// </summary>
public sealed class SrpskiIdentityErrorDescriber : IdentityErrorDescriber
{
    /// <inheritdoc />
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Došlo je do nepoznate greške." };

    /// <inheritdoc />
    public override IdentityError ConcurrencyFailure() =>
        new() { Code = nameof(ConcurrencyFailure), Description = "Zapis je u međuvremenu izmenjen. Osvežite stranicu." };

    /// <inheritdoc />
    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Trenutna lozinka nije ispravna." };

    /// <inheritdoc />
    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Token nije važeći." };

    /// <inheritdoc />
    public override IdentityError RecoveryCodeRedemptionFailed() =>
        new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "Kod za oporavak nije važeći." };

    /// <inheritdoc />
    public override IdentityError LoginAlreadyAssociated() =>
        new() { Code = nameof(LoginAlreadyAssociated), Description = "Korisnik sa ovom prijavom već postoji." };

    /// <inheritdoc />
    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = $"Korisničko ime „{userName}“ nije dozvoljeno. Dozvoljena su slova, cifre i znakovi - . _ @ +." };

    /// <inheritdoc />
    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = $"E-mail „{email}“ nije ispravan." };

    /// <inheritdoc />
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Korisničko ime „{userName}“ je već zauzeto." };

    /// <inheritdoc />
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = $"E-mail „{email}“ je već zauzet." };

    /// <inheritdoc />
    public override IdentityError InvalidRoleName(string? role) =>
        new() { Code = nameof(InvalidRoleName), Description = $"Rola „{role}“ nije dozvoljena." };

    /// <inheritdoc />
    public override IdentityError DuplicateRoleName(string role) =>
        new() { Code = nameof(DuplicateRoleName), Description = $"Rola „{role}“ već postoji." };

    /// <inheritdoc />
    public override IdentityError UserAlreadyHasPassword() =>
        new() { Code = nameof(UserAlreadyHasPassword), Description = "Korisnik već ima lozinku." };

    /// <inheritdoc />
    public override IdentityError UserLockoutNotEnabled() =>
        new() { Code = nameof(UserLockoutNotEnabled), Description = "Zaključavanje naloga nije uključeno za ovog korisnika." };

    /// <inheritdoc />
    public override IdentityError UserAlreadyInRole(string role) =>
        new() { Code = nameof(UserAlreadyInRole), Description = $"Korisnik već ima rolu „{role}“." };

    /// <inheritdoc />
    public override IdentityError UserNotInRole(string role) =>
        new() { Code = nameof(UserNotInRole), Description = $"Korisnik nema rolu „{role}“." };

    /// <inheritdoc />
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Lozinka mora imati najmanje {length} karaktera." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Lozinka mora sadržati najmanje {uniqueChars} različitih znakova." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Lozinka mora sadržati najmanje jedan specijalni znak." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Lozinka mora sadržati najmanje jednu cifru (0–9)." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Lozinka mora sadržati najmanje jedno malo slovo (a–z)." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Lozinka mora sadržati najmanje jedno veliko slovo (A–Z)." };
}
