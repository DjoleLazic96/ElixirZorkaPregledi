using TpmPregledi.Api.Common;
using TpmPregledi.Application.Features.Auth.Commands;
using TpmPregledi.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// Auth endpointi (tehnički ugovor, engleska imena, isti u svim aplikacijama):
/// <c>POST /auth/login</c>, <c>POST /auth/refresh</c>, <c>POST /auth/logout</c>, <c>GET /auth/me</c>,
/// <c>POST /auth/change-password</c>. Refresh token putuje samo u HttpOnly cookie-ju.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Mapira <c>/api/v1/auth/*</c>.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
             .AllowAnonymous()
             .RequireRateLimiting("auth-login")
             .WithSummary("Prijava korisničkim imenom i lozinkom; refresh token u cookie-ju");

        group.MapPost("/refresh", RefreshAsync)
             .AllowAnonymous()
             .RequireRateLimiting("auth-refresh")
             .WithSummary("Razmena refresh cookie-ja za nov access token (rotacija)");

        group.MapPost("/logout", LogoutAsync)
             .RequireAuthorization()
             .WithSummary("Odjava — revokuje refresh token i briše cookie");

        group.MapGet("/me", GetMeAsync)
             .RequireAuthorization()
             .WithSummary("Trenutno prijavljen korisnik (hidracija UI-ja)");

        group.MapPost("/change-password", ChangePasswordAsync)
             .RequireAuthorization()
             .WithSummary("Promena sopstvene lozinke; revokuje sve refresh tokene");

        return app;
    }

    private static async Task<IResult> LoginAsync([FromBody] LoginCommand command, IMediator mediator, HttpContext http)
    {
        var result = await mediator.Send(command);
        SetRefreshCookie(http, result.RefreshToken, result.RefreshExpiresAt);
        return Results.Ok(result.Response);
    }

    private static async Task<IResult> RefreshAsync(IMediator mediator, HttpContext http)
    {
        var token = http.Request.Cookies[CookieNames.RefreshToken];
        if (string.IsNullOrEmpty(token))
            return Results.Unauthorized();

        var result = await mediator.Send(new RefreshCommand(token));
        SetRefreshCookie(http, result.RefreshToken, result.RefreshExpiresAt);
        return Results.Ok(result.Response);
    }

    private static async Task<IResult> LogoutAsync(IMediator mediator, HttpContext http)
    {
        var token = http.Request.Cookies[CookieNames.RefreshToken];
        await mediator.Send(new LogoutCommand(token));

        // Brisanje MORA sa istim Path/HttpOnly/Secure/SameSite kao pri postavljanju — browser
        // inače tretira „tpm-pregledi.refresh“ na Path=/ kao drugi cookie i stari ostaje.
        http.Response.Cookies.Delete(CookieNames.RefreshToken, CookieOptionsZaRefresh(http, expires: null));
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(IMediator mediator) =>
        Results.Ok(await mediator.Send(new GetCurrentUserQuery()));

    private static async Task<IResult> ChangePasswordAsync([FromBody] ChangePasswordCommand command, IMediator mediator)
    {
        await mediator.Send(command);
        return Results.NoContent();
    }

    /// <summary>
    /// HttpOnly (JS ne vidi) + Secure (samo preko HTTPS-a — zahteva ispravan X-Forwarded-Proto iza
    /// proxy-ja) + SameSite=Strict + Path=/api/v1/auth (cookie ne putuje na ostale rute).
    /// </summary>
    private static void SetRefreshCookie(HttpContext http, string token, DateTime expiresAt) =>
        http.Response.Cookies.Append(CookieNames.RefreshToken, token,
            CookieOptionsZaRefresh(http, new DateTimeOffset(expiresAt)));

    private static CookieOptions CookieOptionsZaRefresh(HttpContext http, DateTimeOffset? expires) => new()
    {
        HttpOnly    = true,
        Secure      = http.Request.IsHttps,   // dev (http://localhost) → false
        SameSite    = SameSiteMode.Strict,
        Path        = "/api/v1/auth",
        Expires     = expires,
        IsEssential = true,                   // cookie consent ne sme da ga filtrira — bez njega app ne radi
    };
}
