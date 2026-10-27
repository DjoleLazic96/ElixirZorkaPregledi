using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TpmPregledi.Infrastructure.ServerStanje;

/// <summary>
/// Windows implementacija <see cref="IHardwareInfo"/>.
/// CPU: <see cref="PerformanceCounter"/> nad "% Processor Time".
/// RAM: <c>GlobalMemoryStatusEx</c> P/Invoke — vraća tačnu fizičku RAM mašine
/// (ne cgroup limit kao <c>GC.GetGCMemoryInfo</c>).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsHardwareInfo : IHardwareInfo
{
    public async Task<double> ReadCpuPercentAsync(CancellationToken ct)
    {
        // PerformanceCounter se mora „ugrejati" — prvi NextValue() uvek vraća 0.
        // Dva očitavanja sa razmakom od 500ms daju validan sistemski prosek.
        using var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        counter.NextValue();
        await Task.Delay(500, ct);
        return Math.Round(counter.NextValue(), 1);
    }

    public (long UkupnoBytes, long SlobodnoBytes) ReadRam()
    {
        var status = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(status))
            return (0, 0);
        return ((long)status.ullTotalPhys, (long)status.ullAvailPhys);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MEMORYSTATUSEX
    {
        public uint  dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
