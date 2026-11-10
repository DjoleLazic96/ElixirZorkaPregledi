using FluentAssertions;
using TpmPregledi.Application.Features.Podesavanja;
using TpmPregledi.Application.Features.Sekcije;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Domain.Entities;

namespace TpmPregledi.Application.UnitTests.Features;

/// <summary>Brojevi, podešavanja i sekcije iz faze 01.</summary>
public class OsnovaTests
{
    [Theory]
    [InlineData("DP1", 2026, 142, "DP1-2026-0142")]
    [InlineData("A", 2026, 31, "A-2026-0031")]
    [InlineData("K", 2027, 1, "K-2027-0001")]
    [InlineData("PZ4", 2026, 12345, "PZ4-2026-12345")]
    public void Broj_ima_format_vrsta_godina_redni(string vrsta, int godina, int redni, string ocekivano)
        => Brojac.Formatiraj(vrsta, godina, redni).Should().Be(ocekivano);

    [Fact]
    public async Task Prazna_baza_vraca_podrazumevana_podesavanja()
    {
        await using var h = IdentityHarness.New();
        var sut = new PodesavanjaHandler(h.Db);

        (await sut.Handle(new GetPodesavanjaQuery(), default)).Should().Be(PodesavanjaDto.Podrazumevana);
    }

    [Fact]
    public async Task Izmena_podesavanja_se_cita_nazad_i_ne_dira_ostale_vrednosti()
    {
        await using var h = IdentityHarness.New();
        var sut  = new PodesavanjaHandler(h.Db);
        var novo = PodesavanjaDto.Podrazumevana with { GranicaPauzeMin = 15, UpozorenjeDp = new TimeOnly(12, 30), DpNaDanPz = true };

        await sut.Handle(new IzmeniPodesavanjaCommand(novo), default);

        (await sut.Handle(new GetPodesavanjaQuery(), default)).Should().Be(novo);
    }

    [Fact]
    public void Validator_odbija_nulu_za_granicu_pauze()
        => new IzmeniPodesavanjaValidator()
            .Validate(new IzmeniPodesavanjaCommand(PodesavanjaDto.Podrazumevana with { GranicaPauzeMin = 0 }))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Sekcije_su_po_redosledu()
    {
        await using var h = IdentityHarness.New();
        h.Db.Sekcije.AddRange(new Sekcija { Id = 2, Sifra = "20", Naziv = "Sekcija 20", Redosled = 2 },
                              new Sekcija { Id = 1, Sifra = "10", Naziv = "Sekcija 10", Redosled = 1 });
        await h.Db.SaveChangesAsync();

        var lista = await new GetSekcijeHandler(h.Db).Handle(new GetSekcijeQuery(), default);

        lista.Select(s => s.Sifra).Should().Equal("10", "20");
    }
}
