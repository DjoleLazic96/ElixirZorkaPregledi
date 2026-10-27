using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Kategorije.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Kategorije.Commands;

/// <summary>PRIMER feature-a — kopiraj ili obriši. Vraća deaktiviranu kategoriju u upotrebu. Idempotentno.</summary>
/// <param name="Id">PK kategorije.</param>
public sealed record AktivirajKategorijuCommand(int Id) : IRequest<KategorijaDto>;

/// <summary>
/// Pre aktivacije proverava da u međuvremenu nije kreirana aktivna kategorija sa istom šifrom —
/// inače bi filtrirani jedinstveni indeks pukao sa 500 umesto čitljivog 422.
/// </summary>
public sealed class AktivirajKategorijuHandler(IAppDbContext db)
    : IRequestHandler<AktivirajKategorijuCommand, KategorijaDto>
{
    /// <inheritdoc />
    public async Task<KategorijaDto> Handle(AktivirajKategorijuCommand cmd, CancellationToken ct)
    {
        var kategorija = await db.Kategorije.FirstOrDefaultAsync(k => k.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Kategorija ne postoji.");

        if (kategorija.Aktivan)
            return KategorijaDto.Od(kategorija);

        if (await db.Kategorije.AnyAsync(k => k.Id != cmd.Id && k.Sifra == kategorija.Sifra && k.Aktivan, ct))
            throw new BusinessRuleException(KreirajKategorijuHandler.PorukaDuplikat);

        kategorija.Aktivan = true;
        await db.SaveChangesAsync(ct);

        return KategorijaDto.Od(kategorija);
    }
}
