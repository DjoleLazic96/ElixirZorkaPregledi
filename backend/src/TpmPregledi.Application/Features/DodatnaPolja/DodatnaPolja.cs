using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.DodatnaPolja;

/// <summary>Dodatno polje uz aktivnost (R2.11, R2.13).</summary>
/// <param name="Id">ID.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Tip"><c>broj</c>, <c>merenje</c>, <c>dve_vrednosti</c>, <c>da_ne</c>.</param>
/// <param name="Jedinica">Jedinica.</param>
/// <param name="Oznake">Oznake vrednosti za <c>dve_vrednosti</c>.</param>
/// <param name="Prikaz"><c>uvek</c> ili <c>uz_neispravan</c>.</param>
/// <param name="Obavezno">Mora se popuniti kad se prikaže.</param>
public sealed record DodatnoPoljeDto(int Id, string Naziv, string Tip, string? Jedinica, IReadOnlyList<string> Oznake, string Prikaz, bool Obavezno);

/// <summary>Telo za novo polje (Admin).</summary>
/// <param name="Naziv">Naziv.</param>
/// <param name="Tip">Tip.</param>
/// <param name="Jedinica">Jedinica.</param>
/// <param name="Oznake">Oznake za <c>dve_vrednosti</c>.</param>
/// <param name="Prikaz">Kada se prikazuje.</param>
/// <param name="Obavezno">Obavezno.</param>
public sealed record DodatnoPoljeInput(string Naziv, string Tip, string? Jedinica, IReadOnlyList<string>? Oznake, string Prikaz, bool Obavezno);

/// <summary>Sva polja.</summary>
public sealed record GetDodatnaPoljaQuery : IRequest<IReadOnlyList<DodatnoPoljeDto>>;

/// <summary>Novo polje ovog tipa (R2.13); na stavke se dodaje kroz nacrt šablona (faza 10).</summary>
/// <param name="Input">Podaci.</param>
public sealed record KreirajDodatnoPoljeCommand(DodatnoPoljeInput Input) : IRequest<DodatnoPoljeDto>;

/// <summary>Oblik ulaza.</summary>
public sealed class KreirajDodatnoPoljeValidator : AbstractValidator<KreirajDodatnoPoljeCommand>
{
    private static readonly string[] Tipovi = ["broj", "merenje", "dve_vrednosti", "da_ne"];

    /// <summary>Pravila.</summary>
    public KreirajDodatnoPoljeValidator()
    {
        RuleFor(x => x.Input.Naziv).NotEmpty().WithMessage("Naziv polja je obavezan.")
            .MaximumLength(200).WithMessage("Naziv može imati najviše 200 karaktera.");
        RuleFor(x => x.Input.Tip).Must(t => Tipovi.Contains(t)).WithMessage("Tip mora biti broj, merenje, dve vrednosti ili da/ne.");
        RuleFor(x => x.Input.Prikaz).Must(p => p is "uvek" or "uz_neispravan").WithMessage("Prikaz mora biti „uvek“ ili „uz neispravan odgovor“.");
        RuleFor(x => x.Input.Jedinica).NotEmpty().When(x => x.Input.Tip is "merenje" or "dve_vrednosti")
            .WithMessage("Za merenje je jedinica obavezna.");
        RuleFor(x => x.Input.Oznake).Must(o => o is { Count: 2 } && o.All(s => !string.IsNullOrWhiteSpace(s)))
            .When(x => x.Input.Tip == "dve_vrednosti").WithMessage("Za dve vrednosti unesite obe oznake.");
    }
}

/// <summary>Katalog dodatnih polja.</summary>
public sealed class DodatnaPoljaHandler(IAppDbContext db) :
    IRequestHandler<GetDodatnaPoljaQuery, IReadOnlyList<DodatnoPoljeDto>>,
    IRequestHandler<KreirajDodatnoPoljeCommand, DodatnoPoljeDto>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DodatnoPoljeDto>> Handle(GetDodatnaPoljaQuery _, CancellationToken ct) =>
        (await db.DodatnaPolja.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct)).Select(Dto).ToList();

    /// <inheritdoc />
    public async Task<DodatnoPoljeDto> Handle(KreirajDodatnoPoljeCommand req, CancellationToken ct)
    {
        var naziv = req.Input.Naziv.Trim();
        if (await db.DodatnaPolja.AnyAsync(p => p.Naziv == naziv, ct))
            throw new BusinessRuleException($"Polje „{naziv}“ već postoji.");

        var p = new DodatnoPolje
        {
            Naziv = naziv,
            Tip = req.Input.Tip,
            Jedinica = string.IsNullOrWhiteSpace(req.Input.Jedinica) ? null : req.Input.Jedinica.Trim(),
            Oznake = req.Input.Tip == "dve_vrednosti" ? req.Input.Oznake!.Select(o => o.Trim()).ToArray() : [],
            Prikaz = req.Input.Prikaz,
            Obavezno = req.Input.Obavezno,
        };
        db.DodatnaPolja.Add(p);
        await db.SaveChangesAsync(ct);
        return Dto(p);
    }

    private static DodatnoPoljeDto Dto(DodatnoPolje p) => new(p.Id, p.Naziv, p.Tip, p.Jedinica, p.Oznake, p.Prikaz, p.Obavezno);
}
