using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Infrastructure.Persistence;

/// <summary>
/// Glavni EF Core kontekst. Nasleđuje <see cref="IdentityDbContext{TUser, TRole, TKey}"/> (Identity
/// tabele sa string PK) i implementira <see cref="IAppDbContext"/> za Application sloj.
/// </summary>
/// <remarks>
/// <para>
/// <b>Imenovanje:</b> <c>EFCore.NamingConventions</c> (<c>UseSnakeCaseNamingConvention</c>) samo
/// snake_case-uje imena koja EF izvede (iz imena <c>DbSet</c>-a, tj. množina: <c>Uredjaji</c> →
/// <c>uredjaji</c>). <b>Jednina</b> dolazi iz eksplicitnog <c>ToTable("uredjaj")</c> — zato ga
/// svaki entitet ima. Kolone i indeksi ostaju na konvenciji (<c>created_at</c>, <c>ix_…</c>);
/// jedinstveni indeksi dobijaju eksplicitno <c>ux_…</c> ime.
/// </para>
/// <para>
/// <b>Identity tabele</b> zadržavaju <c>AspNetUsers</c>, <c>AspNetRoles</c>… (Identity ih zadaje
/// eksplicitnim <c>ToTable</c>, pa ih konvencija ne dira), dok su im kolone snake_case
/// (<c>normalized_user_name</c>). Sistemske tabele (Identity, <c>refresh_token</c>, <c>audit_log</c>)
/// imaju engleske kolone; domenske tabele srpske.
/// </para>
/// <para>
/// Konfiguracija je u <c>private static Configure*</c> metodama, ne u <c>IEntityTypeConfiguration</c>
/// klasama — manje fajlova, cela šema se čita na jednom mestu.
/// </para>
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options), IAppDbContext
{
    // ─── Sistem ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <inheritdoc />
    public DbSet<AuditLog> AuditLog => Set<AuditLog>();

    /// <inheritdoc />
    public DbSet<KorisnikPravo> KorisnikPrava => Set<KorisnikPravo>();

    /// <inheritdoc />
    public DbSet<Podesavanje> Podesavanja => Set<Podesavanje>();

    /// <inheritdoc />
    public DbSet<Brojac> Brojaci => Set<Brojac>();

    // ─── Domen ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public DbSet<Sekcija> Sekcije => Set<Sekcija>();

    /// <inheritdoc />
    public DbSet<Uredjaj> Uredjaji => Set<Uredjaj>();

    /// <inheritdoc />
    public DbSet<Aktivnost> Aktivnosti => Set<Aktivnost>();

    /// <inheritdoc />
    public DbSet<DodatnoPolje> DodatnaPolja => Set<DodatnoPolje>();

    /// <inheritdoc />
    public DbSet<Sablon> Sabloni => Set<Sablon>();

    /// <inheritdoc />
    public DbSet<SablonVerzija> SablonVerzije => Set<SablonVerzija>();

    /// <inheritdoc />
    public DbSet<SablonStavka> SablonStavke => Set<SablonStavka>();

    /// <inheritdoc />
    public DbSet<SablonStavkaPolje> SablonStavkaPolja => Set<SablonStavkaPolje>();

    /// <inheritdoc />
    public DbSet<SablonDogadjaj> SablonDogadjaji => Set<SablonDogadjaj>();

    /// <inheritdoc />
    public DbSet<Fajl> Fajlovi => Set<Fajl>();

    /// <inheritdoc />
    public DbSet<SporanUnos> SporniUnosi => Set<SporanUnos>();

    /// <inheritdoc />
    public DbSet<ZahtevDorade> ZahteviDorade => Set<ZahtevDorade>();

    /// <inheritdoc />
    public DbSet<Odgovor> Odgovori => Set<Odgovor>();

    /// <inheritdoc />
    public DbSet<UredjajVanFunkcije> UredjajiVanFunkcije => Set<UredjajVanFunkcije>();

    /// <inheritdoc />
    public DbSet<Fotografija> Fotografije => Set<Fotografija>();

    /// <inheritdoc />
    public DbSet<SinhronizacijaOperacija> SinhronizacijaOperacije => Set<SinhronizacijaOperacija>();

    /// <inheritdoc />
    public DbSet<NeradniDan> NeradniDani => Set<NeradniDan>();

    /// <inheritdoc />
    public DbSet<Odsustvo> Odsustva => Set<Odsustvo>();

    /// <inheritdoc />
    public DbSet<Pregled> Pregledi => Set<Pregled>();

    /// <inheritdoc />
    public DbSet<PregledUcesnik> PregledUcesnici => Set<PregledUcesnik>();

    /// <inheritdoc />
    public DbSet<PregledDogadjaj> PregledDogadjaji => Set<PregledDogadjaj>();

    /// <inheritdoc />
    public DbSet<ZahtevPrebacivanja> ZahteviPrebacivanja => Set<ZahtevPrebacivanja>();


    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        ConfigureIdentity(b);
        ConfigureSistem(b);
        ConfigureOsnova(b);
        ConfigureOprema(b);
        ConfigureSabloni(b);
        ConfigureRaspored(b);
        ConfigurePopunjavanje(b);
    }

    // ─── Identity + refresh tokeni ─────────────────────────────────────────

    private static void ConfigureIdentity(ModelBuilder b)
    {
        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_token");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.CreatedByIp).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_token_token_hash");
            e.HasIndex(x => x.UserId);
            // Cascade: korisnici se ne brišu fizički, ali ako bi se ikad obrisali, tokeni nemaju smisla bez njih.
            e.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // ─── Audit log ─────────────────────────────────────────────────────────

    private static void ConfigureSistem(ModelBuilder b)
    {
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).IsRequired().HasMaxLength(20);
            e.Property(x => x.Entity).IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.OldValues).HasColumnType("jsonb");
            e.Property(x => x.NewValues).HasColumnType("jsonb");
            e.Property(x => x.IpAddress).HasMaxLength(64);
            // user_id namerno BEZ FK ka AspNetUsers — red mora da preživi sve.
            e.HasIndex(x => new { x.Entity, x.EntityId });
            e.HasIndex(x => x.CreatedAt).IsDescending();
        });
    }

    // ─── Osnova: prava, sekcije, brojači, podešavanja ──────────────────────

    private static void ConfigureOsnova(ModelBuilder b)
    {
        b.Entity<KorisnikPravo>(e =>
        {
            e.ToTable("korisnik_pravo");
            e.HasKey(x => new { x.KorisnikId, x.Pravo });
            e.Property(x => x.Pravo).HasMaxLength(64);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.KorisnikId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Sekcija>(e =>
        {
            e.ToTable("sekcija");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sifra).IsRequired().HasMaxLength(8);
            e.Property(x => x.Naziv).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Sifra).IsUnique().HasDatabaseName("ux_sekcija_sifra");
            // Sekcije sa obrazaca; nema ekrana za izmenu, pa seed ide kroz migraciju.
            e.HasData(new[] { "10", "20", "21", "30", "40", "50", "60", "70" }
                .Select((s, i) => new Sekcija { Id = i + 1, Sifra = s, Naziv = $"Sekcija {s}", Redosled = i + 1 }));
        });

        b.Entity<Brojac>(e =>
        {
            e.ToTable("brojac");
            e.HasKey(x => new { x.Vrsta, x.Godina });
            e.Property(x => x.Vrsta).HasMaxLength(8);
        });

        b.Entity<Podesavanje>(e =>
        {
            e.ToTable("podesavanje");
            e.HasKey(x => x.Kljuc);
            e.Property(x => x.Kljuc).HasMaxLength(64);
            e.Property(x => x.Vrednost).IsRequired().HasColumnType("jsonb");
        });
    }

    // ─── Registar opreme, katalog aktivnosti, dodatna polja ─────────────────

    /// <summary>Fiksno vreme seed-a: HasData mora biti deterministički, a timestamptz ne prima Kind=Unspecified.</summary>
    private static readonly DateTime Seed = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

    private static void ConfigureOprema(ModelBuilder b)
    {
        b.Entity<Uredjaj>(e =>
        {
            e.ToTable("uredjaj");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Prikaz);
            e.Property(x => x.Naziv).IsRequired().HasMaxLength(200);
            e.Property(x => x.Oznaka).IsRequired().HasMaxLength(100);
            e.Property(x => x.Vrsta).IsRequired().HasMaxLength(100);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.CreatedBy).HasMaxLength(64);
            e.Property(x => x.UpdatedBy).HasMaxLength(64);
            // Identitet je par (naziv, oznaka) kako piše na obrascu (plan faze 02).
            e.HasIndex(x => new { x.Naziv, x.Oznaka }).IsUnique().HasDatabaseName("ux_uredjaj_naziv_oznaka");
            e.HasIndex(x => x.QrToken).IsUnique().HasDatabaseName("ux_uredjaj_qr_token");
            e.HasOne(x => x.Sekcija).WithMany().HasForeignKey(x => x.SekcijaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Aktivnost>(e =>
        {
            e.ToTable("aktivnost");
            e.HasKey(x => x.Id);
            e.Property(x => x.Naziv).IsRequired().HasMaxLength(300);
            e.Property(x => x.OdgovorIspravan).IsRequired().HasMaxLength(50);
            e.Property(x => x.OdgovorNeispravan).IsRequired().HasMaxLength(50);
            e.Property(x => x.UputstvoTekst).HasMaxLength(4000);
            e.Property(x => x.UputstvoAzuriraoId).HasMaxLength(64);
            e.Property(x => x.CreatedBy).HasMaxLength(64);
            e.Property(x => x.UpdatedBy).HasMaxLength(64);
            e.HasIndex(x => new { x.Naziv, x.OdgovorIspravan, x.OdgovorNeispravan }).IsUnique()
             .HasDatabaseName("ux_aktivnost_naziv_odgovori");
        });

        b.Entity<DodatnoPolje>(e =>
        {
            e.ToTable("dodatno_polje");
            e.HasKey(x => x.Id);
            e.Property(x => x.Naziv).IsRequired().HasMaxLength(200);
            e.Property(x => x.Tip).IsRequired().HasMaxLength(20);
            e.Property(x => x.Jedinica).HasMaxLength(20);
            e.Property(x => x.Prikaz).IsRequired().HasMaxLength(20);
            e.Property(x => x.CreatedBy).HasMaxLength(64);
            e.Property(x => x.UpdatedBy).HasMaxLength(64);
            e.HasIndex(x => x.Naziv).IsUnique().HasDatabaseName("ux_dodatno_polje_naziv");
            // Tri početna polja (R2.11); nazivi moraju da se poklope sa kolonom „Dodatno polje“ šablona za uvoz.
            e.HasData(
                new DodatnoPolje { Id = 1, CreatedAt = Seed, CreatedBy = "system", Naziv = "Broj rolni koje ne okreću", Tip = "broj", Jedinica = "kom.", Prikaz = "uz_neispravan" },
                new DodatnoPolje
                {
                    Id = 2, CreatedAt = Seed, CreatedBy = "system", Naziv = "Temperatura ležaja", Tip = "dve_vrednosti", Jedinica = "°C", Prikaz = "uz_neispravan",
                    Oznake = ["ležaj do rotora", "ležaj do remenice/spojnice"],
                },
                new DodatnoPolje { Id = 3, CreatedAt = Seed, CreatedBy = "system", Naziv = "Broj nedostajućih / oštećenih komada", Tip = "broj", Jedinica = "kom.", Prikaz = "uz_neispravan" });
        });
    }

    // ─── Raspored, odsustva, pregledi ──────────────────────────────────────

    private static void ConfigureRaspored(ModelBuilder b)
    {
        b.Entity<NeradniDan>(e =>
        {
            e.ToTable("neradni_dan");
            e.HasKey(x => x.Id);
            e.Property(x => x.Naziv).IsRequired().HasMaxLength(200);
            e.Property(x => x.Vrsta).IsRequired().HasMaxLength(20);
            e.Property(x => x.UneoId).HasMaxLength(64);
            e.HasIndex(x => x.Datum).IsUnique().HasDatabaseName("ux_neradni_dan_datum");
        });

        b.Entity<Odsustvo>(e =>
        {
            e.ToTable("odsustvo");
            e.HasKey(x => x.Id);
            e.Property(x => x.KorisnikId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Vrsta).IsRequired().HasMaxLength(20);
            e.Property(x => x.Napomena).HasMaxLength(500);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.ZatrazioId).HasMaxLength(64);
            e.Property(x => x.OdlucioId).HasMaxLength(64);
            e.Property(x => x.RazlogOdbijanja).HasMaxLength(500);
            e.HasIndex(x => new { x.KorisnikId, x.Od });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.KorisnikId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Pregled>(e =>
        {
            e.ToTable("pregled");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.JeDp);
            e.Property(x => x.Broj).IsRequired().HasMaxLength(20);
            e.Property(x => x.Tip).IsRequired().HasMaxLength(8);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.DodeljenId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Napomena).HasMaxLength(500);
            e.Property(x => x.UredjajKlijenta).HasMaxLength(64);
            e.Property(x => x.OverioId).HasMaxLength(64);
            e.Property(x => x.PonistioId).HasMaxLength(64);
            e.Property(x => x.PonistenRazlog).HasMaxLength(500);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Broj).IsUnique().HasDatabaseName("ux_pregled_broj");
            e.HasIndex(x => x.PlaniraniDatum);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DodeljenId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SablonVerzija>().WithMany().HasForeignKey(x => x.SablonVerzijaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PregledUcesnik>(e =>
        {
            e.ToTable("pregled_ucesnik");
            e.HasKey(x => new { x.PregledId, x.KorisnikId });
            e.Property(x => x.KorisnikId).HasMaxLength(64);
            e.HasOne<Pregled>().WithMany(p => p.Ucesnici).HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.KorisnikId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PregledDogadjaj>(e =>
        {
            e.ToTable("pregled_dogadjaj");
            e.HasKey(x => x.Id);
            e.Property(x => x.Vrsta).IsRequired().HasMaxLength(40);
            e.Property(x => x.Podaci).IsRequired().HasColumnType("jsonb");
            e.Property(x => x.KorisnikId).HasMaxLength(64);
            e.HasIndex(x => new { x.PregledId, x.At });
            e.HasOne(x => x.Pregled).WithMany().HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ZahtevPrebacivanja>(e =>
        {
            e.ToTable("zahtev_prebacivanja");
            e.HasKey(x => x.Id);
            e.Property(x => x.PodneoId).IsRequired().HasMaxLength(64);
            e.Property(x => x.PredlozenaZamenaId).HasMaxLength(64);
            e.Property(x => x.ZamenaId).HasMaxLength(64);
            e.Property(x => x.OdlucioId).HasMaxLength(64);
            e.Property(x => x.Razlog).IsRequired().HasMaxLength(500);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.HasOne<Pregled>().WithMany().HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ─── Popunjavanje ──────────────────────────────────────────────────────

    private static void ConfigurePopunjavanje(ModelBuilder b)
    {
        b.Entity<Odgovor>(e =>
        {
            e.ToTable("odgovor");
            e.HasKey(x => x.Id);
            e.Property(x => x.Napomena).HasMaxLength(2000);
            e.Property(x => x.Polja).IsRequired().HasColumnType("jsonb");
            e.Property(x => x.KorisnikId).IsRequired().HasMaxLength(64);
            e.HasIndex(x => new { x.PregledId, x.StavkaId, x.Dorada }).IsUnique().HasDatabaseName("ux_odgovor_stavka_dorada");
            e.HasOne<Pregled>().WithMany().HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SablonStavka>().WithMany().HasForeignKey(x => x.StavkaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UredjajVanFunkcije>(e =>
        {
            e.ToTable("uredjaj_van_funkcije");
            e.HasKey(x => new { x.PregledId, x.UredjajId });
            e.Property(x => x.KorisnikId).IsRequired().HasMaxLength(64);
            e.HasOne<Pregled>().WithMany().HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Uredjaj>().WithMany().HasForeignKey(x => x.UredjajId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Fotografija>(e =>
        {
            e.ToTable("fotografija");
            e.HasKey(x => x.Id);
            e.Property(x => x.Izvor).IsRequired().HasMaxLength(20);
            e.Property(x => x.KorisnikId).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.KlijentId).IsUnique().HasDatabaseName("ux_fotografija_klijent_id");
            e.HasIndex(x => new { x.PregledId, x.StavkaId });
            e.HasOne<Pregled>().WithMany().HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Fajl>().WithMany().HasForeignKey(x => x.FajlId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ZahtevDorade>(e =>
        {
            e.ToTable("zahtev_dorade");
            e.HasKey(x => x.Id);
            e.Property(x => x.PodneoId).IsRequired().HasMaxLength(64);
            e.Property(x => x.OdlucioId).HasMaxLength(64);
            e.Property(x => x.Razlog).IsRequired().HasMaxLength(500);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.HasOne<Pregled>().WithMany().HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SporanUnos>(e =>
        {
            e.ToTable("sporan_unos");
            e.HasKey(x => x.Id);
            e.Property(x => x.KorisnikId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Operacija).IsRequired().HasColumnType("jsonb");
            e.Property(x => x.Razlog).IsRequired().HasMaxLength(500);
            e.Property(x => x.Odluka).HasMaxLength(20);
            e.HasIndex(x => x.PregledId);
            e.HasOne<Pregled>().WithMany().HasForeignKey(x => x.PregledId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SinhronizacijaOperacija>(e =>
        {
            e.ToTable("sinhronizacija_operacija");
            e.HasKey(x => x.OpId);
            e.Property(x => x.Vrsta).IsRequired().HasMaxLength(40);
            e.HasIndex(x => x.PregledId);
        });
    }

    // ─── Šabloni i verzije ─────────────────────────────────────────────────

    private static void ConfigureSabloni(ModelBuilder b)
    {
        b.Entity<Sablon>(e =>
        {
            e.ToTable("sablon");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tip).IsRequired().HasMaxLength(8);
            e.HasIndex(x => x.Tip).IsUnique().HasDatabaseName("ux_sablon_tip");
            e.HasData(Sablon.Tipovi.Select((t, i) => new Sablon { Id = i + 1, Tip = t }));
        });

        b.Entity<SablonVerzija>(e =>
        {
            e.ToTable("sablon_verzija");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.ObjavioId).HasMaxLength(64);
            e.HasIndex(x => new { x.SablonId, x.Broj }).IsUnique().HasDatabaseName("ux_sablon_verzija_broj");
            e.HasOne(x => x.Sablon).WithMany().HasForeignKey(x => x.SablonId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SablonStavka>(e =>
        {
            e.ToTable("sablon_stavka");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.VerzijaId, x.UredjajId, x.AktivnostId }).IsUnique()
             .HasDatabaseName("ux_sablon_stavka_uredjaj_aktivnost");
            e.HasIndex(x => new { x.VerzijaId, x.Kljuc }).IsUnique().HasDatabaseName("ux_sablon_stavka_kljuc");
            e.HasIndex(x => x.Kljuc);
            e.HasOne(x => x.Verzija).WithMany(v => v.Stavke).HasForeignKey(x => x.VerzijaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Uredjaj).WithMany().HasForeignKey(x => x.UredjajId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Aktivnost).WithMany().HasForeignKey(x => x.AktivnostId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Sekcija>().WithMany().HasForeignKey(x => x.SekcijaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SablonStavkaPolje>(e =>
        {
            e.ToTable("sablon_stavka_polje");
            e.HasKey(x => new { x.StavkaId, x.DodatnoPoljeId });
            e.HasOne<SablonStavka>().WithMany(s => s.Polja).HasForeignKey(x => x.StavkaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DodatnoPolje).WithMany().HasForeignKey(x => x.DodatnoPoljeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SablonDogadjaj>(e =>
        {
            e.ToTable("sablon_dogadjaj");
            e.HasKey(x => x.Id);
            e.Property(x => x.Vrsta).IsRequired().HasMaxLength(40);
            e.Property(x => x.Opis).IsRequired().HasMaxLength(1000);
            e.Property(x => x.KorisnikId).HasMaxLength(64);
            e.HasIndex(x => new { x.SablonId, x.At });
            e.HasOne<Sablon>().WithMany().HasForeignKey(x => x.SablonId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Fajl>(e =>
        {
            e.ToTable("fajl");
            e.HasKey(x => x.Id);
            e.Property(x => x.Putanja).IsRequired().HasMaxLength(500);
            e.Property(x => x.Mime).IsRequired().HasMaxLength(100);
            e.Property(x => x.Sha256).IsRequired().HasMaxLength(64);
        });
    }

}
