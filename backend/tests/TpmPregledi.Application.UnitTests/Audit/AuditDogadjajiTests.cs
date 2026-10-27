using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Infrastructure.Audit;
using TpmPregledi.Infrastructure.Persistence;

namespace TpmPregledi.Application.UnitTests.Audit;

/// <summary>
/// AuditDogadjaji upisuje ispravan audit_log red za LOGIN, LOGIN_FAIL, LOGOUT i START
/// (Entity Auth/Sistem, NewValues samo za START) i nikad ne propagira grešku.
/// </summary>
public class AuditDogadjajiTests
{
    private static readonly FakeClock Sat = new();

    private static AuditDogadjaji Sut(AppDbContext ctx) =>
        new(ctx, Sat, NullLogger<AuditDogadjaji>.Instance);

    [Fact]
    public async Task PrijavaUspeh_pise_LOGIN_red()
    {
        await using var ctx = InMemoryDb.New();

        await Sut(ctx).PrijavaUspehAsync("u-1", "admin", "10.0.0.5");

        var log = await ctx.AuditLog.SingleAsync();
        log.Action.Should().Be("LOGIN");
        log.Entity.Should().Be("Auth");
        log.UserId.Should().Be("u-1");
        log.UserName.Should().Be("admin");
        log.IpAddress.Should().Be("10.0.0.5");
        log.OldValues.Should().BeNull();
        log.NewValues.Should().BeNull();
        log.CreatedAt.Should().Be(Sat.UtcNow);
    }

    [Fact]
    public async Task PrijavaNeuspeh_pise_LOGIN_FAIL_sa_unetim_imenom_i_bez_userId()
    {
        await using var ctx = InMemoryDb.New();

        await Sut(ctx).PrijavaNeuspehAsync("nepoznati", "10.0.0.9");

        var log = await ctx.AuditLog.SingleAsync();
        log.Action.Should().Be("LOGIN_FAIL");
        log.Entity.Should().Be("Auth");
        log.UserId.Should().BeNull();
        log.UserName.Should().Be("nepoznati");
        log.IpAddress.Should().Be("10.0.0.9");
    }

    [Fact]
    public async Task Odjava_pise_LOGOUT_red()
    {
        await using var ctx = InMemoryDb.New();

        await Sut(ctx).OdjavaAsync("u-2", "pera", "10.0.0.7");

        var log = await ctx.AuditLog.SingleAsync();
        log.Action.Should().Be("LOGOUT");
        log.Entity.Should().Be("Auth");
        log.UserId.Should().Be("u-2");
        log.UserName.Should().Be("pera");
    }

    [Fact]
    public async Task Start_pise_START_red_sa_verzijom_u_NewValues()
    {
        await using var ctx = InMemoryDb.New();

        await Sut(ctx).StartAsync("1.2.3", "abc1234", "Production");

        var log = await ctx.AuditLog.SingleAsync();
        log.Action.Should().Be("START");
        log.Entity.Should().Be("Sistem");
        log.EntityId.Should().Be("start");
        log.UserId.Should().BeNull();
        log.UserName.Should().Be("system");
        log.IpAddress.Should().BeNull();

        var novo = JsonDocument.Parse(log.NewValues!).RootElement;
        novo.GetProperty("verzija").GetString().Should().Be("1.2.3");
        novo.GetProperty("gitSha").GetString().Should().Be("abc1234");
        novo.GetProperty("okruzenje").GetString().Should().Be("Production");
    }

    [Fact]
    public async Task Belezenje_je_best_effort_greska_se_ne_propagira()
    {
        // Dispose-ovan kontekst → SaveChangesAsync baca; metode NE smeju da bace.
        var ctx = InMemoryDb.New();
        var sut = Sut(ctx);
        await ctx.DisposeAsync();

        await FluentActions.Awaiting(() => sut.PrijavaUspehAsync("u-1", "admin", null)).Should().NotThrowAsync();
        await FluentActions.Awaiting(() => sut.StartAsync("1", "a", "Dev")).Should().NotThrowAsync();
    }
}
