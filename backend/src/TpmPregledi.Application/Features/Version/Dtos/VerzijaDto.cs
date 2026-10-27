namespace TpmPregledi.Application.Features.Version.Dtos;

/// <summary>
/// Odgovor <c>GET /api/v1/version</c> — dijalog „O aplikaciji“ i deploy smoke test. Frontend
/// poredi <see cref="Verzija"/> sa svojom i upozorava kad se razlikuju (stari bundle u kešu).
/// </summary>
/// <param name="Verzija">SemVer, npr. <c>1.4.0</c>.</param>
/// <param name="GitSha">Kratki git SHA.</param>
/// <param name="BuildUtc">UTC trenutak build-a ili null.</param>
/// <param name="Okruzenje">Development / Production.</param>
public sealed record VerzijaDto(
    string Verzija,
    string GitSha,
    DateTime? BuildUtc,
    string Okruzenje);
