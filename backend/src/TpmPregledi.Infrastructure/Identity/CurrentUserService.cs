using System.Security.Claims;
using TpmPregledi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace TpmPregledi.Infrastructure.Identity;

/// <summary>
/// Čita trenutnog korisnika iz <see cref="HttpContext.User"/> claims-a. Van HTTP zahteva
/// (seed, START, testovi bez konteksta) sve je null/prazno.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    /// <inheritdoc />
    public string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub");

    /// <inheritdoc />
    public string? UserName =>
        Principal?.Identity?.Name ?? Principal?.FindFirstValue("preferred_username");

    /// <inheritdoc />
    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    /// <inheritdoc />
    public string? IpAddress =>
        accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
