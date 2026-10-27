using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.ServerStanje.Dtos;
using TpmPregledi.Infrastructure.Persistence;

namespace TpmPregledi.Infrastructure.ServerStanje;

/// <summary>
/// Implementacija <see cref="IServerStanjeService"/>: tri nezavisne grupe, svaka u svom try/catch-u —
/// cela poenta stranice je da admin može da je otvori i KAD nešto ne radi.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Aplikacija — verzija/SHA iz <see cref="IAppVersion"/>, .NET, okruženje, uptime, vreme</item>
///   <item>Baza — <c>server_version</c>, <c>pg_database_size</c>, broj konekcija jednim raw upitom; odziv Stopwatch-om;
///         poslednji backup iz foldera <c>Backup:Path</c></item>
///   <item>Host — CPU/RAM preko <see cref="IHardwareInfo"/>, disk preko <see cref="DriveInfo"/></item>
/// </list>
/// Injektuje konkretan <see cref="AppDbContext"/> (jedini dozvoljeni izuzetak od „samo IAppDbContext“)
/// jer mu treba <c>Database.GetDbConnection()</c> za raw SQL.
/// </remarks>
internal sealed class ServerStanjeService(
    AppDbContext db,
    IHardwareInfo hw,
    IHostEnvironment hostEnv,
    IDateTimeProvider clock,
    IAppVersion appVersion,
    IConfiguration config) : IServerStanjeService
{
    /// <summary>Obrazac imena backup fajla koji pravi <c>backup.sh</c>.</summary>
    internal const string BackupPattern = "tpm-pregledi-*.pgcustom.gz";

    /// <inheritdoc />
    public async Task<ServerStanjeDto> DajStanjeAsync(CancellationToken ct)
    {
        var aplikacija = ReadAplikacija();
        var baza       = await ReadBazaAsync(ct);
        var host       = await ReadHostAsync(ct);

        return new ServerStanjeDto(aplikacija, baza, host);
    }

    // ─── Aplikacija ──────────────────────────────────────────────────────────

    private AplikacijaDto ReadAplikacija()
    {
        long uptime = 0;
        try
        {
            using var p = Process.GetCurrentProcess();
            uptime = (long)(clock.UtcNow - p.StartTime.ToUniversalTime()).TotalSeconds;
        }
        catch
        {
            // Process.StartTime može da baci u ograničenim okruženjima — uptime ostaje 0.
        }

        return new AplikacijaDto(
            Verzija:       appVersion.Verzija,
            GitSha:        appVersion.GitSha,
            DotnetVerzija: Environment.Version.ToString(),
            Okruzenje:     hostEnv.EnvironmentName,
            UptimeSekundi: Math.Max(0, uptime),
            VremeUtc:      clock.UtcNow);
    }

    // ─── Baza ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sve izuzetke iz DB grane pretvara u <c>Veza=false</c> + <b>samo tip</b> izuzetka —
    /// <c>ex.Message</c> može da sadrži connection string sa kredencijalima (Npgsql ga uključuje
    /// pri „Failed to connect“). Pun stack je u Serilog logu.
    /// </summary>
    private async Task<BazaDto> ReadBazaAsync(CancellationToken ct)
    {
        var backup = ReadPoslednjiBackup();

        try
        {
            var conn = db.Database.GetDbConnection();

            // Otvaramo vezu samo ako je zatvorena i zatvaramo samo ako smo je mi otvorili —
            // da je ne zatvorimo ispod nogu EF-u ako je već drži.
            var wasClosed = conn.State != ConnectionState.Open;
            if (wasClosed)
                await conn.OpenAsync(ct);

            try
            {
                var sw = Stopwatch.StartNew();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT current_setting('server_version') AS verzija, " +
                    "pg_database_size(current_database())::bigint AS velicina_bytes, " +
                    "(SELECT count(*) FROM pg_stat_activity WHERE datname = current_database())::int AS konekcije";

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Postgres meta upit nije vratio red.");

                var verzija   = reader.GetString(0);
                var velicina  = reader.GetInt64(1);
                var konekcije = reader.GetInt32(2);
                sw.Stop();

                return new BazaDto(
                    Veza: true, PostgresVerzija: verzija, VelicinaBazeBytes: velicina,
                    AktivneKonekcije: konekcije, OdzivMs: sw.ElapsedMilliseconds,
                    PoslednjiBackupUtc: backup, Greska: null);
            }
            finally
            {
                if (wasClosed)
                    await conn.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            return new BazaDto(
                Veza: false, PostgresVerzija: null, VelicinaBazeBytes: null,
                AktivneKonekcije: null, OdzivMs: null,
                PoslednjiBackupUtc: backup, Greska: ex.GetType().Name);
        }
    }

    /// <summary>
    /// Vreme najnovijeg <see cref="BackupPattern"/> fajla u <c>Backup:Path</c> (default <c>backups</c>).
    /// Null ako folder ne postoji, nije čitljiv (kontejnerski korisnik bez prava) ili je prazan —
    /// bilo koji izuzetak znači „nema podatka“, ne grešku stranice.
    /// </summary>
    private DateTime? ReadPoslednjiBackup()
    {
        try
        {
            var folder = config["Backup:Path"] ?? "backups";
            if (!Directory.Exists(folder)) return null;

            var najnoviji = new DirectoryInfo(folder)
                .EnumerateFiles(BackupPattern, new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                .Select(f => f.LastWriteTimeUtc)
                .DefaultIfEmpty()
                .Max();

            return najnoviji == default ? null : najnoviji;
        }
        catch
        {
            return null;
        }
    }

    // ─── Host ────────────────────────────────────────────────────────────────

    /// <summary>Ako čitanje hardvera padne, cela host grupa je <c>null</c>; ostatak snapshot-a ostaje.</summary>
    private async Task<HostDto?> ReadHostAsync(CancellationToken ct)
    {
        try
        {
            var cpu = await hw.ReadCpuPercentAsync(ct);
            var (ramUkupno, ramSlobodno) = hw.ReadRam();

            // Nula iz IHardwareInfo znači „nije pročitano“ → null, ne 0.
            long? ramUkupnoOut   = ramUkupno > 0 ? ramUkupno : null;
            long? ramIskorisceno = ramUkupno > 0 ? Math.Max(0, ramUkupno - ramSlobodno) : null;

            var (diskUkupno, diskSlobodno) = ReadDisk();

            return new HostDto(
                OsOpis:              RuntimeInformation.OSDescription,
                CpuProcenat:         cpu,
                RamUkupnoBytes:      ramUkupnoOut,
                RamIskoriscenoBytes: ramIskorisceno,
                DiskUkupnoBytes:     diskUkupno,
                DiskSlobodnoBytes:   diskSlobodno);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Zbir svih spremnih fiksnih diskova; <c>(null, null)</c> ako nijedan nije čitljiv.</summary>
    private static (long? Ukupno, long? Slobodno) ReadDisk()
    {
        long ukupno = 0, slobodno = 0;
        var imaDisk = false;

        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                ukupno   += d.TotalSize;
                slobodno += d.AvailableFreeSpace;
                imaDisk = true;
            }
            catch
            {
                // Pojedini disk može da baci (odmontiran u međuvremenu) — preskoči.
            }
        }

        return imaDisk ? (ukupno, slobodno) : (null, null);
    }
}
