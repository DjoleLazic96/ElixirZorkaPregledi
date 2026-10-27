using TpmPregledi.Application.Features.Korisnici.Dtos;
using TpmPregledi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Korisnici.Queries;

/// <summary>
/// Lista korisnika za admin sekciju, sortirana po korisničkom imenu. Nepaginirana — korisnika
/// je desetine; paginirati tek ako klijent ima stotine.
/// </summary>
/// <param name="Aktivan">Filter po statusu; null = svi (admin ekran ima „Prikaži i neaktivne“).</param>
public sealed record GetKorisniciQuery(bool? Aktivan = null) : IRequest<IReadOnlyList<KorisnikDto>>;

/// <summary>Čita korisnike i njihovu (jednu) rolu.</summary>
public sealed class GetKorisniciHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<GetKorisniciQuery, IReadOnlyList<KorisnikDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<KorisnikDto>> Handle(GetKorisniciQuery req, CancellationToken ct)
    {
        var upit = userManager.Users.AsNoTracking();
        if (req.Aktivan is { } aktivan)
            upit = upit.Where(u => u.IsActive == aktivan);

        var users = await upit.OrderBy(u => u.UserName).ToListAsync(ct);

        // Identity nema bulk čitanje rola; populacija je mala, pa je N+1 prihvatljivo.
        var result = new List<KorisnikDto>(users.Count);
        foreach (var u in users)
            result.Add(KorisnikDto.Od(u, await KorisnikHelpers.RolaAsync(userManager, u)));

        return result;
    }
}
