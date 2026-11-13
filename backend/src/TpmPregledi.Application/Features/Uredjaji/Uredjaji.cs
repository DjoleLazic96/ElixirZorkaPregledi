using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Uredjaji;

/// <summary>Uređaj u registru.</summary>
/// <param name="Id">Stalni ID.</param>
/// <param name="Naziv">Naziv sa obrasca.</param>
/// <param name="Oznaka">Oznaka sa obrasca (može biti prazna).</param>
/// <param name="SekcijaId">Sekcija.</param>
/// <param name="Sekcija">Šifra sekcije („10“…).</param>
/// <param name="Vrsta">Vrsta uređaja.</param>
/// <param name="Status"><c>aktivan</c> ili <c>van_upotrebe</c>.</param>
/// <param name="Sabloni">Tipovi šablona čija važeća verzija sadrži uređaj (R8.4 „u kojim je šablonima“).</param>
public sealed record UredjajDto(
    int Id, string Naziv, string Oznaka, int SekcijaId, string Sekcija, string Vrsta, string Status,
    IReadOnlyList<string> Sabloni);

/// <summary>Telo za kreiranje i izmenu uređaja.</summary>
/// <param name="Naziv">Naziv.</param>
/// <param name="Oznaka">Oznaka (prazno dozvoljeno).</param>
/// <param name="SekcijaId">Sekcija.</param>
/// <param name="Vrsta">Vrsta.</param>
public sealed record UredjajInput(string Naziv, string? Oznaka, int SekcijaId, string Vrsta);

/// <summary>Registar (R2.2): filteri po sekciji i statusu, pretraga po nazivu i oznaci.</summary>
/// <param name="SekcijaId">Filter po sekciji.</param>
/// <param name="Status">Filter po statusu.</param>
/// <param name="Pretraga">Deo naziva ili oznake.</param>
/// <param name="SamoBezSablona">Samo uređaji koji nisu ni u jednoj važećoj verziji (R2.10).</param>
public sealed record GetUredjajiQuery(int? SekcijaId = null, string? Status = null, string? Pretraga = null, bool SamoBezSablona = false)
    : IRequest<IReadOnlyList<UredjajDto>>;

/// <summary>Jedan uređaj.</summary>
/// <param name="Id">ID.</param>
public sealed record GetUredjajQuery(int Id) : IRequest<UredjajDto>;

/// <summary>Novi uređaj (Direktor, Admin — R2.3).</summary>
/// <param name="Input">Podaci.</param>
public sealed record KreirajUredjajCommand(UredjajInput Input) : IRequest<UredjajDto>;

/// <summary>Izmena uređaja; ID ostaje, pa istorija ide za uređajem.</summary>
/// <param name="Id">ID.</param>
/// <param name="Input">Podaci.</param>
public sealed record IzmeniUredjajCommand(int Id, UredjajInput Input) : IRequest<UredjajDto>;

/// <summary>„Van upotrebe“ umesto brisanja, i vraćanje u upotrebu (R2.3).</summary>
/// <param name="Id">ID.</param>
/// <param name="Status">Novi status.</param>
public sealed record PostaviStatusUredjajaCommand(int Id, string Status) : IRequest<UredjajDto>;

/// <summary>Oblik ulaza za kreiranje.</summary>
public sealed class KreirajUredjajValidator : AbstractValidator<KreirajUredjajCommand>
{
    /// <summary>Pravila.</summary>
    public KreirajUredjajValidator() => RuleFor(x => x.Input).SetValidator(new UredjajInputValidator());
}

/// <summary>Oblik ulaza za izmenu.</summary>
public sealed class IzmeniUredjajValidator : AbstractValidator<IzmeniUredjajCommand>
{
    /// <summary>Pravila.</summary>
    public IzmeniUredjajValidator() => RuleFor(x => x.Input).SetValidator(new UredjajInputValidator());
}

/// <summary>Zajednička pravila za naziv, oznaku, sekciju i vrstu.</summary>
public sealed class UredjajInputValidator : AbstractValidator<UredjajInput>
{
    /// <summary>Pravila.</summary>
    public UredjajInputValidator()
    {
        RuleFor(x => x.Naziv).NotEmpty().WithMessage("Naziv uređaja je obavezan.")
            .MaximumLength(200).WithMessage("Naziv može imati najviše 200 karaktera.");
        RuleFor(x => x.Oznaka).MaximumLength(100).WithMessage("Oznaka može imati najviše 100 karaktera.");
        RuleFor(x => x.SekcijaId).GreaterThan(0).WithMessage("Sekcija je obavezna.");
        RuleFor(x => x.Vrsta).NotEmpty().WithMessage("Vrsta je obavezna.")
            .MaximumLength(100).WithMessage("Vrsta može imati najviše 100 karaktera.");
    }
}

/// <summary>Oblik ulaza za status.</summary>
public sealed class PostaviStatusUredjajaValidator : AbstractValidator<PostaviStatusUredjajaCommand>
{
    /// <summary>Pravila.</summary>
    public PostaviStatusUredjajaValidator() =>
        RuleFor(x => x.Status).Must(s => s is StatusUredjaja.Aktivan or StatusUredjaja.VanUpotrebe)
            .WithMessage("Status mora biti „aktivan“ ili „van_upotrebe“.");
}

