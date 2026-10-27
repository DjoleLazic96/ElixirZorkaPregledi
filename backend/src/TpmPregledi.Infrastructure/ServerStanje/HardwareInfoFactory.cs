using System.Runtime.InteropServices;

namespace TpmPregledi.Infrastructure.ServerStanje;

/// <summary>
/// Bira <see cref="IHardwareInfo"/> implementaciju po platformi na kojoj proces
/// radi: Windows → <see cref="WindowsHardwareInfo"/>, Linux → <see cref="LinuxHardwareInfo"/>,
/// sve ostalo → <see cref="NullHardwareInfo"/>. Izdvojeno u factory da bi izbor
/// bio jedan izvor istine (koristi ga i DI registracija i test), bez čitanja
/// stvarnog hardvera pri odabiru.
/// </summary>
internal static class HardwareInfoFactory
{
    public static IHardwareInfo Napravi()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsHardwareInfo();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxHardwareInfo();
        return new NullHardwareInfo();
    }
}
