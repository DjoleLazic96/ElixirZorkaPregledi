namespace TpmPregledi.Domain.Entities;

/// <summary>
/// Obaveštenje jednom korisniku (zvono, R9.1): upisuje se u istoj transakciji sa događajem, a push i mejl šalje posao
/// isporuke (<see cref="IsporucenoAt"/>), pa obaveštenje ne zavisi od toga da li su push ili mejl dostupni u tom trenutku.
/// </summary>
public class Obavestenje
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Primalac.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary><see cref="VrsteObavestenja"/>.</summary>
    public string Vrsta { get; set; } = string.Empty;

    /// <summary>Naslov (push i mejl).</summary>
    public string Naslov { get; set; } = string.Empty;

    /// <summary>Tekst.</summary>
    public string Tekst { get; set; } = string.Empty;

    /// <summary>Ruta u aplikaciji (klik na obaveštenje).</summary>
    public string? Link { get; set; }

    /// <summary>Ključ idempotentnosti po korisniku (npr. <c>rok_overe:142:0</c>) — zakazane provere ne šalju isto dva puta.</summary>
    public string? Kljuc { get; set; }

    /// <summary>Kada je nastalo (UTC).</summary>
    public DateTime KreiranoAt { get; set; }

    /// <summary>Kada je pročitano u zvonu.</summary>
    public DateTime? ProcitanoAt { get; set; }

    /// <summary>Kada su push i mejl poslati (null = čeka isporuku).</summary>
    public DateTime? IsporucenoAt { get; set; }

    /// <summary>Neuspeli pokušaji isporuke (posle granice se odustaje, zvono ostaje).</summary>
    public int Pokusaja { get; set; }
}

/// <summary>Web Push pretplata jednog uređaja (VAPID).</summary>
public class PushPretplata
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Korisnik.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>Adresa push servisa (jedinstvena po uređaju i pregledaču).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Javni ključ pretplate.</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Tajna pretplate.</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Opis uređaja (User-Agent), za prikaz.</summary>
    public string? UredjajKlijenta { get; set; }

    /// <summary>Kada je prijavljena (UTC).</summary>
    public DateTime KreiranoAt { get; set; }
}

/// <summary>Vrste obaveštenja. Prvih osam ima primaoce koje bira Admin (R9.4); ostale idu samo onome koga se tiču (R9.3).</summary>
public static class VrsteObavestenja
{
    /// <summary>DP ili PZ nije započet do vremena iz podešavanja (R9.2).</summary>
    public const string NijeZapocet = "nije_zapocet";

    /// <summary>Dnevni rezime posle kraja radnog dana.</summary>
    public const string Rezime = "rezime";

    /// <summary>Istekao rok za overu (R6.3).</summary>
    public const string RokOvere = "rok_overe";

    /// <summary>Otvorena hitna akcija.</summary>
    public const string HitnaAkcija = "hitna_akcija";

    /// <summary>Akciji je istekao rok (R7.8).</summary>
    public const string RokAkcije = "rok_akcije";

    /// <summary>Ista stavka neispravna N puta u M dana (R8.2).</summary>
    public const string PonovljeniKvar = "ponovljeni_kvar";

    /// <summary>Uređaj dugo van funkcije (R8.3).</summary>
    public const string VanFunkcije = "van_funkcije";

    /// <summary>Raspored za sledeću nedelju nije potvrđen (spec §5.2).</summary>
    public const string PredlogRasporeda = "predlog_rasporeda";

    /// <summary>Dodela ili promena dodele pregleda.</summary>
    public const string Dodela = "dodela";

    /// <summary>Pregled vraćen na doradu.</summary>
    public const string Dorada = "dorada";

    /// <summary>Ishod zahteva (odsustvo, prebacivanje, dorada).</summary>
    public const string IshodZahteva = "ishod_zahteva";

    /// <summary>Probno obaveštenje (provera push-a na telefonu).</summary>
    public const string Probno = "probno";

    /// <summary>Vrste čije primaoce bira Admin.</summary>
    public static readonly IReadOnlyList<string> Podesive = [NijeZapocet, Rezime, RokOvere, HitnaAkcija, RokAkcije, PonovljeniKvar, VanFunkcije, PredlogRasporeda];
}
