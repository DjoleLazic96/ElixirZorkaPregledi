using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.UnitTests.Common.Fakes;

/// <summary>Fake izdavač tokena: vraća fiksne vrednosti i beleži revokacije po korisniku.</summary>
public sealed class FakeJwtTokenService : IJwtTokenService
{
    public int RevokeCount { get; private set; }
    public List<string> RevokeAllZa { get; } = [];
    public List<string> DeleteInactiveZa { get; } = [];

    public Task<TokenPair> IssueTokenPairAsync(ApplicationUser user, IReadOnlyList<string> roles, string? ipAddress, CancellationToken ct = default) =>
        Task.FromResult(new TokenPair("access", FakeClock.Podrazumevano.AddMinutes(15), "refresh", FakeClock.Podrazumevano.AddDays(7)));

    public Task<RotateResult?> RotateAsync(string refreshToken, string? ipAddress, CancellationToken ct = default) =>
        Task.FromResult<RotateResult?>(null);

    public Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        RevokeCount++;
        return Task.CompletedTask;
    }

    public Task RevokeAllAsync(string userId, CancellationToken ct = default)
    {
        RevokeAllZa.Add(userId);
        return Task.CompletedTask;
    }

    public Task DeleteInactiveAsync(string userId, CancellationToken ct = default)
    {
        DeleteInactiveZa.Add(userId);
        return Task.CompletedTask;
    }
}
