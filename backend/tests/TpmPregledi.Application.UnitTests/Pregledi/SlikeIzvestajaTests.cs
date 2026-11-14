using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TpmPregledi.Infrastructure.Izvestaji;

namespace TpmPregledi.Application.UnitTests.Pregledi;

/// <summary>Zamerka 72: slike u PDF-u i Word-u se umanjuju na sličice; nečitljiva slika se preskače.</summary>
public class SlikeIzvestajaTests
{
    static SlikeIzvestajaTests() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>Probna slika kakvu šalje telefon (JPEG ili PNG zadate veličine u pikselima).</summary>
    public static byte[] Slika(int sirina, int visina, ImageFormat format = ImageFormat.Jpeg) =>
        Document.Create(d => d.Page(p =>
        {
            p.Size(new PageSize(sirina * 72f / 96, visina * 72f / 96));
            p.Margin(0);
            p.Content().Background(Colors.Green.Medium).Padding(20).Background(Colors.Red.Medium);
        })).GenerateImages(new ImageGenerationSettings { ImageFormat = format, RasterDpi = 96 }).First();

    [Fact]
    public void Dimenzije_se_citaju_iz_zaglavlja_jpeg_i_png()
    {
        SlikeIzvestaja.Dimenzije(Slika(800, 600)).Should().Be((800, 600));
        SlikeIzvestaja.Dimenzije(Slika(300, 500, ImageFormat.Png)).Should().Be((300, 500));
        SlikeIzvestaja.Dimenzije([1, 2, 3]).Should().BeNull();
    }

    [Fact]
    public void Velika_slika_postaje_mala_sa_istim_odnosom_stranica()
    {
        var original = Slika(2048, 1536);
        var mala = SlikeIzvestaja.Umanji(original)!;
        mala.Sirina.Should().Be(480);
        mala.Visina.Should().Be(360);
        mala.Jpeg.Length.Should().BeLessThan(original.Length);
        SlikeIzvestaja.Dimenzije(mala.Jpeg).Should().Be((480, 360));
    }

    [Fact]
    public void Necitljiva_slika_se_preskace()
    {
        SlikeIzvestaja.Umanji([0xFF, 0xD8, 0xFF, 0x00, 0x01]).Should().BeNull();
    }
}
