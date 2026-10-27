using System.Globalization;
using System.Reflection;
using TpmPregledi.Application.Common.Interfaces;

namespace TpmPregledi.Infrastructure.Version;

/// <summary>
/// Čita verziju iz assembly atributa koje postavlja build (<c>Directory.Build.props</c> +
/// <c>-p:Version -p:SourceRevisionId -p:BuildUtc</c>): <c>AssemblyInformationalVersion</c>
/// (<c>"1.4.0+ab12cd3"</c>) i <c>AssemblyMetadata("BuildUtc")</c>.
/// </summary>
/// <remarks>
/// <para>
/// Čita se assembly Infrastructure sloja, ne <c>Assembly.GetEntryAssembly()</c> — pod test hostom
/// entry assembly je testhost, a svi naši projekti dobijaju istu verziju iz Directory.Build.props.
/// </para>
/// <para>
/// SDK-ov ugrađeni SourceLink stavlja <b>pun</b> 40-znakovni SHA pri lokalnom <c>dotnet run</c>,
/// Docker prosleđuje kratki — normalizujemo na 7 znakova da svuda izgleda isto.
/// </para>
/// </remarks>
public sealed class AppVersion : IAppVersion
{
    /// <summary>Fallback verzije kad build nije prosledio nijednu.</summary>
    public const string VerzijaFallback = "0.0.0-dev";

    /// <summary>Fallback SHA kad ga build nema.</summary>
    public const string GitShaFallback = "dev";

    /// <inheritdoc />
    public string Verzija { get; }

    /// <inheritdoc />
    public string GitSha { get; }

    /// <inheritdoc />
    public DateTime? BuildUtc { get; }

    /// <summary>Runtime konstruktor (DI): čita atribute ovog assembly-ja.</summary>
    public AppVersion() : this(typeof(AppVersion).Assembly) { }

    /// <summary>Čita atribute zadatog assembly-ja.</summary>
    public AppVersion(Assembly assembly) : this(
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(m => m.Key == "BuildUtc")?.Value)
    { }

    /// <summary>Čisto parsiranje — koristi ga i unit test.</summary>
    /// <param name="informationalVersion">Npr. <c>"1.2.3+abc1234"</c>, <c>"1.2.3"</c> ili null.</param>
    /// <param name="buildUtc">ISO 8601 UTC string ili null.</param>
    public AppVersion(string? informationalVersion, string? buildUtc)
    {
        (Verzija, GitSha) = Razdvoji(informationalVersion);
        BuildUtc = ParsirajUtc(buildUtc);
    }

    private static (string Verzija, string GitSha) Razdvoji(string? iv)
    {
        if (string.IsNullOrWhiteSpace(iv))
            return (VerzijaFallback, GitShaFallback);

        var plus = iv.IndexOf('+');
        if (plus < 0)
            return (iv, GitShaFallback);

        var verzija = iv[..plus];
        var sha     = iv[(plus + 1)..];
        if (string.IsNullOrWhiteSpace(sha))
            return (verzija, GitShaFallback);

        return (verzija, sha.Length > 7 ? sha[..7] : sha);
    }

    private static DateTime? ParsirajUtc(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }
}
