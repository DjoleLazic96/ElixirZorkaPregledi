namespace TpmPregledi.Infrastructure.ServerStanje;

/// <summary>
/// Fallback za platforme koje nisu Windows ni Linux (npr. macOS u razvoju). Vraća nule, koje
/// servis mapira u <c>null</c> („nedostupno“) — nikad izmišljen broj (npr. procena RAM-a iz GC
/// informacija bi dala cgroup limit, ne stvarnu memoriju mašine).
/// </summary>
internal sealed class NullHardwareInfo : IHardwareInfo
{
    public Task<double> ReadCpuPercentAsync(CancellationToken ct) => Task.FromResult(0d);

    public (long UkupnoBytes, long SlobodnoBytes) ReadRam() => (0, 0);
}
