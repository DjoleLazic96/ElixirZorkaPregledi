using FluentAssertions;
using TpmPregledi.Application.Common;

namespace TpmPregledi.Application.UnitTests.Common;

/// <summary>Srpska množina u porukama (ručno testiranje: „43 stavki“, „3 izmena“, „u 5 dela“).</summary>
public class MnozinaTests
{
    [Theory]
    [InlineData(1, "1 stavka")]
    [InlineData(3, "3 stavke")]
    [InlineData(5, "5 stavki")]
    [InlineData(12, "12 stavki")]
    [InlineData(21, "21 stavka")]
    [InlineData(43, "43 stavke")]
    [InlineData(111, "111 stavki")]
    public void Bira_oblik_po_srpskom_pravilu(int n, string ocekivano) =>
        Mnozina.Od(n, "stavka", "stavke", "stavki").Should().Be(ocekivano);
}
