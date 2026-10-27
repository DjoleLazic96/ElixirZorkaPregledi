using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Kategorije.Commands;
using TpmPregledi.Application.Features.Kategorije.Queries;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Domain.Entities;

namespace TpmPregledi.Application.UnitTests.Kategorije;

/// <summary>PRIMER feature-a: poslovno pravilo duplikata šifre (422) na kreiranju, izmeni i aktivaciji; 404; lista.</summary>
public class KategorijeHandlerTests
{
    private const string Duplikat = "Kategorija sa ovom šifrom već postoji.";

    [Fact]
    public async Task Kreiranje_sa_postojecom_aktivnom_sifrom_vraca_422()
    {
        await using var db = InMemoryDb.New();
        db.Kategorije.Add(new Kategorija { Sifra = "K1", Naziv = "Prva" });
        await db.SaveChangesAsync();
        var handler = new KreirajKategorijuHandler(db);

        var act = () => handler.Handle(new KreirajKategorijuCommand("K1", "Duplikat", null), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().ContainSingle()
            .Which.Should().Be(Duplikat);
    }

    [Fact]
    public async Task Kreiranje_sa_sifrom_deaktivirane_kategorije_prolazi()
    {
        await using var db = InMemoryDb.New();
        db.Kategorije.Add(new Kategorija { Sifra = "K1", Naziv = "Stara", Aktivan = false });
        await db.SaveChangesAsync();

        var dto = await new KreirajKategorijuHandler(db).Handle(new KreirajKategorijuCommand(" K1 ", " Nova ", "  "), default);

        dto.Sifra.Should().Be("K1", "trim");
        dto.Naziv.Should().Be("Nova");
        dto.Opis.Should().BeNull("prazan opis postaje null");
        dto.Aktivan.Should().BeTrue();
        (await db.Kategorije.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Izmena_na_sifru_druge_aktivne_vraca_422_a_ista_sifra_prolazi()
    {
        await using var db = InMemoryDb.New();
        var k1 = new Kategorija { Sifra = "K1", Naziv = "Prva" };
        var k2 = new Kategorija { Sifra = "K2", Naziv = "Druga" };
        db.Kategorije.AddRange(k1, k2);
        await db.SaveChangesAsync();
        var handler = new IzmeniKategorijuHandler(db);

        await FluentActions.Awaiting(() => handler.Handle(new IzmeniKategorijuCommand(k2.Id, "K1", "Druga", null), default))
            .Should().ThrowAsync<BusinessRuleException>();

        var dto = await handler.Handle(new IzmeniKategorijuCommand(k2.Id, "K2", "Druga izmenjena", null), default);
        dto.Naziv.Should().Be("Druga izmenjena");
    }

    [Fact]
    public async Task Aktivacija_kad_vec_postoji_aktivna_sa_istom_sifrom_vraca_422()
    {
        await using var db = InMemoryDb.New();
        var stara = new Kategorija { Sifra = "K1", Naziv = "Stara", Aktivan = false };
        db.Kategorije.AddRange(stara, new Kategorija { Sifra = "K1", Naziv = "Nova" });
        await db.SaveChangesAsync();

        var act = () => new AktivirajKategorijuHandler(db).Handle(new AktivirajKategorijuCommand(stara.Id), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Greske.Should().Equal(Duplikat);
    }

    [Fact]
    public async Task Deaktivacija_i_aktivacija_su_idempotentne()
    {
        await using var db = InMemoryDb.New();
        var k = new Kategorija { Sifra = "K1", Naziv = "Prva" };
        db.Kategorije.Add(k);
        await db.SaveChangesAsync();

        (await new DeaktivirajKategorijuHandler(db).Handle(new DeaktivirajKategorijuCommand(k.Id), default)).Aktivan.Should().BeFalse();
        (await new DeaktivirajKategorijuHandler(db).Handle(new DeaktivirajKategorijuCommand(k.Id), default)).Aktivan.Should().BeFalse();
        (await new AktivirajKategorijuHandler(db).Handle(new AktivirajKategorijuCommand(k.Id), default)).Aktivan.Should().BeTrue();
    }

    [Fact]
    public async Task Nepostojeca_kategorija_vraca_404()
    {
        await using var db = InMemoryDb.New();

        await FluentActions.Awaiting(() => new GetKategorijaHandler(db).Handle(new GetKategorijaQuery(999), default))
            .Should().ThrowAsync<NotFoundException>().WithMessage("Kategorija ne postoji.");
        await FluentActions.Awaiting(() => new IzmeniKategorijuHandler(db).Handle(new IzmeniKategorijuCommand(999, "X", "Y", null), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Lista_filtrira_pretrazuje_i_sortira_sa_whitelist_om()
    {
        await using var db = InMemoryDb.New();
        db.Kategorije.AddRange(
            new Kategorija { Sifra = "B", Naziv = "Beta" },
            new Kategorija { Sifra = "A", Naziv = "Alfa" },
            new Kategorija { Sifra = "C", Naziv = "Gama", Aktivan = false });
        await db.SaveChangesAsync();
        var sut = new GetKategorijeHandler(db, NullLogger<GetKategorijeHandler>.Instance);

        var sve = await sut.Handle(new GetKategorijeQuery(), default);
        sve.Ukupno.Should().Be(3);
        sve.Stavke.Select(k => k.Sifra).Should().Equal("A", "B", "C");

        var aktivne = await sut.Handle(new GetKategorijeQuery(Aktivan: true, Sort: "naziv", Smer: "desc"), default);
        aktivne.Stavke.Select(k => k.Naziv).Should().Equal("Beta", "Alfa");

        var pretraga = await sut.Handle(new GetKategorijeQuery(Pretraga: "gam"), default);
        pretraga.Stavke.Should().ContainSingle().Which.Sifra.Should().Be("C");

        var nepoznatSort = await sut.Handle(new GetKategorijeQuery(Sort: "nepostoji"), default);
        nepoznatSort.Stavke.Select(k => k.Sifra).Should().Equal("A", "B", "C");
    }
}
