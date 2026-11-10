using System.Security.Cryptography;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Application.Features.Popunjavanje;
using TpmPregledi.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace TpmPregledi.Infrastructure.Fajlovi;

/// <summary>
/// Fajlovi na volumenu (<c>Fajlovi:Putanja</c>, podrazumevano <c>data/fotografije</c>). Putanja u bazi je relativna
/// (<c>2026/11/&lt;guid&gt;.jpg</c>), pa se volumen može premestiti bez izmene baze.
/// </summary>
public sealed class FajlSkladiste(IConfiguration config, IDateTimeProvider clock) : IFajlSkladiste
{
    private string Koren => Path.GetFullPath(config["Fajlovi:Putanja"] ?? Path.Combine("data", "fotografije"));

    /// <inheritdoc />
    public async Task<Fajl> SacuvajAsync(byte[] sadrzaj, string mime, CancellationToken ct)
    {
        var sada = clock.UtcNow;
        var ekstenzija = mime switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
        var relativna = Path.Combine(sada.ToString("yyyy"), sada.ToString("MM"), Guid.NewGuid().ToString("N") + ekstenzija);
        var puna = Path.Combine(Koren, relativna);
        Directory.CreateDirectory(Path.GetDirectoryName(puna)!);
        await File.WriteAllBytesAsync(puna, sadrzaj, ct);

        return new Fajl
        {
            Putanja = relativna.Replace('\\', '/'), Mime = mime, Velicina = sadrzaj.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(sadrzaj)), KreiranoAt = sada,
        };
    }

    /// <inheritdoc />
    public Stream Otvori(Fajl fajl)
    {
        var puna = Path.GetFullPath(Path.Combine(Koren, fajl.Putanja));
        // Putanja dolazi iz baze, ali provera ostaje: čitanje van korena nikad.
        if (!puna.StartsWith(Koren, StringComparison.Ordinal)) throw new InvalidOperationException("Neispravna putanja fajla.");
        return File.OpenRead(puna);
    }
}
