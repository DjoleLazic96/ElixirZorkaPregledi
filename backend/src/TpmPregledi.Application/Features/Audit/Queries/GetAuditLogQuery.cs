using TpmPregledi.Application.Common;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Common.Pagination;
using TpmPregledi.Application.Features.Audit.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Audit.Queries;

/// <summary>
/// Paginiran, filtriran pregled audit loga (Admin). Sort je fiksan: <c>created_at DESC, id DESC</c>
/// (najnovije prvo, stabilan tiebreak) — nema <c>sort</c> parametra.
/// </summary>
/// <param name="Stranica">1-bazirana stranica (default 1).</param>
/// <param name="Velicina">Veličina stranice (default 50, max 200 — vidi <see cref="StranicaDefaults"/>).</param>
/// <param name="Korisnik">Filter po korisničkom imenu (contains, case-insensitive).</param>
/// <param name="Entitet">Filter po entitetu (tačno poklapanje, npr. <c>Kategorija</c>, <c>Auth</c>).</param>
/// <param name="Akcija">Filter po akciji (tačno: CREATE/UPDATE/DELETE/LOGIN/LOGIN_FAIL/LOGOUT/START).</param>
/// <param name="Od">Donja granica vremena, inkluzivno, UTC.</param>
/// <param name="Do">Gornja granica vremena, inkluzivno, UTC.</param>
public sealed record GetAuditLogQuery(
    int Stranica = 1,
    int Velicina = StranicaDefaults.Default,
    string? Korisnik = null,
    string? Entitet = null,
    string? Akcija = null,
    DateTime? Od = null,
    DateTime? Do = null)
    : IRequest<StranicaDto<AuditRedDto>>;

/// <summary>Izvršava <see cref="GetAuditLogQuery"/> nad <c>audit_log</c> tabelom.</summary>
public sealed class GetAuditLogHandler(IAppDbContext db)
    : IRequestHandler<GetAuditLogQuery, StranicaDto<AuditRedDto>>
{
    /// <inheritdoc />
    public async Task<StranicaDto<AuditRedDto>> Handle(GetAuditLogQuery req, CancellationToken ct)
    {
        var (stranica, velicina) = StranicaDefaults.Normalizuj(req.Stranica, req.Velicina);

        var upit = db.AuditLog.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(req.Korisnik))
        {
            // ToLower().Contains se prevodi u SQL na Npgsql-u i evaluira na InMemory provideru
            // (unit testovi) — zato ne koristimo EF.Functions.ILike (Npgsql-specifično).
            var term = req.Korisnik.Trim().ToLower();
            upit = upit.Where(a => a.UserName != null && a.UserName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(req.Entitet))
            upit = upit.Where(a => a.Entity == req.Entitet);

        if (!string.IsNullOrWhiteSpace(req.Akcija))
            upit = upit.Where(a => a.Action == req.Akcija);

        if (req.Od is { } od)
        {
            var odUtc = DatumHelper.KaoUtc(od);
            upit = upit.Where(a => a.CreatedAt >= odUtc);
        }

        if (req.Do is { } doo)
        {
            var dooUtc = DatumHelper.KaoUtc(doo);
            upit = upit.Where(a => a.CreatedAt <= dooUtc);
        }

        var ukupno = await upit.CountAsync(ct);

        var redovi = await upit
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((stranica - 1) * velicina)
            .Take(velicina)
            .Select(a => new
            {
                a.Id, a.CreatedAt, a.UserName, a.Action, a.Entity,
                a.EntityId, a.IpAddress, a.OldValues, a.NewValues,
            })
            .ToListAsync(ct);

        // Labela se mapira u memoriji — EntitetLabele nije prevodivo u SQL.
        var stavke = redovi
            .Select(a => new AuditRedDto(
                Id:             a.Id,
                Vreme:          a.CreatedAt,
                Korisnik:       a.UserName,
                Akcija:         a.Action,
                Entitet:        a.Entity,
                EntitetLabela:  EntitetLabele.Za(a.Entity),
                EntitetId:      a.EntityId,
                Ip:             a.IpAddress,
                StareVrednosti: a.OldValues,
                NoveVrednosti:  a.NewValues))
            .ToList();

        return new StranicaDto<AuditRedDto>(stavke, ukupno, stranica, velicina);
    }
}
