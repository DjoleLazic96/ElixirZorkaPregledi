namespace TpmPregledi.Application.Features.Auth.Dtos;

/// <summary>
/// Odgovor na uspešnu prijavu ili refresh. Tehnički ugovor — engleska imena, identičan u
/// svim aplikacijama (frontend auth kod je deljen).
/// </summary>
/// <param name="AccessToken">JWT; frontend ga drži u memoriji, nikad u storage-u.</param>
/// <param name="ExpiresAt">UTC istek access tokena (frontend radi tihi refresh 60 s ranije).</param>
/// <param name="User">Osnovni podaci o korisniku.</param>
/// <param name="Roles">Role korisnika (uvek tačno jedna, ali ugovor ostaje niz).</param>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserDto User,
    IReadOnlyList<string> Roles);

/// <summary>Sažet prikaz prijavljenog korisnika za UI (header, sidebar).</summary>
/// <param name="Id">GUID korisnika.</param>
/// <param name="UserName">Korisničko ime.</param>
/// <param name="FullName">Puno ime.</param>
/// <param name="Email">E-mail (opciono).</param>
public sealed record UserDto(
    string Id,
    string UserName,
    string FullName,
    string? Email);
