namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// Dijagnostički endpointi — po jedan za svaku politiku autorizacije, vraćaju trivijalan JSON.
/// Integracioni testovi njima dokazuju da politike propuštaju tačno one role koje treba,
/// nezavisno od poslovnih ruta. Mapiraju se samo u Development-u (vidi <c>Program.cs</c>).
/// </summary>
public static class DiagEndpoints
{
    /// <summary>Mapira <c>/api/v1/_diag/*</c>.</summary>
    public static IEndpointRouteBuilder MapDiagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/_diag").WithTags("Diag");

        group.MapGet("/admin-only", () => Results.Ok(new { ok = true, scope = "admin" }))
             .RequireAuthorization("RequireAdmin")
             .WithSummary("Dokaz da RequireAdmin propušta samo Admina");

        group.MapGet("/korisnik", () => Results.Ok(new { ok = true, scope = "korisnik" }))
             .RequireAuthorization("RequireKorisnik")
             .WithSummary("Dokaz da RequireKorisnik propušta Admina i Korisnika");

        return app;
    }
}
