using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Kategorije.Dtos;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Kategorije.Commands;

/// <summary>PRIMER feature-a — kopiraj ili obriši. Menja šifru, naziv i opis (cela izmena, PUT).</summary>
/// <param name="Id">PK kategorije.</param>
/// <param name="Sifra">Nova šifra.</param>
/// <param name="Naziv">Novi naziv.</param>
/// <param name="Opis">Novi opis (opciono).</param>
public sealed record IzmeniKategorijuCommand(int Id, string Sifra, string Naziv, string? Opis) : IRequest<KategorijaDto>;

/// <summary>Oblik ulaza — ista pravila kao pri kreiranju.</summary>
public sealed class IzmeniKategorijuValidator : AbstractValidator<IzmeniKategorijuCommand>
{
    /// <summary>Pravila validacije.</summary>
    public IzmeniKategorijuValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Identifikator kategorije nije ispravan.");
        RuleFor(x => x.Sifra)
            .NotEmpty().WithMessage("Šifra je obavezna.")
            .MaximumLength(32).WithMessage("Šifra može imati najviše 32 karaktera.");
        RuleFor(x => x.Naziv)
            .NotEmpty().WithMessage("Naziv je obavezan.")
            .MaximumLength(200).WithMessage("Naziv može imati najviše 200 karaktera.");
        RuleFor(x => x.Opis)
            .MaximumLength(1000).WithMessage("Opis može imati najviše 1000 karaktera.");
    }
}

/// <summary>Učitaj → proveri duplikat šifre među DRUGIM aktivnim → izmeni → <c>SaveChanges</c> (audit UPDATE).</summary>
public sealed class IzmeniKategorijuHandler(IAppDbContext db)
    : IRequestHandler<IzmeniKategorijuCommand, KategorijaDto>
{
    /// <inheritdoc />
    public async Task<KategorijaDto> Handle(IzmeniKategorijuCommand cmd, CancellationToken ct)
    {
        var kategorija = await db.Kategorije.FirstOrDefaultAsync(k => k.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Kategorija ne postoji.");

        var sifra = cmd.Sifra.Trim();

        // Provera važi i za neaktivnu kategoriju koja se menja: ako je kasnije aktiviraju,
        // ne sme da se sudari sa postojećom aktivnom.
        if (await db.Kategorije.AnyAsync(k => k.Id != cmd.Id && k.Sifra == sifra && k.Aktivan, ct))
            throw new BusinessRuleException(KreirajKategorijuHandler.PorukaDuplikat);

        kategorija.Sifra = sifra;
        kategorija.Naziv = cmd.Naziv.Trim();
        kategorija.Opis  = string.IsNullOrWhiteSpace(cmd.Opis) ? null : cmd.Opis.Trim();

        await db.SaveChangesAsync(ct);

        return KategorijaDto.Od(kategorija);
    }
}
