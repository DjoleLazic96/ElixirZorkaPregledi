using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Infrastructure.Persistence;

/// <summary>
/// Jedno mesto za deo konfiguracije <c>DbContext</c>-a koji utiče na <b>model</b> (provider +
/// konvencija imenovanja). Dele ga runtime DI (<c>DependencyInjection</c>) i design-time factory
/// (<c>dotnet ef</c>) — ako bi se razišli, migracija bi opisivala drugačiju šemu od one koju
/// aplikacija koristi.
/// </summary>
internal static class DbContextOptionsExtensions
{
    /// <summary>Npgsql + snake_case imena tabela/kolona/indeksa.</summary>
    public static DbContextOptionsBuilder KonfigurisiPostgres(this DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
