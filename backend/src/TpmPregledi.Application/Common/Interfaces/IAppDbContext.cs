using Microsoft.EntityFrameworkCore;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Apstrakcija nad <c>AppDbContext</c>-om za Application sloj — handleri ne zavise od
/// Infrastructure-a. Svaki novi entitet dobija <c>DbSet</c> ovde i u <c>AppDbContext</c>.
/// </summary>
public interface IAppDbContext
{
    /// <summary>Korisnici (Identity).</summary>
    DbSet<ApplicationUser> Users { get; }

    /// <summary>Refresh tokeni (heševi).</summary>
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>Audit log (append-only).</summary>
    DbSet<AuditLog> AuditLog { get; }

    /// <summary>Prava korisnika (štikliranje, R1.6).</summary>
    DbSet<KorisnikPravo> KorisnikPrava { get; }

    /// <summary>Sekcije pogona (10…70).</summary>
    DbSet<Sekcija> Sekcije { get; }

    /// <summary>Brojači brojeva pregleda, akcija i kvarova.</summary>
    DbSet<Brojac> Brojaci { get; }

    /// <summary>Podešavanja aplikacije (ključ → JSON).</summary>
    DbSet<Podesavanje> Podesavanja { get; }

    /// <summary>Registar opreme.</summary>
    DbSet<Uredjaj> Uredjaji { get; }

    /// <summary>Katalog aktivnosti.</summary>
    DbSet<Aktivnost> Aktivnosti { get; }

    /// <summary>Dodatna polja uz aktivnosti.</summary>
    DbSet<DodatnoPolje> DodatnaPolja { get; }

    /// <summary>Šest šablona.</summary>
    DbSet<Sablon> Sabloni { get; }

    /// <summary>Verzije šablona.</summary>
    DbSet<SablonVerzija> SablonVerzije { get; }

    /// <summary>Stavke verzija.</summary>
    DbSet<SablonStavka> SablonStavke { get; }

    /// <summary>Dodatna polja na stavkama.</summary>
    DbSet<SablonStavkaPolje> SablonStavkaPolja { get; }

    /// <summary>Istorijat šablona.</summary>
    DbSet<SablonDogadjaj> SablonDogadjaji { get; }

    /// <summary>Fajlovi na volumenu.</summary>
    DbSet<Fajl> Fajlovi { get; }


    /// <summary>Snima promene (audit interceptor radi u istoj transakciji).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
