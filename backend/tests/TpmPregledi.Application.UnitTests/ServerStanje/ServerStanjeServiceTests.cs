using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TpmPregledi.Application.UnitTests.Common;
using TpmPregledi.Application.UnitTests.Common.Fakes;
using TpmPregledi.Infrastructure.Persistence;
using TpmPregledi.Infrastructure.ServerStanje;
using TpmPregledi.Infrastructure.Version;

namespace TpmPregledi.Application.UnitTests.ServerStanje;

/// <summary>
/// ServerStanjeService sa fake hardverom i InMemory bazom: app grupa iz IAppVersion, host grupa iz
/// fake-a, DB grupa graceful (veza=false, samo tip greške), backup folder (null kad ga nema),
/// greška u jednoj grupi ne ruši ostatak.
/// </summary>
public class ServerStanjeServiceTests
{
    private sealed class FakeHardwareInfo(double cpu, long ramUkupno, long ramSlobodno) : IHardwareInfo
    {
        public Task<double> ReadCpuPercentAsync(CancellationToken ct) => Task.FromResult(cpu);
        public (long UkupnoBytes, long SlobodnoBytes) ReadRam() => (ramUkupno, ramSlobodno);
    }

    private sealed class ThrowingHardwareInfo : IHardwareInfo
    {
        public Task<double> ReadCpuPercentAsync(CancellationToken ct) => throw new InvalidOperationException("hw");
        public (long UkupnoBytes, long SlobodnoBytes) ReadRam() => throw new InvalidOperationException("hw");
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "TpmPregledi.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static IConfiguration Config(string? backupPath = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Backup:Path"] = backupPath ?? Path.Combine(Path.GetTempPath(), "nema-ovog-foldera-" + Guid.NewGuid()),
        }).Build();

    private static ServerStanjeService Sut(AppDbContext ctx, IHardwareInfo hw, string? backupPath = null) =>
        new(ctx, hw, new FakeHostEnvironment(), new FakeClock(), new AppVersion("1.2.3+abc1234", null), Config(backupPath));

    [Fact]
    public async Task App_grupa_dolazi_iz_IAppVersion_i_sata()
    {
        await using var ctx = InMemoryDb.New();
        var dto = await Sut(ctx, new FakeHardwareInfo(12.5, 16L << 30, 8L << 30)).DajStanjeAsync(default);

        dto.Aplikacija.Verzija.Should().Be("1.2.3");
        dto.Aplikacija.GitSha.Should().Be("abc1234");
        dto.Aplikacija.Okruzenje.Should().Be("Testing");
        dto.Aplikacija.DotnetVerzija.Should().NotBeNullOrWhiteSpace();
        dto.Aplikacija.UptimeSekundi.Should().BeGreaterThanOrEqualTo(0);
        dto.Aplikacija.VremeUtc.Should().Be(FakeClock.Podrazumevano);
    }

    [Fact]
    public async Task Host_grupa_dolazi_iz_fake_hardvera_a_nula_postaje_null()
    {
        await using var ctx = InMemoryDb.New();
        var ukupno = 16L << 30;
        var dto = await Sut(ctx, new FakeHardwareInfo(12.5, ukupno, 8L << 30)).DajStanjeAsync(default);

        dto.Host.Should().NotBeNull();
        dto.Host!.CpuProcenat.Should().Be(12.5);
        dto.Host.RamUkupnoBytes.Should().Be(ukupno);
        dto.Host.RamIskoriscenoBytes.Should().Be(ukupno - (8L << 30));

        var nula = await Sut(ctx, new FakeHardwareInfo(5, 0, 0)).DajStanjeAsync(default);
        nula.Host!.RamUkupnoBytes.Should().BeNull("0 iz hardvera znači „nedostupno“, ne 0 bajtova");
        nula.Host.RamIskoriscenoBytes.Should().BeNull();
    }

    [Fact]
    public async Task DB_grupa_je_graceful_kad_konekcija_nije_postgres_i_nosi_samo_tip_greske()
    {
        await using var ctx = InMemoryDb.New();
        var dto = await Sut(ctx, new FakeHardwareInfo(5, 1024, 512)).DajStanjeAsync(default);

        dto.Baza.Veza.Should().BeFalse();
        dto.Baza.PostgresVerzija.Should().BeNull();
        dto.Baza.VelicinaBazeBytes.Should().BeNull();
        dto.Baza.AktivneKonekcije.Should().BeNull();
        dto.Baza.OdzivMs.Should().BeNull();
        dto.Baza.Greska.Should().NotBeNullOrWhiteSpace().And.NotContain(" ", "samo ime tipa, ne poruka");
    }

    [Fact]
    public async Task Backup_je_null_kad_folder_ne_postoji_a_inace_vreme_najnovijeg_fajla()
    {
        await using var ctx = InMemoryDb.New();

        (await Sut(ctx, new FakeHardwareInfo(5, 1, 1)).DajStanjeAsync(default)).Baza.PoslednjiBackupUtc.Should().BeNull();

        var folder = Path.Combine(Path.GetTempPath(), "tpm-pregledi-backup-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            // Prefiks iz same konstante, da test preživi preimenovanje projekta (preimenuj.ps1 menja slug).
            var prefiks = ServerStanjeService.BackupPattern.Split('-')[0];
            var stariji = Path.Combine(folder, $"{prefiks}-2026-10-01.pgcustom.gz");
            var noviji  = Path.Combine(folder, $"{prefiks.ToUpperInvariant()}-2026-10-02.pgcustom.gz");   // case-insensitive
            File.WriteAllText(stariji, "x");
            File.WriteAllText(noviji, "y");
            File.WriteAllText(Path.Combine(folder, "nesto-drugo.txt"), "z");
            File.SetLastWriteTimeUtc(stariji, new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(noviji,  new DateTime(2026, 10, 2, 3, 0, 0, DateTimeKind.Utc));

            var dto = await Sut(ctx, new FakeHardwareInfo(5, 1, 1), folder).DajStanjeAsync(default);

            dto.Baza.PoslednjiBackupUtc.Should().Be(new DateTime(2026, 10, 2, 3, 0, 0, DateTimeKind.Utc));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Greska_u_host_grupi_ne_rusi_ostatak()
    {
        await using var ctx = InMemoryDb.New();
        var dto = await Sut(ctx, new ThrowingHardwareInfo()).DajStanjeAsync(default);

        dto.Host.Should().BeNull("host grana je bacila → grupa nedostupna");
        dto.Aplikacija.Verzija.Should().Be("1.2.3");
        dto.Baza.Should().NotBeNull();
    }
}
