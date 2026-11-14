using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TpmPregledi.Infrastructure.Izvestaji;

/// <summary>Umanjena slika za izveštaj: JPEG i dimenzije u pikselima (za odnos stranica u Word-u).</summary>
/// <param name="Jpeg">Sadržaj.</param>
/// <param name="Sirina">Širina u pikselima.</param>
/// <param name="Visina">Visina u pikselima.</param>
public sealed record SlikaIzvestaja(byte[] Jpeg, int Sirina, int Visina);

/// <summary>
/// Slike u PDF-u i Word-u (zamerka 72). Sa telefona stižu do 2048 px i 2 MB, pa bi izveštaj sa desetak slika imao desetine MB;
/// zato se svaka umanji na sličicu (duža strana ~480 px, JPEG srednjeg kvaliteta) — dovoljno da se vidi šta je slikano na
/// odštampanom izveštaju. Umanjuje QuestPDF (već je u projektu, ume JPG, PNG i WebP), pa ne treba nova biblioteka. Slika koja ne
/// može da se pročita se preskače — izveštaj se ne ruši zbog jedne loše slike.
/// </summary>
public static class SlikeIzvestaja
{
    /// <summary>Duža strana sličice u tačkama PDF-a (72 po inču); uz 144 dpi to je oko 480 px.</summary>
    private const float DuzaStrana = 240;

    /// <summary>Umanjuje sliku ili vraća null ako nije čitljiva.</summary>
    public static SlikaIzvestaja? Umanji(byte[] original)
    {
        try
        {
            var (w, h) = Dimenzije(original) ?? (4, 3);
            var k = DuzaStrana / Math.Max(w, h);
            var (sirina, visina) = (Math.Max(1f, w * k), Math.Max(1f, h * k));
            var slike = Document.Create(d => d.Page(p =>
            {
                p.Size(new PageSize(sirina, visina));
                p.Margin(0);
                p.Content().Image(original).FitUnproportionally();
            })).GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Jpeg, ImageCompressionQuality = ImageCompressionQuality.Medium, RasterDpi = 144 });
            var jpeg = slike.First();
            return new SlikaIzvestaja(jpeg, (int)Math.Round(sirina * 2), (int)Math.Round(visina * 2));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Dimenzije iz zaglavlja JPEG-a, PNG-a ili WebP-a (bez dekodiranja cele slike); null ako format nije prepoznat.</summary>
    public static (int Sirina, int Visina)? Dimenzije(byte[] b)
    {
        // PNG: IHDR odmah posle potpisa.
        if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            return (BigEndian(b, 16), BigEndian(b, 20));
        // JPEG: prvi SOFn marker nosi visinu i širinu.
        if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var m = b[i + 1];
                if (m is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                    return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                if (m is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { i += 2; continue; }
                i += 2 + ((b[i + 2] << 8) | b[i + 3]);
            }
            return null;
        }
        // WebP (VP8X, VP8L, VP8).
        if (b.Length > 30 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            var vrsta = System.Text.Encoding.ASCII.GetString(b, 12, 4);
            return vrsta switch
            {
                "VP8X" => (1 + (b[24] | (b[25] << 8) | (b[26] << 16)), 1 + (b[27] | (b[28] << 8) | (b[29] << 16))),
                "VP8L" => (1 + (b[21] | ((b[22] & 0x3F) << 8)), 1 + ((b[22] >> 6) | (b[23] << 2) | ((b[24] & 0x0F) << 10))),
                "VP8 " => (b[26] | ((b[27] & 0x3F) << 8), b[28] | ((b[29] & 0x3F) << 8)),
                _ => null,
            };
        }
        return null;
    }

    private static int BigEndian(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
}
