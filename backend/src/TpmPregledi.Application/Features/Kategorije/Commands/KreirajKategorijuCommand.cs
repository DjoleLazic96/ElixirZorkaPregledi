using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Kategorije.Dtos;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Kategorije.Commands;

/// <summary>PRIMER feature-a — kopiraj ili obriši. Kreira kategoriju.</summary>
/// <param name="Sifra">Šifra.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Opis">Opis (opciono).</param>
public sealed record KreirajKategorijuCommand(string Sifra, string Naziv, string? Opis) : IRequest<KategorijaDto>;

/// <summary>Validator = oblik ulaza (obavezno, dužina). Poslovna pravila su u handleru.</summary>
public sealed class KreirajKategorijuValidator : AbstractValidator<KreirajKategorijuCommand>
{
    /// <summary>Pravila validacije.</summary>
    public KreirajKategorijuValidator()
    {
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

/// <summary>Handler = poslovno pravilo: šifra je jedinstvena među aktivnim kategorijama (422).</summary>
public sealed class KreirajKategorijuHandler(IAppDbContext db)
    : IRequestHandler<KreirajKategorijuCommand, KategorijaDto>
{
    /// <summary>Poruka poslovnog pravila — ista u Kreiraj, Izmeni i Aktiviraj.</summary>
    public const string PorukaDuplikat = "Kategorija sa ovom šifrom već postoji.";

    /// <inheritdoc />
    public async Task<KategorijaDto> Handle(KreirajKategorijuCommand cmd, CancellationToken ct)
    {
        var sifra = cmd.Sifra.Trim();

        if (await db.Kategorije.AnyAsync(k => k.Sifra == sifra && k.Aktivan, ct))
            throw new BusinessRuleException(PorukaDuplikat);

        var kategorija = new Kategorija
        {
            Sifra = sifra,
            Naziv = cmd.Naziv.Trim(),
            Opis  = string.IsNullOrWhiteSpace(cmd.Opis) ? null : cmd.Opis.Trim(),
        };

        db.Kategorije.Add(kategorija);
        await db.SaveChangesAsync(ct);   // interceptor: audit CREATE + CreatedAt/By

        return KategorijaDto.Od(kategorija);
    }
}
