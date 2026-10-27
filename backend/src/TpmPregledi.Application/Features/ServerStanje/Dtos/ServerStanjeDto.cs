namespace TpmPregledi.Application.Features.ServerStanje.Dtos;

/// <summary>
/// Snapshot tehničkog stanja servera: <see cref="Aplikacija"/> (runtime), <see cref="Baza"/>
/// (Postgres), <see cref="Host"/> (mašina). Svaka grupa se skuplja u svom try/catch-u — pad
/// jedne ne ruši ostatak; <see cref="Host"/> je <c>null</c> kad se hardver ne može pročitati.
/// Jedinice su bajtovi i sekunde; frontend formatira.
/// </summary>
/// <param name="Aplikacija">Verzija, .NET, okruženje, uptime, vreme.</param>
/// <param name="Baza">Veza, verzija, veličina, konekcije, odziv, poslednji backup.</param>
/// <param name="Host">CPU/RAM/disk ili null.</param>
public sealed record ServerStanjeDto(
    AplikacijaDto Aplikacija,
    BazaDto Baza,
    HostDto? Host);

/// <summary>Runtime aplikacije.</summary>
/// <param name="Verzija">SemVer iz <c>IAppVersion</c>.</param>
/// <param name="GitSha">Kratki git SHA build-a.</param>
/// <param name="DotnetVerzija">Verzija .NET runtime-a.</param>
/// <param name="Okruzenje">Development / Production.</param>
/// <param name="UptimeSekundi">Sekunde od starta procesa.</param>
/// <param name="VremeUtc">Trenutno vreme servera (UTC).</param>
public sealed record AplikacijaDto(
    string Verzija,
    string GitSha,
    string DotnetVerzija,
    string Okruzenje,
    long UptimeSekundi,
    DateTime VremeUtc);

/// <summary>
/// Stanje Postgres baze. Kad veza ne radi (<c>Veza=false</c>) sva polja su null, a
/// <see cref="Greska"/> nosi <b>samo tip</b> izuzetka — poruka može da sadrži connection string.
/// </summary>
/// <param name="Veza">Da li je meta upit prošao.</param>
/// <param name="PostgresVerzija"><c>server_version</c>.</param>
/// <param name="VelicinaBazeBytes"><c>pg_database_size(current_database())</c>.</param>
/// <param name="AktivneKonekcije">Broj konekcija na ovu bazu (<c>pg_stat_activity</c>).</param>
/// <param name="OdzivMs">Trajanje meta upita u milisekundama.</param>
/// <param name="PoslednjiBackupUtc">Vreme najnovijeg <c>tpm-pregledi-*.pgcustom.gz</c> fajla u backup folderu; null ako ga nema.</param>
/// <param name="Greska">Tip izuzetka kad veza ne radi, inače null.</param>
public sealed record BazaDto(
    bool Veza,
    string? PostgresVerzija,
    long? VelicinaBazeBytes,
    int? AktivneKonekcije,
    long? OdzivMs,
    DateTime? PoslednjiBackupUtc,
    string? Greska);

/// <summary>
/// Host mašina. Disk je zbir svih fiksnih diskova. Pojedinačno polje je null kad se metrika
/// ne može pročitati (npr. RAM na nepodržanoj platformi) — nikad izmišljena nula.
/// </summary>
/// <param name="OsOpis">Opis OS-a.</param>
/// <param name="CpuProcenat">Sistemski CPU prosek 0–100 (uzorak ~500 ms).</param>
/// <param name="RamUkupnoBytes">Fizička RAM.</param>
/// <param name="RamIskoriscenoBytes">Iskorišćena RAM.</param>
/// <param name="DiskUkupnoBytes">Kapacitet fiksnih diskova.</param>
/// <param name="DiskSlobodnoBytes">Slobodno na fiksnim diskovima.</param>
public sealed record HostDto(
    string OsOpis,
    double? CpuProcenat,
    long? RamUkupnoBytes,
    long? RamIskoriscenoBytes,
    long? DiskUkupnoBytes,
    long? DiskSlobodnoBytes);
