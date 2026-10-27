using Microsoft.EntityFrameworkCore;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Infrastructure.Persistence;
using TpmPregledi.Infrastructure.Persistence.Interceptors;

namespace TpmPregledi.Application.UnitTests.Common;

/// <summary>
/// EF InMemory <see cref="AppDbContext"/> sa jedinstvenim (Guid) imenom baze po testu — testovi
/// ne dele stanje. Samo za unit testove handlera; integracioni idu na pravi Postgres.
/// </summary>
public static class InMemoryDb
{
    /// <summary>Kontekst bez audit interceptora.</summary>
    public static AppDbContext New() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>Kontekst sa <see cref="AuditSaveChangesInterceptor"/> (audit kolone + audit_log redovi).</summary>
    public static AppDbContext New(ICurrentUser user, IDateTimeProvider clock) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(user, clock))
            .Options);
}
