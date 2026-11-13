using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Sekcije;

/// <summary>Sekcija za prikaz i filtere.</summary>
/// <param name="Id">PK.</param>
/// <param name="Sifra">Šifra sa obrasca („10“…„70“).</param>
/// <param name="Naziv">Naziv za prikaz.</param>
/// <param name="Aktivna">Neaktivna se ne nudi za nove uređaje (zamerka 33).</param>
public sealed record SekcijaDto(int Id, string Sifra, string Naziv, bool Aktivna = true);

/// <summary>Sve sekcije, po redosledu (i neaktivne — stari uređaji i pregledi pokazuju na njih).</summary>
public sealed record GetSekcijeQuery : IRequest<IReadOnlyList<SekcijaDto>>;

/// <summary>Nova sekcija (Admin, zamerka 33); ide na kraj redosleda.</summary>
/// <param name="Sifra">Šifra (npr. „80“).</param>
/// <param name="Naziv">Naziv; prazno = „Sekcija {šifra}“.</param>
public sealed record NovaSekcijaCommand(string Sifra, string? Naziv) : IRequest<SekcijaDto>;

/// <summary>Preimenovanje i aktivnost sekcije (Admin). Šifra se ne menja — po njoj se sekcija vidi na starim pregledima.</summary>
/// <param name="Id">Sekcija.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Aktivna">Aktivna.</param>
public sealed record IzmeniSekcijuCommand(int Id, string Naziv, bool Aktivna) : IRequest<SekcijaDto>;

/// <summary>Novi redosled sekcija (Admin): ID-jevi svih sekcija redom.</summary>
/// <param name="Ids">ID-jevi.</param>
public sealed record RedosledSekcijaCommand(IReadOnlyList<int> Ids) : IRequest<IReadOnlyList<SekcijaDto>>;

/// <summary>Oblik ulaza za novu sekciju.</summary>
public sealed class NovaSekcijaValidator : AbstractValidator<NovaSekcijaCommand>
{
    /// <summary>Pravila.</summary>
    public NovaSekcijaValidator()
    {
        RuleFor(x => x.Sifra).NotEmpty().WithMessage("Šifra je obavezna.").MaximumLength(8).WithMessage("Šifra može imati najviše 8 karaktera.");
        RuleFor(x => x.Naziv).MaximumLength(100).WithMessage("Naziv može imati najviše 100 karaktera.");
    }
}

/// <summary>Oblik ulaza za izmenu sekcije.</summary>
public sealed class IzmeniSekcijuValidator : AbstractValidator<IzmeniSekcijuCommand>
{
    /// <summary>Pravila.</summary>
    public IzmeniSekcijuValidator()
    {
        RuleFor(x => x.Naziv).NotEmpty().WithMessage("Naziv je obavezan.").MaximumLength(100).WithMessage("Naziv može imati najviše 100 karaktera.");
    }
}

/// <summary>Oblik ulaza za redosled.</summary>
public sealed class RedosledSekcijaValidator : AbstractValidator<RedosledSekcijaCommand>
{
    /// <summary>Pravila.</summary>
    public RedosledSekcijaValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage("Redosled je prazan.");
    }
}

/// <summary>Čita i uređuje sekcije.</summary>
public sealed class GetSekcijeHandler(IAppDbContext db) :
    IRequestHandler<GetSekcijeQuery, IReadOnlyList<SekcijaDto>>,
    IRequestHandler<NovaSekcijaCommand, SekcijaDto>,
    IRequestHandler<IzmeniSekcijuCommand, SekcijaDto>,
    IRequestHandler<RedosledSekcijaCommand, IReadOnlyList<SekcijaDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SekcijaDto>> Handle(GetSekcijeQuery _, CancellationToken ct) =>
        await db.Sekcije.AsNoTracking()
            .OrderBy(s => s.Redosled)
            .Select(s => new SekcijaDto(s.Id, s.Sifra, s.Naziv, s.Aktivna))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<SekcijaDto> Handle(NovaSekcijaCommand req, CancellationToken ct)
    {
        var sifra = req.Sifra.Trim();
        if (await db.Sekcije.AnyAsync(s => s.Sifra == sifra, ct)) throw new BusinessRuleException($"Sekcija {sifra} već postoji.");
        var s = new Sekcija
        {
            Sifra = sifra, Naziv = string.IsNullOrWhiteSpace(req.Naziv) ? $"Sekcija {sifra}" : req.Naziv.Trim(),
            Redosled = (await db.Sekcije.MaxAsync(x => (int?)x.Redosled, ct) ?? 0) + 1,
        };
        db.Sekcije.Add(s);
        await db.SaveChangesAsync(ct);
        return new SekcijaDto(s.Id, s.Sifra, s.Naziv, s.Aktivna);
    }

    /// <inheritdoc />
    public async Task<SekcijaDto> Handle(IzmeniSekcijuCommand req, CancellationToken ct)
    {
        var s = await db.Sekcije.FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Sekcija ne postoji.");
        s.Naziv = req.Naziv.Trim();
        s.Aktivna = req.Aktivna;
        await db.SaveChangesAsync(ct);
        return new SekcijaDto(s.Id, s.Sifra, s.Naziv, s.Aktivna);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SekcijaDto>> Handle(RedosledSekcijaCommand req, CancellationToken ct)
    {
        var sve = await db.Sekcije.ToListAsync(ct);
        if (req.Ids.Count != sve.Count || req.Ids.Distinct().Count() != sve.Count || sve.Any(s => !req.Ids.Contains(s.Id)))
            throw new BusinessRuleException("Redosled mora da sadrži svaku sekciju tačno jednom.");
        for (var i = 0; i < req.Ids.Count; i++) sve.First(s => s.Id == req.Ids[i]).Redosled = i + 1;
        await db.SaveChangesAsync(ct);
        return await Handle(new GetSekcijeQuery(), ct);
    }
}
