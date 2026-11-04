using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Infrastructure.Persistence;

/// <summary>
/// Jedan atomski <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>: Postgres zaključava red brojača
/// do kraja transakcije, pa dva paralelna zahteva ne mogu da dobiju isti broj, a prvi broj u godini
/// nastaje bez posebne provere „postoji li red“.
/// </summary>
public sealed class BrojacService(AppDbContext db) : IBrojacService
{
    /// <inheritdoc />
    public async Task<string> SledeciAsync(string vrsta, int godina, CancellationToken ct = default)
    {
        var redni = await db.Database.SqlQuery<int>($"""
            INSERT INTO brojac (vrsta, godina, poslednji) VALUES ({vrsta}, {godina}, 1)
            ON CONFLICT (vrsta, godina) DO UPDATE SET poslednji = brojac.poslednji + 1
            RETURNING poslednji AS "Value"
            """).ToListAsync(ct);

        return Brojac.Formatiraj(vrsta, godina, redni.Single());
    }
}
