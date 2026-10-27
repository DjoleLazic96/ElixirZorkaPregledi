using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TpmPregledi.Infrastructure.Persistence;

/// <summary>
/// Pravi <see cref="AppDbContext"/> za <c>dotnet ef migrations add</c>. Alat inače pokušava da
/// podigne ceo host iz <c>Program.cs</c>, koji fail-fast odbija start bez tajni; ovde je dovoljan
/// connection string (za generisanje migracije se ne otvara veza). Vrednost je dev klaster iz
/// <c>appsettings.Development.json</c> — javna, dev-only.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DevConnectionString =
        "Host=localhost;Port=55434;Database=tpm_pregledi;Username=tpm_pregledi;Password=tpm-pregledi-dev-only";

    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.KonfigurisiPostgres(DevConnectionString);   // isti model deo kao runtime DI
        return new AppDbContext(builder.Options);
    }
}
