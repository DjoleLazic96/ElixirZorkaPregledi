namespace TpmPregledi.Application.Features.Podesavanja;

/// <summary>
/// Podešavanja aplikacije (R12.1) kao tipiziran ugovor. U bazi je svaki red jedan ključ
/// (<c>podesavanje</c>, jsonb); ključ koji ne postoji u bazi uzima vrednost iz <see cref="Podrazumevana"/>,
/// pa novo podešavanje ne traži migraciju podataka.
/// </summary>
/// <param name="KrajRadnogDana">Posle ovog vremena nezapočet DP je propušten i prenosi se (R3.3).</param>
/// <param name="UpozorenjeDp">Vreme upozorenja „DP nije započet“ (R9.2).</param>
/// <param name="UpozorenjePz">Vreme upozorenja „PZ nije započet“ (R9.2).</param>
/// <param name="DpNaDanPz">Da li se DP radi na dan PZ-a (R3.9).</param>
/// <param name="GranicaPauzeMin">Razmak između odgovora (min) iznad kog se pregled deli na delove (R5.2).</param>
/// <param name="OdstupanjeSataMin">Dozvoljeno odstupanje sata telefona (min) pre upozorenja kontroloru (R5.1).</param>
/// <param name="RokOvereSati">Rok za overu u satima radnih dana (R6.3).</param>
/// <param name="PonovljeniKvarBroj">Koliko puta ista stavka neispravna …</param>
/// <param name="PonovljeniKvarDana">… u koliko dana = ponovljeni kvar (R8.2).</param>
/// <param name="VanFunkcijeDana">Posle koliko dana van funkcije ide obaveštenje, i na svakih toliko (R8.3).</param>
public sealed record PodesavanjaDto(
    TimeOnly KrajRadnogDana,
    TimeOnly UpozorenjeDp,
    TimeOnly UpozorenjePz,
    bool DpNaDanPz,
    int GranicaPauzeMin,
    int OdstupanjeSataMin,
    int RokOvereSati,
    int PonovljeniKvarBroj,
    int PonovljeniKvarDana,
    int VanFunkcijeDana)
{
    /// <summary>Početne vrednosti iz spec-a (R3.9, R5.2, R6.3, R8.2, R8.3, R9.2).</summary>
    public static readonly PodesavanjaDto Podrazumevana = new(
        KrajRadnogDana:     new TimeOnly(15, 0),
        UpozorenjeDp:       new TimeOnly(13, 0),
        UpozorenjePz:       new TimeOnly(13, 0),
        DpNaDanPz:          false,
        GranicaPauzeMin:    10,
        OdstupanjeSataMin:  5,
        RokOvereSati:       24,
        PonovljeniKvarBroj: 3,
        PonovljeniKvarDana: 30,
        VanFunkcijeDana:    30);
}
