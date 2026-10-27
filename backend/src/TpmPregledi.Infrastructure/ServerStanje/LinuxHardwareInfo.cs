using System.Globalization;
using System.Runtime.Versioning;

namespace TpmPregledi.Infrastructure.ServerStanje;

/// <summary>
/// Linux implementacija <see cref="IHardwareInfo"/>.
/// CPU: dva uzastopna čitanja <c>/proc/stat</c> sa razmakom 500ms — razlika
/// busy/total preko sample perioda daje sistemski CPU procenat.
/// RAM: parsiranje <c>/proc/meminfo</c> (<c>MemTotal</c>, <c>MemAvailable</c>).
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxHardwareInfo : IHardwareInfo
{
    public async Task<double> ReadCpuPercentAsync(CancellationToken ct)
    {
        var (idle1, total1) = ReadCpuTimes();
        await Task.Delay(500, ct);
        var (idle2, total2) = ReadCpuTimes();

        var idleDelta  = idle2  - idle1;
        var totalDelta = total2 - total1;
        if (totalDelta == 0) return 0;

        var usage = 100.0 * (1.0 - (double)idleDelta / totalDelta);
        return Math.Round(Math.Clamp(usage, 0, 100), 1);
    }

    public (long UkupnoBytes, long SlobodnoBytes) ReadRam()
    {
        long memTotalKb = 0, memAvailableKb = 0;
        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemTotal:"))          memTotalKb     = ParseKb(line);
            else if (line.StartsWith("MemAvailable:")) memAvailableKb = ParseKb(line);
            if (memTotalKb > 0 && memAvailableKb > 0) break;
        }
        return (memTotalKb * 1024, memAvailableKb * 1024);
    }

    private static long ParseKb(string line)
    {
        // Format: "MemTotal:       16323452 kB"
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return long.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    private static (long Idle, long Total) ReadCpuTimes()
    {
        // Prvi red /proc/stat: "cpu  user nice system idle iowait irq softirq steal guest guest_nice"
        var first = File.ReadLines("/proc/stat").First();
        var parts = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        long sum = 0;
        for (int i = 1; i < parts.Length; i++)
            sum += long.Parse(parts[i], CultureInfo.InvariantCulture);

        var idle = long.Parse(parts[4], CultureInfo.InvariantCulture);
        return (idle, sum);
    }
}
