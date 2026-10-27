using TpmPregledi.Application.Common.Interfaces;

namespace TpmPregledi.Application.UnitTests.Common.Fakes;

/// <summary>Ručni fake za <see cref="ICurrentUser"/>: podrazumevano prijavljen admin sa IP adresom.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; init; } = "u-1";
    public string? UserName { get; init; } = "admin";
    public IReadOnlyList<string> Roles { get; init; } = [global::TpmPregledi.Domain.Identity.Roles.Admin];
    public string? IpAddress { get; init; } = "10.1.2.3";
    public bool IsAuthenticated => UserId is not null;

    /// <summary>Niko nije prijavljen (seed, START, login pre izdavanja tokena).</summary>
    public static FakeCurrentUser Anoniman => new() { UserId = null, UserName = null, Roles = [] };
}
