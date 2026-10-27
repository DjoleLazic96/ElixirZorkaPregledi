using Microsoft.AspNetCore.Identity;
using TpmPregledi.Application.Common.Exceptions;

namespace TpmPregledi.Application.Common;

/// <summary>
/// Prevodi neuspeo <see cref="IdentityResult"/> u <see cref="ValidationFailedException"/> (400)
/// sa greškama pod zadatim JSON poljem. Poruke su već srpske — <c>SrpskiIdentityErrorDescriber</c>
/// u Infrastructure sloju je registrovan u Identity-ju, pa handleri ne prevode ništa.
/// </summary>
public static class IdentityGreske
{
    /// <summary>Sve greške rezultata pod jednim poljem (npr. <c>"novaLozinka"</c>).</summary>
    public static ValidationFailedException KaoValidacija(IdentityResult result, string polje) =>
        new(new Dictionary<string, string[]>
        {
            [polje] = result.Errors.Select(e => e.Description).ToArray(),
        });
}
