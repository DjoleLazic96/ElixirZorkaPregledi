using TpmPregledi.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Sekcije;

/// <summary>Sekcija za prikaz i filtere.</summary>
/// <param name="Id">PK.</param>
/// <param name="Sifra">Šifra sa obrasca („10“…„70“).</param>
/// <param name="Naziv">Naziv za prikaz.</param>
public sealed record SekcijaDto(int Id, string Sifra, string Naziv);

/// <summary>Sve sekcije, po redosledu sa obrazaca.</summary>
public sealed record GetSekcijeQuery : IRequest<IReadOnlyList<SekcijaDto>>;

/// <summary>Čita sekcije.</summary>
public sealed class GetSekcijeHandler(IAppDbContext db) : IRequestHandler<GetSekcijeQuery, IReadOnlyList<SekcijaDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SekcijaDto>> Handle(GetSekcijeQuery _, CancellationToken ct) =>
        await db.Sekcije.AsNoTracking()
            .OrderBy(s => s.Redosled)
            .Select(s => new SekcijaDto(s.Id, s.Sifra, s.Naziv))
            .ToListAsync(ct);
}
