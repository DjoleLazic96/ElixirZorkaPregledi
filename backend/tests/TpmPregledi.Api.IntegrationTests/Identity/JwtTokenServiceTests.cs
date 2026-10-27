using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TpmPregledi.Api.IntegrationTests.Infrastructure;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;
using TpmPregledi.Infrastructure.Identity;
using TpmPregledi.Infrastructure.Persistence;

namespace TpmPregledi.Api.IntegrationTests.Identity;

/// <summary>
/// JwtTokenService nad pravom bazom: u bazi je samo SHA-256 heš (nikad sirovi token), access token
/// nosi role claim, rotacija revokuje stari i izdaje nov, RevokeAll gasi sve važeće, DeleteInactive
/// briše revokovane/istekle.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class JwtTokenServiceTests(CustomWebAppFactory factory)
{
    [Fact]
    public async Task Izdavanje_cuva_hash_a_ne_sirovi_token_i_access_nosi_rolu()
    {
        var user = await factory.SeedUserAsync("jwt-" + Guid.NewGuid().ToString("N")[..8], "Jwt2026!!", Roles.Korisnik);
        await using var scope = factory.NoviScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pair = await svc.IssueTokenPairAsync(user, [Roles.Korisnik], "10.0.0.1");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(pair.AccessToken);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == Roles.Korisnik);
        jwt.Claims.Should().Contain(c => c.Type == "preferred_username" && c.Value == user.UserName);

        var red = await db.RefreshTokens.SingleAsync(r => r.UserId == user.Id);
        red.TokenHash.Should().Be(RefreshTokenHasher.Hash(pair.RefreshToken));
        red.TokenHash.Should().NotBe(pair.RefreshToken);
        red.CreatedByIp.Should().Be("10.0.0.1");
        (await db.RefreshTokens.AnyAsync(r => r.TokenHash == pair.RefreshToken)).Should().BeFalse("sirovi token nije nigde u bazi");
    }

    [Fact]
    public async Task Rotacija_revokuje_stari_i_izdaje_nov_a_ponovna_rotacija_starog_pada()
    {
        var user = await factory.SeedUserAsync("rot-" + Guid.NewGuid().ToString("N")[..8], "Jwt2026!!", Roles.Korisnik);
        await using var scope = factory.NoviScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pair = await svc.IssueTokenPairAsync(user, [Roles.Korisnik], null);
        var rotated = await svc.RotateAsync(pair.RefreshToken, null);

        rotated.Should().NotBeNull();
        rotated!.Tokens.RefreshToken.Should().NotBe(pair.RefreshToken);
        rotated.User.Id.Should().Be(user.Id);

        var stari = await db.RefreshTokens.SingleAsync(r => r.TokenHash == RefreshTokenHasher.Hash(pair.RefreshToken));
        stari.RevokedAt.Should().NotBeNull();

        (await svc.RotateAsync(pair.RefreshToken, null)).Should().BeNull("revokovan token se ne može ponovo rotirati");
        (await svc.RotateAsync("izmisljen", null)).Should().BeNull();
    }

    [Fact]
    public async Task RevokeAll_gasi_sve_vazece_a_DeleteInactive_ih_brise()
    {
        var user = await factory.SeedUserAsync("rev-" + Guid.NewGuid().ToString("N")[..8], "Jwt2026!!", Roles.Korisnik);
        await using var scope = factory.NoviScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var a = await svc.IssueTokenPairAsync(user, [Roles.Korisnik], null);
        var b = await svc.IssueTokenPairAsync(user, [Roles.Korisnik], null);

        await svc.RevokeAllAsync(user.Id);

        var tokeni = await db.RefreshTokens.AsNoTracking().Where(r => r.UserId == user.Id).ToListAsync();
        tokeni.Should().HaveCount(2).And.OnlyContain(r => r.RevokedAt != null);
        // Nov scope = nov zahtev: ExecuteUpdate zaobilazi change tracker, pa bi isti DbContext još
        // držao stare (nerevokovane) instance tokena u memoriji.
        await using (var noviScope = factory.NoviScope())
        {
            var svc2 = noviScope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            (await svc2.RotateAsync(a.RefreshToken, null)).Should().BeNull();
            (await svc2.RotateAsync(b.RefreshToken, null)).Should().BeNull();
        }

        await svc.DeleteInactiveAsync(user.Id);
        (await db.RefreshTokens.AsNoTracking().CountAsync(r => r.UserId == user.Id)).Should().Be(0);
    }
}
