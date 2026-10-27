using System.Runtime.InteropServices;
using FluentAssertions;
using TpmPregledi.Infrastructure.ServerStanje;

namespace TpmPregledi.Application.UnitTests.ServerStanje;

/// <summary>
/// Proverava <see cref="IHardwareInfo"/> implementacije i izbor po platformi.
/// <see cref="NullHardwareInfo"/> vraća prazne/nulte vrednosti (nikad ne izmišlja
/// broj — servis ih mapira u „nedostupno"); factory bira Windows/Linux/Null po
/// <see cref="RuntimeInformation"/> BEZ stvarnog čitanja hardvera.
/// </summary>
public class HardwareInfoTests
{
    [Fact]
    public async Task NullHardwareInfo_vraca_nulu_za_cpu()
    {
        var hw = new NullHardwareInfo();

        var cpu = await hw.ReadCpuPercentAsync(CancellationToken.None);

        cpu.Should().Be(0d);
    }

    [Fact]
    public void NullHardwareInfo_vraca_nule_za_ram()
    {
        var hw = new NullHardwareInfo();

        var (ukupno, slobodno) = hw.ReadRam();

        ukupno.Should().Be(0);
        slobodno.Should().Be(0);
    }

    [Fact]
    public void Izbor_implementacije_po_platformi_daje_ispravan_tip()
    {
        // Ista logika kao u DependencyInjection: Windows→Windows, Linux→Linux,
        // ostalo→Null. Testira izbor BEZ pokretanja stvarnog čitanja hardvera.
        IHardwareInfo hw = HardwareInfoFactory.Napravi();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            hw.Should().BeOfType<WindowsHardwareInfo>();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            hw.Should().BeOfType<LinuxHardwareInfo>();
        else
            hw.Should().BeOfType<NullHardwareInfo>();
    }
}
