using System.Text.Json;
using Microsoft.Extensions.Logging;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;

namespace TpmPregledi.Infrastructure.Audit;

/// <summary>
/// Piše događaje koji nisu izmena entiteta (LOGIN / LOGIN_FAIL / LOGOUT / START) u <c>audit_log</c>.
/// </summary>
/// <remarks>
/// Upisi su <b>best-effort</b>: svaki ima svoj try/catch koji grešku loguje (Warning) i guta.
/// Prijava ne sme da padne zbog audita (npr. baza nakratko nedostupna) — bezbednosno je bolje
/// pustiti prijavu nego je odbiti zbog logovanja. Zaseban <c>SaveChangesAsync</c> po događaju:
/// LOGIN_FAIL iz <c>catch</c> grane login handlera ide nezavisno od glavne transakcije.
/// </remarks>
public sealed class AuditDogadjaji(
    IAppDbContext db,
    IDateTimeProvider clock,
    ILogger<AuditDogadjaji> logger)
    : IAuditDogadjaji
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task PrijavaUspehAsync(string userId, string userName, string? ip, CancellationToken ct = default) =>
        PisiAsync("LOGIN", "Auth", entityId: null, userId, userName, ip, newValues: null, ct);

    /// <inheritdoc />
    public Task PrijavaNeuspehAsync(string korisnickoIme, string? ip, CancellationToken ct = default) =>
        PisiAsync("LOGIN_FAIL", "Auth", entityId: null, userId: null, korisnickoIme, ip, newValues: null, ct);

    /// <inheritdoc />
    public Task OdjavaAsync(string? userId, string? userName, string? ip, CancellationToken ct = default) =>
        PisiAsync("LOGOUT", "Auth", entityId: null, userId, userName, ip, newValues: null, ct);

    /// <inheritdoc />
    public Task StartAsync(string verzija, string gitSha, string okruzenje, CancellationToken ct = default)
    {
        var novo = JsonSerializer.Serialize(new { verzija, gitSha, okruzenje }, Json);
        return PisiAsync("START", "Sistem", entityId: "start", userId: null, userName: "system", ip: null, novo, ct);
    }

    private async Task PisiAsync(
        string akcija, string entitet, string? entityId,
        string? userId, string? userName, string? ip, string? newValues,
        CancellationToken ct)
    {
        try
        {
            db.AuditLog.Add(new AuditLog
            {
                UserId    = userId,
                UserName  = userName,
                Action    = akcija,
                Entity    = entitet,
                EntityId  = entityId,
                OldValues = null,
                NewValues = newValues,
                CreatedAt = clock.UtcNow,
                IpAddress = ip,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Neuspeo upis audit događaja {Akcija}/{Entitet} za korisnika {Korisnik} (IP {Ip}).",
                akcija, entitet, userName, ip);
        }
    }
}
