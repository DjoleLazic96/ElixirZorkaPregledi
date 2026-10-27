using FluentAssertions;
using TpmPregledi.Application.Common.Pagination;

namespace TpmPregledi.Application.UnitTests.Common;

/// <summary>Normalizacija paginacije: stranica ≥ 1, veličina u [1, 200] sa default 50.</summary>
public class StranicaDefaultsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void Stranica_manja_od_1_postaje_1(int ulaz, int ocekivano)
        => StranicaDefaults.Normalizuj(ulaz, 50).Stranica.Should().Be(ocekivano);

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(25, 25)]
    [InlineData(200, 200)]
    [InlineData(5000, 200)]
    public void Velicina_se_normalizuje_na_default_ili_max(int ulaz, int ocekivano)
        => StranicaDefaults.Normalizuj(1, ulaz).Velicina.Should().Be(ocekivano);

    [Fact]
    public void Konstante_su_50_i_200()
    {
        StranicaDefaults.Default.Should().Be(50);
        StranicaDefaults.Max.Should().Be(200);
    }
}
