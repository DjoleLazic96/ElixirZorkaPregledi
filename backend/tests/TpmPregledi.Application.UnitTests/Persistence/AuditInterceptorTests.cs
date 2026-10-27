using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Persistence.Interceptors;

namespace TpmPregledi.Application.UnitTests.Persistence;

/// <summary>
/// AuditSaveChangesInterceptor: CREATE/UPDATE/DELETE redovi sa snapshotima, audit kolone
/// (CreatedAt/By, UpdatedAt/By), osetljiva polja se ne serijalizuju, AuditLog sam se ne audit-uje,
/// korisnik iz ICurrentUser ili fallback.
/// </summary>
public class AuditInterceptorTests
{
    private static readonly FakeClock Sat = new();

    [Fact]
    public async Task CREATE_pise_red_sa_novim_vrednostima_i_puni_created_kolone()
    {
        await using var ctx = InMemoryDb.New(new FakeCurrentUser(), Sat);
        var k = new Kategorija { Sifra = "K1", Naziv = "Prva" };
        ctx.Kategorije.Add(k);

        await ctx.SaveChangesAsync();

        k.CreatedAt.Should().Be(Sat.UtcNow);
        k.CreatedBy.Should().Be("u-1");
        k.UpdatedAt.Should().BeNull("nije još menjan");
        k.UpdatedBy.Should().BeNull();

        var log = await ctx.AuditLog.SingleAsync();
        log.Action.Should().Be("CREATE");
        log.Entity.Should().Be(nameof(Kategorija));
        log.EntityId.Should().Be(k.Id.ToString());
        log.UserId.Should().Be("u-1");
        log.UserName.Should().Be("admin");
        log.IpAddress.Should().Be("10.1.2.3");
        log.OldValues.Should().BeNull();
        JsonDocument.Parse(log.NewValues!).RootElement.GetProperty("Sifra").GetString().Should().Be("K1");
    }

    [Fact]
    public async Task UPDATE_pise_stare_i_nove_vrednosti_i_puni_updated_kolone()
    {
        await using var ctx = InMemoryDb.New(new FakeCurrentUser { UserId = "u-9", UserName = "marko" }, Sat);
        var k = new Kategorija { Sifra = "K1", Naziv = "Prva" };
        ctx.Kategorije.Add(k);
        await ctx.SaveChangesAsync();

        Sat.UtcNow = Sat.UtcNow.AddHours(1);
        k.Naziv = "Izmenjena";
        await ctx.SaveChangesAsync();

        k.UpdatedAt.Should().Be(Sat.UtcNow);
        k.UpdatedBy.Should().Be("u-9");
        k.CreatedBy.Should().Be("u-9", "created se ne menja pri izmeni");

        var log = await ctx.AuditLog.SingleAsync(a => a.Action == "UPDATE");
        JsonDocument.Parse(log.OldValues!).RootElement.GetProperty("Naziv").GetString().Should().Be("Prva");
        JsonDocument.Parse(log.NewValues!).RootElement.GetProperty("Naziv").GetString().Should().Be("Izmenjena");
    }

    [Fact]
    public async Task DELETE_pise_samo_stare_vrednosti()
    {
        await using var ctx = InMemoryDb.New(new FakeCurrentUser(), Sat);
        var k = new Kategorija { Sifra = "K1", Naziv = "Prva" };
        ctx.Kategorije.Add(k);
        await ctx.SaveChangesAsync();

        ctx.Kategorije.Remove(k);
        await ctx.SaveChangesAsync();

        var log = await ctx.AuditLog.SingleAsync(a => a.Action == "DELETE");
        log.OldValues.Should().NotBeNull();
        log.NewValues.Should().BeNull();
    }

    [Fact]
    public async Task Osetljiva_polja_korisnika_se_ne_serijalizuju()
    {
        await using var ctx = InMemoryDb.New(new FakeCurrentUser(), Sat);
        ctx.Users.Add(new ApplicationUser
        {
            UserName = "novi", FullName = "Novi Korisnik",
            PasswordHash = "HASH", SecurityStamp = "STAMP", ConcurrencyStamp = "CS",
        });
        await ctx.SaveChangesAsync();

        var log = await ctx.AuditLog.SingleAsync();
        var json = log.NewValues!;
        json.Should().Contain("\"UserName\"").And.Contain("\"FullName\"");
        json.Should().NotContain("PasswordHash").And.NotContain("HASH")
            .And.NotContain("SecurityStamp").And.NotContain("ConcurrencyStamp")
            .And.NotContain("AccessFailedCount").And.NotContain("LockoutEnd");
    }

    [Theory]
    [InlineData("PasswordHash", true)]
    [InlineData("ClientSecretEncrypted", true)]
    [InlineData("NovaLozinka", true)]
    [InlineData("RefreshToken", true)]
    [InlineData("Naziv", false)]
    [InlineData("Email", false)]
    public void JeOsetljivo_prepoznaje_imena_sa_osetljivim_recima(string ime, bool ocekivano)
        => AuditSaveChangesInterceptor.JeOsetljivo(ime).Should().Be(ocekivano);

    [Fact]
    public async Task AuditLog_sam_i_RefreshToken_se_ne_audituju()
    {
        await using var ctx = InMemoryDb.New(new FakeCurrentUser(), Sat);
        ctx.AuditLog.Add(new AuditLog { Action = "LOGIN", Entity = "Auth" });
        ctx.RefreshTokens.Add(new RefreshToken { TokenHash = "h", UserId = "u-1", ExpiresAt = Sat.UtcNow.AddDays(1) });

        await ctx.SaveChangesAsync();

        (await ctx.AuditLog.CountAsync()).Should().Be(1, "samo ručno dodati LOGIN red, bez rekurzije i bez tokena");
    }

    [Fact]
    public async Task Bez_prijavljenog_korisnika_izmena_ApplicationUser_a_se_pripisuje_tom_korisniku()
    {
        // Login tok: JWT još ne postoji, a server ažurira LastLoginAt baš za tog korisnika.
        await using var ctx = InMemoryDb.New(FakeCurrentUser.Anoniman, Sat);
        var u = new ApplicationUser { UserName = "pera", FullName = "Pera" };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();

        var log = await ctx.AuditLog.SingleAsync();
        log.UserId.Should().Be(u.Id);
        log.UserName.Should().Be("pera");
    }

    [Fact]
    public async Task Bez_prijavljenog_korisnika_ostali_entiteti_dobijaju_system()
    {
        await using var ctx = InMemoryDb.New(FakeCurrentUser.Anoniman, Sat);
        var k = new Kategorija { Sifra = "SEED", Naziv = "Seed" };
        ctx.Kategorije.Add(k);
        await ctx.SaveChangesAsync();

        k.CreatedBy.Should().Be("system");
        var log = await ctx.AuditLog.SingleAsync();
        log.UserId.Should().BeNull();
        log.UserName.Should().Be("system");
    }
}
