using System.Text.Json;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Podesavanja;

/// <summary>Trenutna podešavanja (nepostojeći ključ = podrazumevana vrednost).</summary>
public sealed record GetPodesavanjaQuery : IRequest<PodesavanjaDto>;

/// <summary>Admin menja sva podešavanja odjednom (forma šalje ceo objekat).</summary>
/// <param name="Vrednosti">Nove vrednosti.</param>
public sealed record IzmeniPodesavanjaCommand(PodesavanjaDto Vrednosti) : IRequest<PodesavanjaDto>;

/// <summary>Opsezi su široki, ali sprečavaju očigledne greške u kucanju (npr. 0 ili 10 000 minuta).</summary>
public sealed class IzmeniPodesavanjaValidator : AbstractValidator<IzmeniPodesavanjaCommand>
{
    /// <summary>Pravila validacije.</summary>
    public IzmeniPodesavanjaValidator()
    {
        RuleFor(x => x.Vrednosti).NotNull().WithMessage("Podešavanja su obavezna.");
        RuleFor(x => x.Vrednosti.GranicaPauzeMin).InclusiveBetween(1, 240)
            .WithMessage("Granica pauze mora biti između 1 i 240 minuta.");
        RuleFor(x => x.Vrednosti.OdstupanjeSataMin).InclusiveBetween(1, 1440)
            .WithMessage("Dozvoljeno odstupanje sata mora biti između 1 i 1440 minuta.");
        RuleFor(x => x.Vrednosti.RokOvereSati).InclusiveBetween(1, 240)
            .WithMessage("Rok za overu mora biti između 1 i 240 sati.");
        RuleFor(x => x.Vrednosti.PonovljeniKvarBroj).InclusiveBetween(2, 50)
            .WithMessage("Broj ponavljanja kvara mora biti između 2 i 50.");
        RuleFor(x => x.Vrednosti.PonovljeniKvarDana).InclusiveBetween(1, 365)
            .WithMessage("Period za ponovljeni kvar mora biti između 1 i 365 dana.");
        RuleFor(x => x.Vrednosti.VanFunkcijeDana).InclusiveBetween(1, 365)
            .WithMessage("Granica „dugo van funkcije“ mora biti između 1 i 365 dana.");
    }
}

/// <summary>Čitanje i upis; ključ u bazi = ime svojstva DTO-a u camelCase-u.</summary>
public sealed class PodesavanjaHandler(IAppDbContext db)
    : IRequestHandler<GetPodesavanjaQuery, PodesavanjaDto>,
      IRequestHandler<IzmeniPodesavanjaCommand, PodesavanjaDto>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task<PodesavanjaDto> Handle(GetPodesavanjaQuery _, CancellationToken ct) => ProcitajAsync(ct);

    /// <inheritdoc />
    public async Task<PodesavanjaDto> Handle(IzmeniPodesavanjaCommand req, CancellationToken ct)
    {
        var novo = JsonSerializer.SerializeToElement(req.Vrednosti, Json);
        var postojeca = await db.Podesavanja.ToDictionaryAsync(p => p.Kljuc, ct);

        foreach (var svojstvo in novo.EnumerateObject())
        {
            var vrednost = svojstvo.Value.GetRawText();
            if (postojeca.TryGetValue(svojstvo.Name, out var red))
            {
                // Upis samo stvarne promene — audit log ne dobija redove za nepromenjena polja.
                if (red.Vrednost != vrednost) red.Vrednost = vrednost;
            }
            else
            {
                db.Podesavanja.Add(new Podesavanje { Kljuc = svojstvo.Name, Vrednost = vrednost });
            }
        }

        await db.SaveChangesAsync(ct);
        return await ProcitajAsync(ct);
    }

    private async Task<PodesavanjaDto> ProcitajAsync(CancellationToken ct)
    {
        var podrazumevana = JsonSerializer.SerializeToNode(PodesavanjaDto.Podrazumevana, Json)!.AsObject();
        var redovi = await db.Podesavanja.AsNoTracking().ToListAsync(ct);
        foreach (var r in redovi.Where(r => podrazumevana.ContainsKey(r.Kljuc)))
            podrazumevana[r.Kljuc] = System.Text.Json.Nodes.JsonNode.Parse(r.Vrednost);

        return podrazumevana.Deserialize<PodesavanjaDto>(Json)!;
    }
}
