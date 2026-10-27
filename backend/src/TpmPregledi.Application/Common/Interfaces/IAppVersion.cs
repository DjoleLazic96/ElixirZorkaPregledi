namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Verzija aplikacije iz jednog izvora (<c>VERSION</c> fajl + git SHA ugrađeni u assembly pri
/// build-u). Koriste je <c>GET /api/v1/version</c>, start log, audit <c>START</c> i stanje servera —
/// nikad se ne upisuje ručno na više mesta.
/// </summary>
public interface IAppVersion
{
    /// <summary>SemVer verzija, npr. <c>1.4.0</c>; <c>0.0.0-dev</c> kad build nema verziju.</summary>
    string Verzija { get; }

    /// <summary>Kratki git SHA (7 znakova), npr. <c>ab12cd3</c>; <c>dev</c> kad ga build nema.</summary>
    string GitSha { get; }

    /// <summary>UTC trenutak build-a; null kad build nije prosledio <c>BuildUtc</c>.</summary>
    DateTime? BuildUtc { get; }
}
