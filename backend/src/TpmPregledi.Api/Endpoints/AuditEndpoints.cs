using TpmPregledi.Application.Common.Pagination;
using TpmPregledi.Application.Features.Audit;
using TpmPregledi.Application.Features.Audit.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TpmPregledi.Api.Endpoints;

/// <summary>
/// Audit log (Admin, read-only): paginirana lista sa filterima i lista entiteta za filter.
/// Tabela je append-only — nema ruta za izmenu ni brisanje.
/// </summary>
public static class AuditEndpoints
{
    /// <summary>Mapira <c>/api/v1/audit</c> i <c>/api/v1/audit/entiteti</c>.</summary>
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit")
            .WithTags("Audit")
            .RequireAuthorization("RequireAdmin");

        // `do` je rezervisana reč u C#, zato parametar `doo` sa eksplicitnim imenom na žici.
        group.MapGet("",
            async (
                [FromQuery] int? stranica,
                [FromQuery] int? velicina,
                [FromQuery] string? korisnik,
                [FromQuery] string? entitet,
                [FromQuery] string? akcija,
                [FromQuery] DateTime? od,
                [FromQuery(Name = "do")] DateTime? doo,
                IMediator m) =>
                Results.Ok(await m.Send(new GetAuditLogQuery(
                    Stranica: stranica ?? 1,
                    Velicina: velicina ?? StranicaDefaults.Default,
                    Korisnik: korisnik,
                    Entitet:  entitet,
                    Akcija:   akcija,
                    Od:       od,
                    Do:       doo))))
            .WithSummary("Audit log: filteri stranica, velicina, korisnik, entitet, akcija, od, do; sort created_at desc");

        // Statička lista — nema MediatR posrednika, labele su u Application sloju (EntitetLabele).
        group.MapGet("/entiteti", () => Results.Ok(EntitetLabele.Sve()))
            .WithSummary("Entiteti sa srpskim labelama za filter „Entitet“");

        return app;
    }
}
