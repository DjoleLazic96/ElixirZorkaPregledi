using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Application.Features.Raspored;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Aktivnosti;

/// <summary>Uputstvo aktivnosti (R2.4): tekst i do 3 referentne slike; važi u svim šablonima, izmena ne pravi novu verziju.</summary>
/// <param name="AktivnostId">Aktivnost.</param>
/// <param name="Aktivnost">Naziv.</param>
/// <param name="Tekst">Tekst (null = nije uneto).</param>
/// <param name="Slike">ID-jevi slika.</param>
/// <param name="Azurirao">Ko je poslednji menjao.</param>
/// <param name="AzuriranoAt">Kada.</param>
public sealed record UputstvoDto(int AktivnostId, string Aktivnost, string? Tekst, IReadOnlyList<long> Slike, string? Azurirao, DateTime? AzuriranoAt);

/// <summary>Uputstvo aktivnosti.</summary>
/// <param name="Id">Aktivnost.</param>
public sealed record GetUputstvoQuery(int Id) : IRequest<UputstvoDto>;

/// <summary>Izmena teksta uputstva (prazno = briše tekst).</summary>
/// <param name="Id">Aktivnost.</param>
/// <param name="Tekst">Tekst.</param>
public sealed record IzmeniUputstvoCommand(int Id, string? Tekst) : IRequest<UputstvoDto>;

/// <summary>Dodaje sliku uputstvu (najviše 3).</summary>
public sealed record DodajSlikuUputstvaCommand(int Id, byte[] Sadrzaj, string Mime) : IRequest<UputstvoDto>;

/// <summary>Uklanja sliku iz uputstva (fajl ostaje, kao i kod ostalih fajlova).</summary>
/// <param name="Id">Aktivnost.</param>
/// <param name="FajlId">Slika.</param>
public sealed record UkloniSlikuUputstvaCommand(int Id, long FajlId) : IRequest<UputstvoDto>;

/// <summary>Slika uputstva.</summary>
/// <param name="Id">Aktivnost.</param>
/// <param name="FajlId">Slika.</param>
public sealed record GetSlikaUputstvaQuery(int Id, long FajlId) : IRequest<(Stream Sadrzaj, string Mime)>;

/// <summary>Tekst najviše 4000 karaktera.</summary>
public sealed class IzmeniUputstvoValidator : AbstractValidator<IzmeniUputstvoCommand>
{
    /// <summary>Pravila.</summary>
    public IzmeniUputstvoValidator() => RuleFor(x => x.Tekst).MaximumLength(4000).WithMessage("Uputstvo može imati najviše 4000 karaktera.");
}

/// <summary>Slika: tip i veličina kao kod slika uz stavku.</summary>
public sealed class DodajSlikuUputstvaValidator : AbstractValidator<DodajSlikuUputstvaCommand>
{
    /// <summary>Pravila.</summary>
    public DodajSlikuUputstvaValidator()
    {
        RuleFor(x => x.Mime).Must(m => FotografijeHandler.Tipovi.Contains(m)).WithMessage("Dozvoljene su slike JPG, PNG ili WebP.");
        RuleFor(x => x.Sadrzaj).Must(s => s.Length is > 0 and <= 3 * 1024 * 1024).WithMessage("Slika mora biti manja od 3 MB.");
    }
}

/// <summary>Uputstva za aktivnosti.</summary>
public sealed class UputstvaHandler(IAppDbContext db, IFajlSkladiste skladiste, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<GetUputstvoQuery, UputstvoDto>,
    IRequestHandler<IzmeniUputstvoCommand, UputstvoDto>,
    IRequestHandler<DodajSlikuUputstvaCommand, UputstvoDto>,
    IRequestHandler<UkloniSlikuUputstvaCommand, UputstvoDto>,
    IRequestHandler<GetSlikaUputstvaQuery, (Stream Sadrzaj, string Mime)>
{
    /// <summary>Najviše slika (R2.4).</summary>
    public const int NajviseSlika = 3;

    /// <inheritdoc />
    public async Task<UputstvoDto> Handle(GetUputstvoQuery req, CancellationToken ct)
    {
        var a = await db.Aktivnosti.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Aktivnost ne postoji.");
        var k = await RasporedKontekst.UcitajAsync(db, ct);
        return new UputstvoDto(a.Id, a.Naziv, a.UputstvoTekst, a.UputstvoSlike, a.UputstvoAzuriraoId is null ? null : k.Ime(a.UputstvoAzuriraoId), a.UputstvoAzuriranoAt);
    }

    /// <inheritdoc />
    public async Task<UputstvoDto> Handle(IzmeniUputstvoCommand req, CancellationToken ct)
    {
        var a = await UcitajAsync(req.Id, ct);
        a.UputstvoTekst = string.IsNullOrWhiteSpace(req.Tekst) ? null : req.Tekst.Trim();
        await SacuvajAsync(a, ct);
        return await Handle(new GetUputstvoQuery(a.Id), ct);
    }

    /// <inheritdoc />
    public async Task<UputstvoDto> Handle(DodajSlikuUputstvaCommand req, CancellationToken ct)
    {
        var a = await UcitajAsync(req.Id, ct);
        if (a.UputstvoSlike.Length >= NajviseSlika) throw new BusinessRuleException("Uputstvo može imati najviše 3 slike.");
        var fajl = await skladiste.SacuvajAsync(req.Sadrzaj, req.Mime, ct);
        db.Fajlovi.Add(fajl);
        await db.SaveChangesAsync(ct);
        a.UputstvoSlike = [.. a.UputstvoSlike, fajl.Id];
        await SacuvajAsync(a, ct);
        return await Handle(new GetUputstvoQuery(a.Id), ct);
    }

    /// <inheritdoc />
    public async Task<UputstvoDto> Handle(UkloniSlikuUputstvaCommand req, CancellationToken ct)
    {
        var a = await UcitajAsync(req.Id, ct);
        if (!a.UputstvoSlike.Contains(req.FajlId)) throw new NotFoundException("Slika nije u ovom uputstvu.");
        a.UputstvoSlike = a.UputstvoSlike.Where(x => x != req.FajlId).ToArray();
        await SacuvajAsync(a, ct);
        return await Handle(new GetUputstvoQuery(a.Id), ct);
    }

    /// <inheritdoc />
    public async Task<(Stream Sadrzaj, string Mime)> Handle(GetSlikaUputstvaQuery req, CancellationToken ct)
    {
        var a = await db.Aktivnosti.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Aktivnost ne postoji.");
        if (!a.UputstvoSlike.Contains(req.FajlId)) throw new NotFoundException("Slika nije u ovom uputstvu.");
        var fajl = await db.Fajlovi.AsNoTracking().FirstAsync(x => x.Id == req.FajlId, ct);
        return (skladiste.Otvori(fajl), fajl.Mime);
    }

    private async Task<Domain.Entities.Aktivnost> UcitajAsync(int id, CancellationToken ct) =>
        await db.Aktivnosti.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Aktivnost ne postoji.");

    private async Task SacuvajAsync(Domain.Entities.Aktivnost a, CancellationToken ct)
    {
        a.UputstvoAzuriraoId = currentUser.UserId;
        a.UputstvoAzuriranoAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
