using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Kategorije.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Kategorije.Queries;

/// <summary>PRIMER feature-a — kopiraj ili obriši. Jedna kategorija po ID-u (i neaktivna — admin je mora videti).</summary>
/// <param name="Id">PK kategorije.</param>
public sealed record GetKategorijaQuery(int Id) : IRequest<KategorijaDto>;

/// <summary>Čitanje: <c>AsNoTracking</c> + projekcija; nepostojeća → 404.</summary>
public sealed class GetKategorijaHandler(IAppDbContext db)
    : IRequestHandler<GetKategorijaQuery, KategorijaDto>
{
    /// <inheritdoc />
    public async Task<KategorijaDto> Handle(GetKategorijaQuery req, CancellationToken ct)
    {
        var dto = await db.Kategorije.AsNoTracking()
            .Where(k => k.Id == req.Id)
            .Select(k => new KategorijaDto(k.Id, k.Sifra, k.Naziv, k.Opis, k.Aktivan, k.CreatedAt, k.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException("Kategorija ne postoji.");
    }
}
