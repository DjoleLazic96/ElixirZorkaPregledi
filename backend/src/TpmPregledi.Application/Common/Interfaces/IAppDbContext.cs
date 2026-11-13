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


    /// <summary>Neradni dani i praznici.</summary>
    DbSet<NeradniDan> NeradniDani { get; }

    /// <summary>Odsustva i zahtevi za odsustvo.</summary>
    DbSet<Odsustvo> Odsustva { get; }

    /// <summary>Pregledi (DP i PZ).</summary>
    DbSet<Pregled> Pregledi { get; }

    /// <summary>Učesnici PZ-a.</summary>
    DbSet<PregledUcesnik> PregledUcesnici { get; }

    /// <summary>Istorija pregleda.</summary>
    DbSet<PregledDogadjaj> PregledDogadjaji { get; }

    /// <summary>Zahtevi za prebacivanje pregleda.</summary>
    DbSet<ZahtevPrebacivanja> ZahteviPrebacivanja { get; }

    /// <summary>Odgovori na stavke pregleda.</summary>
    DbSet<Odgovor> Odgovori { get; }

    /// <summary>Uređaji van funkcije na pregledu.</summary>
    DbSet<UredjajVanFunkcije> UredjajiVanFunkcije { get; }

    /// <summary>Slike uz stavke.</summary>
    DbSet<Fotografija> Fotografije { get; }

    /// <summary>Primljene operacije (idempotentnost).</summary>
    DbSet<SinhronizacijaOperacija> SinhronizacijaOperacije { get; }

    /// <summary>Praćenje promena — odbijena operacija sinhronizacije odbacuje svoje nesnimljene izmene.</summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    /// <summary>Sporni unosi.</summary>
    DbSet<SporanUnos> SporniUnosi { get; }

    /// <summary>Zahtevi za doradu.</summary>
    DbSet<ZahtevDorade> ZahteviDorade { get; }

    /// <summary>Potrebne akcije.</summary>
    DbSet<Akcija> Akcije { get; }

    /// <summary>Istorija akcija.</summary>
    DbSet<AkcijaDogadjaj> AkcijaDogadjaji { get; }

    /// <summary>Kvarovi.</summary>
    DbSet<Kvar> Kvarovi { get; }

    /// <summary>Obaveštenja (zvono) sa stanjem isporuke push-a i mejla.</summary>
    DbSet<Obavestenje> Obavestenja { get; }

    /// <summary>Web Push pretplate uređaja.</summary>
    DbSet<PushPretplata> PushPretplate { get; }

    /// <summary>Predlozi izmena šablona (R2.8).</summary>
    DbSet<PredlogIzmene> PredloziIzmena { get; }

    /// <summary>„Slažem se sa predlogom“ (zamerka 37).</summary>
    DbSet<PredlogSaglasnost> PredlogSaglasnosti { get; }

    /// <summary>Snima promene (audit interceptor radi u istoj transakciji).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
