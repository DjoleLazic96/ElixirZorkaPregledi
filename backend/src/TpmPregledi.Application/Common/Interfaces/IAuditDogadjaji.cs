namespace TpmPregledi.Application.Common.Interfaces;

/// <summary>
/// Beleži događaje koji nisu izmena entiteta (prijava, odjava, start aplikacije) u
/// <c>audit_log</c> — jedinstven pregled sa izmenama podataka. Implementira ga
/// <c>AuditDogadjaji</c> u Infrastructure sloju.
/// </summary>
/// <remarks>
/// Sve metode su <b>best-effort</b>: greška pri upisu se loguje (Warning) i guta, nikad ne
/// propagira — beleženje ne sme da obori prijavu ni start. Zato ih handleri zovu bez try/catch-a.
/// </remarks>
public interface IAuditDogadjaji
{
    /// <summary>Uspešna prijava: <c>Action="LOGIN"</c>, <c>Entity="Auth"</c>.</summary>
    /// <param name="userId">ID prijavljenog korisnika.</param>
    /// <param name="userName">Korisničko ime (snapshot).</param>
    /// <param name="ip">IP adresa klijenta (može biti null).</param>
    /// <param name="ct">Token za otkazivanje.</param>
    Task PrijavaUspehAsync(string userId, string userName, string? ip, CancellationToken ct = default);

    /// <summary>
    /// Neuspešna prijava: <c>Action="LOGIN_FAIL"</c>, <c>Entity="Auth"</c>, <c>UserId=null</c>.
    /// Klijent dobija istu 401 poruku bez obzira na razlog; interno čuvamo uneto ime radi admin uvida.
    /// </summary>
    /// <param name="korisnickoIme">Uneto korisničko ime.</param>
    /// <param name="ip">IP adresa klijenta (može biti null).</param>
    /// <param name="ct">Token za otkazivanje.</param>
    Task PrijavaNeuspehAsync(string korisnickoIme, string? ip, CancellationToken ct = default);

    /// <summary>Odjava: <c>Action="LOGOUT"</c>, <c>Entity="Auth"</c>.</summary>
    /// <param name="userId">ID korisnika (null ako token nije prisutan).</param>
    /// <param name="userName">Korisničko ime (snapshot, može biti null).</param>
    /// <param name="ip">IP adresa (može biti null).</param>
    /// <param name="ct">Token za otkazivanje.</param>
    Task OdjavaAsync(string? userId, string? userName, string? ip, CancellationToken ct = default);

    /// <summary>
    /// Start aplikacije: <c>Action="START"</c>, <c>Entity="Sistem"</c>, <c>UserName="system"</c>,
    /// <c>NewValues = { verzija, gitSha, okruzenje }</c>. Daje istoriju deploy-a u samoj bazi.
    /// </summary>
    /// <param name="verzija">Verzija aplikacije (<c>IAppVersion.Verzija</c>).</param>
    /// <param name="gitSha">Git SHA build-a.</param>
    /// <param name="okruzenje">Ime okruženja (Development/Production).</param>
    /// <param name="ct">Token za otkazivanje.</param>
    Task StartAsync(string verzija, string gitSha, string okruzenje, CancellationToken ct = default);
}
