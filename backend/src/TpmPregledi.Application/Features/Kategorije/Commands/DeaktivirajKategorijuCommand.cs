using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Kategorije.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Kategorije.Commands;

/// <summary>PRIMER feature-a — kopiraj ili obriši. Soft delete (<c>aktivan = false</c>). Idempotentno.</summary>
/// <param name="Id">PK kategorije.</param>
public sealed record DeaktivirajKategorijuCommand(int Id) : IRequest<KategorijaDto>;

/// <summary>Interceptor beleži UPDATE sa poljem <c>Aktivan</c> — to je audit trag soft delete-a.</summary>
public sealed class DeaktivirajKategorijuHandler(IAppDbContext db)
    : IRequestHandler<DeaktivirajKategorijuCommand, KategorijaDto>
{
    /// <inheritdoc />
    public async Task<KategorijaDto> Handle(DeaktivirajKategorijuCommand cmd, CancellationToken ct)
    {
        var kategorija = await db.Kategorije.FirstOrDefaultAsync(k => k.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Kategorija ne postoji.");

        if (kategorija.Aktivan)
        {
            kategorija.Aktivan = false;
            await db.SaveChangesAsync(ct);
        }

        return KategorijaDto.Od(kategorija);
    }
}
