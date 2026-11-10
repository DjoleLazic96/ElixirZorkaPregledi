using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Identity;

/// <summary>Jedno štiklirano pravo korisnika (PK: korisnik + pravo). Vrednosti iz <see cref="Prava"/>.</summary>
[Auditable]
public class KorisnikPravo
{
    /// <summary>GUID korisnika (<c>AspNetUsers.id</c>).</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>Pravo, npr. <c>overa</c>.</summary>
    public string Pravo { get; set; } = string.Empty;
}
