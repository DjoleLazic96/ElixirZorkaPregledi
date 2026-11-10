using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Aktivnosti;

/// <summary>Aktivnost iz kataloga.</summary>
/// <param name="Id">ID.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="OdgovorIspravan">Odgovor „u redu“.</param>
/// <param name="OdgovorNeispravan">Odgovor „kvar“.</param>
/// <param name="BrojStavki">U koliko stavki važećih verzija se koristi.</param>
/// <param name="ImaUputstvo">Uneto je uputstvo (tekst ili slike).</param>
public sealed record AktivnostDto(int Id, string Naziv, string OdgovorIspravan, string OdgovorNeispravan, int BrojStavki, bool ImaUputstvo = false);

/// <summary>Telo za kreiranje i izmenu aktivnosti.</summary>
/// <param name="Naziv">Naziv.</param>
/// <param name="OdgovorIspravan">Odgovor „u redu“.</param>
/// <param name="OdgovorNeispravan">Odgovor „kvar“.</param>
public sealed record AktivnostInput(string Naziv, string OdgovorIspravan, string OdgovorNeispravan);

/// <summary>Ceo katalog, po nazivu.</summary>
public sealed record GetAktivnostiQuery : IRequest<IReadOnlyList<AktivnostDto>>;

/// <summary>Nova aktivnost u katalogu (R2.3: naziv i par odgovora).</summary>
/// <param name="Input">Podaci.</param>
public sealed record KreirajAktivnostCommand(AktivnostInput Input) : IRequest<AktivnostDto>;

/// <summary>
/// Izmena aktivnosti. Naziv sme da se ispravi (pravopis), a par odgovora ne kad je aktivnost u objavljenoj
/// verziji: već dati odgovori bi promenili značenje. Za drugi par se pravi nova aktivnost i nacrt.
/// </summary>
/// <param name="Id">ID.</param>
/// <param name="Input">Podaci.</param>
public sealed record IzmeniAktivnostCommand(int Id, AktivnostInput Input) : IRequest<AktivnostDto>;

/// <summary>Oblik ulaza.</summary>
public sealed class AktivnostInputValidator : AbstractValidator<AktivnostInput>
{
    /// <summary>Pravila.</summary>
    public AktivnostInputValidator()
    {
        RuleFor(x => x.Naziv).NotEmpty().WithMessage("Naziv aktivnosti je obavezan.")
            .MaximumLength(300).WithMessage("Naziv može imati najviše 300 karaktera.");
        RuleFor(x => x.OdgovorIspravan).NotEmpty().WithMessage("Odgovor „ispravno“ je obavezan.")
            .MaximumLength(50).WithMessage("Odgovor može imati najviše 50 karaktera.");
        RuleFor(x => x.OdgovorNeispravan).NotEmpty().WithMessage("Odgovor „neispravno“ je obavezan.")
            .MaximumLength(50).WithMessage("Odgovor može imati najviše 50 karaktera.");
        RuleFor(x => x).Must(x => x.OdgovorIspravan.Trim() != x.OdgovorNeispravan.Trim())
            .WithMessage("Dva odgovora moraju biti različita.");
    }
}

/// <summary>Validator za kreiranje.</summary>
public sealed class KreirajAktivnostValidator : AbstractValidator<KreirajAktivnostCommand>
{
    /// <summary>Pravila.</summary>
    public KreirajAktivnostValidator() => RuleFor(x => x.Input).SetValidator(new AktivnostInputValidator());
}

/// <summary>Validator za izmenu.</summary>
public sealed class IzmeniAktivnostValidator : AbstractValidator<IzmeniAktivnostCommand>
{
    /// <summary>Pravila.</summary>
    public IzmeniAktivnostValidator() => RuleFor(x => x.Input).SetValidator(new AktivnostInputValidator());
}

/// <summary>Katalog aktivnosti.</summary>
public sealed class AktivnostiHandler(IAppDbContext db) :
    IRequestHandler<GetAktivnostiQuery, IReadOnlyList<AktivnostDto>>,
    IRequestHandler<KreirajAktivnostCommand, AktivnostDto>,
    IRequestHandler<IzmeniAktivnostCommand, AktivnostDto>
{
    /// <summary>Poruka kad se menja par odgovora aktivnosti koja je u objavljenoj verziji.</summary>
    public const string PorukaParUUpotrebi =
        "Aktivnost je u važećem šablonu, pa joj se odgovori ne mogu menjati. Napravite novu aktivnost i zamenite je kroz nacrt.";

    /// <inheritdoc />
    public async Task<IReadOnlyList<AktivnostDto>> Handle(GetAktivnostiQuery _, CancellationToken ct)
    {
        var broj = await BrojStavkiAsync(ct);
        var lista = await db.Aktivnosti.AsNoTracking().OrderBy(a => a.Naziv).ThenBy(a => a.OdgovorIspravan).ToListAsync(ct);
        return lista.Select(a => Dto(a, broj)).ToList();
    }

    /// <inheritdoc />
    public async Task<AktivnostDto> Handle(KreirajAktivnostCommand req, CancellationToken ct)
    {
        var (n, i, ne) = Ocisti(req.Input);
        await ZabraniDuplikatAsync(n, i, ne, null, ct);
        var a = new Aktivnost { Naziv = n, OdgovorIspravan = i, OdgovorNeispravan = ne };
        db.Aktivnosti.Add(a);
        await db.SaveChangesAsync(ct);
        return Dto(a, await BrojStavkiAsync(ct));
    }

    /// <inheritdoc />
    public async Task<AktivnostDto> Handle(IzmeniAktivnostCommand req, CancellationToken ct)
    {
        var a = await db.Aktivnosti.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
                ?? throw new NotFoundException("Aktivnost ne postoji.");
        var (n, i, ne) = Ocisti(req.Input);
        var broj = await BrojStavkiAsync(ct);
        if ((a.OdgovorIspravan != i || a.OdgovorNeispravan != ne) && broj.GetValueOrDefault(a.Id) > 0)
            throw new BusinessRuleException(PorukaParUUpotrebi);
        await ZabraniDuplikatAsync(n, i, ne, a.Id, ct);

        a.Naziv = n;
        a.OdgovorIspravan = i;
        a.OdgovorNeispravan = ne;
        await db.SaveChangesAsync(ct);
        return Dto(a, broj);
    }

    private static (string, string, string) Ocisti(AktivnostInput x) =>
        (x.Naziv.Trim(), x.OdgovorIspravan.Trim(), x.OdgovorNeispravan.Trim());

    private async Task ZabraniDuplikatAsync(string n, string i, string ne, int? id, CancellationToken ct)
    {
        if (await db.Aktivnosti.AnyAsync(a => a.Naziv == n && a.OdgovorIspravan == i && a.OdgovorNeispravan == ne && a.Id != id, ct))
            throw new BusinessRuleException($"Aktivnost „{n}“ sa odgovorima {i} / {ne} već postoji u katalogu.");
    }

    private async Task<Dictionary<int, int>> BrojStavkiAsync(CancellationToken ct) =>
        await db.SablonStavke.AsNoTracking()
            .Where(s => s.Verzija!.Status == StatusVerzije.Objavljena)
            .GroupBy(s => s.AktivnostId)
            .Select(g => new { g.Key, Broj = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Broj, ct);

    private static AktivnostDto Dto(Aktivnost a, Dictionary<int, int> broj) =>
        new(a.Id, a.Naziv, a.OdgovorIspravan, a.OdgovorNeispravan, broj.GetValueOrDefault(a.Id), a.UputstvoTekst is not null || a.UputstvoSlike.Length > 0);
}