/// <summary>Svi upiti i komande registra.</summary>
public sealed class UredjajiHandler(IAppDbContext db) :
    IRequestHandler<GetUredjajiQuery, IReadOnlyList<UredjajDto>>,
    IRequestHandler<GetUredjajQuery, UredjajDto>,
    IRequestHandler<KreirajUredjajCommand, UredjajDto>,
    IRequestHandler<IzmeniUredjajCommand, UredjajDto>,
    IRequestHandler<PostaviStatusUredjajaCommand, UredjajDto>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UredjajDto>> Handle(GetUredjajiQuery req, CancellationToken ct)
    {
        var upit = db.Uredjaji.AsNoTracking().Include(u => u.Sekcija).AsQueryable();
        if (req.SekcijaId is { } s) upit = upit.Where(u => u.SekcijaId == s);
        if (!string.IsNullOrWhiteSpace(req.Status)) upit = upit.Where(u => u.Status == req.Status);
        if (!string.IsNullOrWhiteSpace(req.Pretraga))
        {
            var t = req.Pretraga.Trim().ToLower();
            upit = upit.Where(u => u.Naziv.ToLower().Contains(t) || u.Oznaka.ToLower().Contains(t));
        }

        var lista = await upit.OrderBy(u => u.Sekcija!.Redosled).ThenBy(u => u.Oznaka).ThenBy(u => u.Naziv).ToListAsync(ct);
        var uSablonima = await SabloniPoUredjajuAsync(ct);
        if (req.SamoBezSablona) lista = lista.Where(u => !uSablonima.ContainsKey(u.Id)).ToList();

        return lista.Select(u => Dto(u, uSablonima)).ToList();
    }

    /// <inheritdoc />
    public async Task<UredjajDto> Handle(GetUredjajQuery req, CancellationToken ct) =>
        Dto(await UcitajAsync(req.Id, ct), await SabloniPoUredjajuAsync(ct));

    /// <inheritdoc />
    public async Task<UredjajDto> Handle(KreirajUredjajCommand req, CancellationToken ct)
    {
        var (naziv, oznaka) = await ProveriAsync(req.Input, null, ct);
        var u = new Uredjaj { Naziv = naziv, Oznaka = oznaka, SekcijaId = req.Input.SekcijaId, Vrsta = req.Input.Vrsta.Trim() };
        db.Uredjaji.Add(u);
        await db.SaveChangesAsync(ct);
        return await Handle(new GetUredjajQuery(u.Id), ct);
    }

    /// <inheritdoc />
    public async Task<UredjajDto> Handle(IzmeniUredjajCommand req, CancellationToken ct)
    {
        var u = await UcitajAsync(req.Id, ct);
        var (naziv, oznaka) = await ProveriAsync(req.Input, req.Id, ct);
        u.Naziv = naziv;
        u.Oznaka = oznaka;
        u.SekcijaId = req.Input.SekcijaId;
        u.Vrsta = req.Input.Vrsta.Trim();
        await db.SaveChangesAsync(ct);
        return await Handle(new GetUredjajQuery(u.Id), ct);
    }

    /// <inheritdoc />
    public async Task<UredjajDto> Handle(PostaviStatusUredjajaCommand req, CancellationToken ct)
    {
        var u = await UcitajAsync(req.Id, ct);
        u.Status = req.Status;
        await db.SaveChangesAsync(ct);
        return await Handle(new GetUredjajQuery(u.Id), ct);
    }

    private async Task<Uredjaj> UcitajAsync(int id, CancellationToken ct) =>
        await db.Uredjaji.Include(u => u.Sekcija).FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NotFoundException("Uređaj ne postoji.");

    /// <summary>Sekcija mora postojati (i biti aktivna za nov uređaj); par (naziv, oznaka) mora biti jedinstven (422 sa gotovom rečenicom).</summary>
    private async Task<(string Naziv, string Oznaka)> ProveriAsync(UredjajInput input, int? id, CancellationToken ct)
    {
        var naziv = input.Naziv.Trim();
        var oznaka = (input.Oznaka ?? string.Empty).Trim();
        var sekcija = await db.Sekcije.AsNoTracking().FirstOrDefaultAsync(s => s.Id == input.SekcijaId, ct)
            ?? throw new BusinessRuleException("Izabrana sekcija ne postoji.");
        // Neaktivna sekcija (zamerka 33) ostaje postojećim uređajima, ali se ne bira za nov uređaj ni pri premeštanju.
        if (!sekcija.Aktivna && !await db.Uredjaji.AnyAsync(u => u.Id == id && u.SekcijaId == sekcija.Id, ct))
            throw new BusinessRuleException($"Sekcija {sekcija.Sifra} nije aktivna.");
        if (await db.Uredjaji.AnyAsync(u => u.Naziv == naziv && u.Oznaka == oznaka && u.Id != id, ct))
            throw new BusinessRuleException(
                $"Uređaj „{new Uredjaj { Naziv = naziv, Oznaka = oznaka }.Prikaz}“ već postoji u registru.");
        return (naziv, oznaka);
    }

    /// <summary>Uređaj → tipovi šablona čija objavljena verzija ga sadrži.</summary>
    private async Task<Dictionary<int, List<string>>> SabloniPoUredjajuAsync(CancellationToken ct)
    {
        var parovi = await db.SablonStavke.AsNoTracking()
            .Where(s => s.Verzija!.Status == StatusVerzije.Objavljena)
            .Select(s => new { s.UredjajId, s.Verzija!.Sablon!.Tip })
            .Distinct()
            .ToListAsync(ct);
        return parovi.GroupBy(p => p.UredjajId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Tip).OrderBy(t => Sablon.Tipovi.ToList().IndexOf(t)).ToList());
    }

    private static UredjajDto Dto(Uredjaj u, Dictionary<int, List<string>> uSablonima) =>
        new(u.Id, u.Naziv, u.Oznaka, u.SekcijaId, u.Sekcija?.Sifra ?? string.Empty, u.Vrsta, u.Status,
            uSablonima.GetValueOrDefault(u.Id) ?? []);
}
