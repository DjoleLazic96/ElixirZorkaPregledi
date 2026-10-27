using FluentAssertions;
using TpmPregledi.Application.Features.Audit;
using TpmPregledi.Application.Features.Audit.Queries;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Infrastructure.Persistence;

namespace TpmPregledi.Application.UnitTests.Audit;

/// <summary>
/// GetAuditLogQuery: sort created_at DESC + id DESC, filteri (korisnik contains/case-insensitive,
/// entitet/akcija tačno, od/do inkluzivno sa normalizacijom Kind-a), paginacija i srpske labele.
/// </summary>
public class GetAuditLogQueryTests
{
    private static readonly DateTime T0 = new(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);

    private static AuditLog Red(string action, string entity, string? user, DateTime created) =>
        new() { Action = action, Entity = entity, UserName = user, UserId = user, EntityId = "1", CreatedAt = created, IpAddress = "10.0.0.1" };

    private static async Task<AppDbContext> SaRedovima(params AuditLog[] rows)
    {
        var ctx = InMemoryDb.New();
        ctx.AuditLog.AddRange(rows);
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Sortira_najnovije_prvo()
    {
        await using var ctx = await SaRedovima(
            Red("CREATE", "Kategorija", "a", T0.AddHours(1)),
            Red("UPDATE", "Kategorija", "b", T0.AddHours(3)),
            Red("DELETE", "Kategorija", "c", T0.AddHours(2)));

        var res = await new GetAuditLogHandler(ctx).Handle(new GetAuditLogQuery(), default);

        res.Ukupno.Should().Be(3);
        res.Stavke.Select(s => s.Korisnik).Should().ContainInOrder("b", "c", "a");
    }

    [Fact]
    public async Task Isto_vreme_se_razbija_po_Id_opadajuce()
    {
        await using var ctx = await SaRedovima(
            Red("CREATE", "Kategorija", "prvi", T0),
            Red("CREATE", "Kategorija", "drugi", T0));

        var res = await new GetAuditLogHandler(ctx).Handle(new GetAuditLogQuery(), default);

        res.Stavke.Select(s => s.Korisnik).Should().ContainInOrder("drugi", "prvi");
    }

    [Fact]
    public async Task Filter_korisnik_je_contains_i_case_insensitive()
    {
        await using var ctx = await SaRedovima(
            Red("LOGIN", "Auth", "Admin",       T0.AddHours(1)),
            Red("LOGIN", "Auth", "adminModric", T0.AddHours(2)),
            Red("LOGIN", "Auth", "pera",        T0.AddHours(3)));

        var res = await new GetAuditLogHandler(ctx).Handle(new GetAuditLogQuery(Korisnik: "ADMIN"), default);

        res.Ukupno.Should().Be(2);
    }

    [Fact]
    public async Task Filter_entitet_i_akcija_su_tacno_poklapanje()
    {
        await using var ctx = await SaRedovima(
            Red("CREATE", "Kategorija", "a", T0.AddHours(1)),
            Red("UPDATE", "Kategorija", "b", T0.AddHours(2)),
            Red("LOGIN",  "Auth",       "c", T0.AddHours(3)));
        var sut = new GetAuditLogHandler(ctx);

        (await sut.Handle(new GetAuditLogQuery(Entitet: "Kategorija"), default)).Ukupno.Should().Be(2);
        (await sut.Handle(new GetAuditLogQuery(Akcija: "LOGIN"), default)).Stavke.Single().Entitet.Should().Be("Auth");
    }

    [Fact]
    public async Task Filter_od_do_je_inkluzivan_i_normalizuje_Unspecified_na_UTC()
    {
        await using var ctx = await SaRedovima(
            Red("CREATE", "Kategorija", "a", T0),
            Red("CREATE", "Kategorija", "b", T0.AddDays(1)),
            Red("CREATE", "Kategorija", "c", T0.AddDays(2)),
            Red("CREATE", "Kategorija", "d", T0.AddDays(3)));

        // Zone-less query string ("?od=2026-07-21T00:00:00") stiže kao Kind=Unspecified.
        var od  = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Unspecified);
        var doo = new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Unspecified);  // = tačno „c“ → inkluzivno

        var res = await new GetAuditLogHandler(ctx).Handle(new GetAuditLogQuery(Od: od, Do: doo), default);

        res.Stavke.Select(s => s.Korisnik).Should().BeEquivalentTo(["b", "c"]);
    }

    [Fact]
    public async Task Paginacija_vraca_stranicu_ukupno_i_normalizovanu_velicinu()
    {
        var rows = Enumerable.Range(0, 25).Select(i => Red("CREATE", "Kategorija", $"k{i:D2}", T0.AddMinutes(i))).ToArray();
        await using var ctx = await SaRedovima(rows);
        var sut = new GetAuditLogHandler(ctx);

        var str2 = await sut.Handle(new GetAuditLogQuery(Stranica: 2, Velicina: 10), default);
        str2.Ukupno.Should().Be(25);
        str2.Stranica.Should().Be(2);
        str2.Velicina.Should().Be(10);
        str2.Stavke.Should().HaveCount(10);
        str2.Stavke.First().Korisnik.Should().Be("k14", "najnoviji je k24, druga stranica počinje od k14");

        (await sut.Handle(new GetAuditLogQuery(Velicina: 5000), default)).Velicina.Should().Be(200);
        (await sut.Handle(new GetAuditLogQuery(Stranica: 0, Velicina: 0), default)).Should()
            .Match<Application.Common.Pagination.StranicaDto<Application.Features.Audit.Dtos.AuditRedDto>>(
                p => p.Stranica == 1 && p.Velicina == 50);
    }

    [Fact]
    public async Task Mapira_srpske_labele_i_sirovi_json()
    {
        await using var ctx = await SaRedovima(
            new AuditLog { Action = "UPDATE", Entity = "Kategorija", UserName = "a", CreatedAt = T0.AddHours(2),
                           OldValues = "{\"Naziv\":\"stari\"}", NewValues = "{\"Naziv\":\"novi\"}" },
            Red("LOGIN", "Auth",   "b", T0.AddHours(1)),
            Red("START", "Sistem", "system", T0));

        var res = await new GetAuditLogHandler(ctx).Handle(new GetAuditLogQuery(), default);

        var kat = res.Stavke.Single(s => s.Entitet == "Kategorija");
        kat.EntitetLabela.Should().Be("Kategorija");
        kat.StareVrednosti.Should().Be("{\"Naziv\":\"stari\"}");
        kat.NoveVrednosti.Should().Be("{\"Naziv\":\"novi\"}");
        res.Stavke.Single(s => s.Entitet == "Auth").EntitetLabela.Should().Be("Prijava");
        res.Stavke.Single(s => s.Entitet == "Sistem").EntitetLabela.Should().Be("Sistem");
    }

    [Fact]
    public void EntitetLabele_pokriva_sve_auditable_entitete_i_fallback()
    {
        EntitetLabele.Za("ApplicationUser").Should().Be("Korisnik");
        EntitetLabele.Za("Kategorija").Should().Be("Kategorija");
        EntitetLabele.Za("Auth").Should().Be("Prijava");
        EntitetLabele.Za("Sistem").Should().Be("Sistem");
        EntitetLabele.Za("NestoNepoznato").Should().Be("NestoNepoznato");

        EntitetLabele.Sve().Should().Contain(e => e.Entitet == "ApplicationUser" && e.Labela == "Korisnik")
            .And.HaveCount(4);
    }
}
