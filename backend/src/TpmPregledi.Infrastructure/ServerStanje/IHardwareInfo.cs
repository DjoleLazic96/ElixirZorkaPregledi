namespace TpmPregledi.Infrastructure.ServerStanje;

/// <summary>
/// Cross-platform čitanje sistemskih hardware metrika koje .NET API ne izlaže
/// uniformno (CPU iskorišćenje, fizička RAM mašine). Disk se NAMERNO ne čita
/// ovde — čita se <c>DriveInfo</c>-om unutar <see cref="ServerStanjeService"/>.
///
/// Koristi se kroz DI; konkretna implementacija se bira po platformi u
/// <see cref="HardwareInfoFactory.Napravi"/> (Windows / Linux / Null fallback).
/// Interfejs je <c>internal</c> jer je detalj Infrastructure sloja — Application
/// vidi samo <c>IServerStanjeService</c>.
/// </summary>
internal interface IHardwareInfo
{
    /// <summary>Sistemski CPU prosek (preko svih jezgara), 0–100. Sample ~500ms.</summary>
    Task<double> ReadCpuPercentAsync(CancellationToken ct);

    /// <summary>Vraća (ukupna fizička RAM, slobodna) u bajtima. (0, 0) ako se ne može pročitati.</summary>
    (long UkupnoBytes, long SlobodnoBytes) ReadRam();
}
