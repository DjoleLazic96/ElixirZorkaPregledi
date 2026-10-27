namespace TpmPregledi.Application.Features.Audit.Dtos;

/// <summary>
/// Jedan red audit loga za admin stranicu „Audit log“. Vrednosti su sirovi JSON stringovi —
/// redovi su mali, pa nema zasebnog detalj-endpointa; frontend računa diff.
/// </summary>
/// <param name="Id">PK reda.</param>
/// <param name="Vreme">UTC trenutak događaja.</param>
/// <param name="Korisnik">Korisničko ime (snapshot) ili <c>system</c>; null za neuspešnu prijavu bez korisnika.</param>
/// <param name="Akcija">CREATE / UPDATE / DELETE / LOGIN / LOGIN_FAIL / LOGOUT / START.</param>
/// <param name="Entitet">CLR ime entiteta ili <c>Auth</c> / <c>Sistem</c>.</param>
/// <param name="EntitetLabela">Srpska labela entiteta (backend je izvor istine, vidi <see cref="EntitetLabele"/>).</param>
/// <param name="EntitetId">PK izmenjenog entiteta kao string (null za auth događaje).</param>
/// <param name="Ip">IP adresa klijenta.</param>
/// <param name="StareVrednosti">Sirovi JSON pre izmene (null za CREATE i događaje).</param>
/// <param name="NoveVrednosti">Sirovi JSON posle izmene (null za DELETE i auth događaje).</param>
public sealed record AuditRedDto(
    long Id,
    DateTime Vreme,
    string? Korisnik,
    string Akcija,
    string Entitet,
    string EntitetLabela,
    string? EntitetId,
    string? Ip,
    string? StareVrednosti,
    string? NoveVrednosti);

/// <summary>Stavka za filter „Entitet“ na audit stranici.</summary>
/// <param name="Entitet">Vrednost koja se šalje kao <c>?entitet=</c>.</param>
/// <param name="Labela">Tekst za prikaz, sa dijakritikama.</param>
public sealed record EntitetLabelaDto(string Entitet, string Labela);
