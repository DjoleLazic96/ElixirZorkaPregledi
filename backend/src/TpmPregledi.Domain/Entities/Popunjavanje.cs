using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Važeći odgovor na stavku pregleda (jedan po stavci i doradi). Promena odgovora piše staro i novo u istoriju pregleda.
/// </summary>
[Auditable]
public class Odgovor
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Stavka verzije šablona.</summary>
    public int StavkaId { get; set; }

    /// <summary>0 = ispravan odgovor, 1 = neispravan.</summary>
    public short Vrednost { get; set; }

    /// <summary>Napomena (obavezna uz neispravan).</summary>
    public string? Napomena { get; set; }

    /// <summary>Dodatna polja kao JSON objekat (ključ = ID polja, vrednost = broj ili niz brojeva).</summary>
    public string Polja { get; set; } = "{}";

    /// <summary>Ko je odgovorio.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>Vreme odgovora sa klijenta (R5.1).</summary>
    public DateTime KlijentAt { get; set; }

    /// <summary>Kada je stiglo na server.</summary>
    public DateTime ServerAt { get; set; }

    /// <summary>Operacija koja je postavila odgovor.</summary>
    public Guid OpId { get; set; }

    /// <summary>Broj dorade (0 = prvo popunjavanje).</summary>
    public int Dorada { get; set; }
}

/// <summary>„Nije u funkciji“ za ceo uređaj; važi samo za taj pregled (R4.6).</summary>
public class UredjajVanFunkcije
{
    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Uređaj.</summary>
    public int UredjajId { get; set; }

    /// <summary>Ko je označio.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>Vreme sa klijenta.</summary>
    public DateTime KlijentAt { get; set; }
}

/// <summary>Slika uz stavku (R4.5): najviše 5 po stavci, pamti izvor i ko ju je dodao.</summary>
public class Fotografija
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Stavka.</summary>
    public int StavkaId { get; set; }

    /// <summary>Fajl na volumenu.</summary>
    public long FajlId { get; set; }

    /// <summary><c>kamera</c> ili <c>galerija</c>.</summary>
    public string Izvor { get; set; } = "kamera";

    /// <summary>Ko je dodao.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>ID sa klijenta — ponovljeno slanje iste slike ne pravi duplikat.</summary>
    public Guid KlijentId { get; set; }

    /// <summary>Kada je primljena (UTC).</summary>
    public DateTime KreiranoAt { get; set; }
}

/// <summary>
/// Sporan unos (R6.8): operacije koje su stigle sa telefona posle prebacivanja ili poništavanja pregleda. Ništa se ne
/// gubi; Supervizor odlučuje šta važi (faza 06).
/// </summary>
public class SporanUnos
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Ko je poslao.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>Operacija kao JSON.</summary>
    public string Operacija { get; set; } = "{}";

    /// <summary>Zašto nije primenjena.</summary>
    public string Razlog { get; set; } = string.Empty;

    /// <summary>Kada je stigla (UTC).</summary>
    public DateTime PrimljenoAt { get; set; }

    /// <summary>Odluka Supervizora: null = čeka.</summary>
    public string? Odluka { get; set; }
}

/// <summary>Primljena operacija sa klijenta — ponovljen <see cref="OpId"/> se ne primenjuje dva puta.</summary>
public class SinhronizacijaOperacija
{
    /// <summary>UUID sa klijenta (PK).</summary>
    public Guid OpId { get; set; }

    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Vrsta operacije.</summary>
    public string Vrsta { get; set; } = string.Empty;

    /// <summary>Vreme operacije sa klijenta (aktivno vreme).</summary>
    public DateTime KlijentAt { get; set; }

    /// <summary>Kada je primljena (UTC).</summary>
    public DateTime PrimljenoAt { get; set; }
}

/// <summary>Aktivno vreme pregleda (R5.2, R5.3) iz vremena operacija sa klijenta.</summary>
public static class AktivnoVreme
{
    /// <summary>
    /// Zbir razmaka između uzastopnih operacija koji nisu veći od granice pauze; svaki veći razmak deli pregled na novi deo.
    /// Prazan spisak → (0, 0); jedna operacija → (0, 1).
    /// </summary>
    public static (int Sekundi, int Delova) Izracunaj(IEnumerable<DateTime> vremena, TimeSpan granicaPauze)
    {
        var niz = vremena.OrderBy(t => t).ToList();
        if (niz.Count == 0) return (0, 0);
        double sekundi = 0;
        var delova = 1;
        for (var i = 1; i < niz.Count; i++)
        {
            var razmak = niz[i] - niz[i - 1];
            if (razmak <= granicaPauze) sekundi += razmak.TotalSeconds;
            else delova++;
        }
        return ((int)Math.Round(sekundi), delova);
    }
}
