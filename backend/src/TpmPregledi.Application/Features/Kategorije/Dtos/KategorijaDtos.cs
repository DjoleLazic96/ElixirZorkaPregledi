using TpmPregledi.Domain.Entities;

namespace TpmPregledi.Application.Features.Kategorije.Dtos;

/// <summary>PRIMER feature-a — kopiraj ili obriši. Prikaz kategorije.</summary>
/// <param name="Id">PK.</param>
/// <param name="Sifra">Šifra.</param>
/// <param name="Naziv">Naziv.</param>
/// <param name="Opis">Opis (opciono).</param>
/// <param name="Aktivan">Soft-delete status.</param>
/// <param name="CreatedAt">UTC kreiranja (interceptor).</param>
/// <param name="UpdatedAt">UTC poslednje izmene ili null.</param>
public sealed record KategorijaDto(
    int Id,
    string Sifra,
    string Naziv,
    string? Opis,
    bool Aktivan,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    /// <summary>Ručno mapiranje entitet → DTO, na jednom mestu.</summary>
    public static KategorijaDto Od(Kategorija k) =>
        new(k.Id, k.Sifra, k.Naziv, k.Opis, k.Aktivan, k.CreatedAt, k.UpdatedAt);
}

/// <summary>
/// Telo <c>POST</c>/<c>PUT /api/v1/kategorije</c>. Samo polja koja korisnik sme da pošalje —
/// bez <c>Id</c>, <c>Aktivan</c> (zasebne akcije) i audit polja.
/// </summary>
/// <param name="Sifra">Šifra (do 32 znaka).</param>
/// <param name="Naziv">Naziv (do 200 znakova).</param>
/// <param name="Opis">Opis (do 1000 znakova, opciono).</param>
public sealed record KategorijaInput(string Sifra, string Naziv, string? Opis);
