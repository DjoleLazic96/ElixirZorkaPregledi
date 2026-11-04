using TpmPregledi.Domain.Common;

namespace TpmPregledi.Domain.Entities;

/// <summary>Neradni dan ili praznik (R3.16, R3.17). DP se tada ne radi; PZ sme ako je zakazan.</summary>
[Auditable]
public class NeradniDan
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Datum (jedinstven).</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Naziv, npr. „Božić“.</summary>
    public string Naziv { get; set; } = string.Empty;

    /// <summary><c>drzavni</c>, <c>verski</c> ili <c>fabrika</c>.</summary>
    public string Vrsta { get; set; } = "drzavni";

    /// <summary>Ko je uneo.</summary>
    public string? UneoId { get; set; }
}

/// <summary>Odsustvo inženjera ili zahtev za odsustvo (R3.18, R3.19).</summary>
[Auditable]
public class Odsustvo
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Čije je odsustvo.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>Prvi dan.</summary>
    public DateOnly Od { get; set; }

    /// <summary>Poslednji dan (uključivo).</summary>
    public DateOnly Do { get; set; }

    /// <summary><c>godisnji</c>, <c>bolovanje</c>, <c>slobodan_dan</c>, <c>sluzbeni_put</c>, <c>ostalo</c>.</summary>
    public string Vrsta { get; set; } = "godisnji";

    /// <summary>Napomena (opciono).</summary>
    public string? Napomena { get; set; }

    /// <summary><c>zahtev</c>, <c>odobreno</c>, <c>odbijeno</c>.</summary>
    public string Status { get; set; } = "zahtev";

    /// <summary>Ko je uneo zahtev ili odsustvo.</summary>
    public string? ZatrazioId { get; set; }

    /// <summary>Ko je odlučio.</summary>
    public string? OdlucioId { get; set; }

    /// <summary>Razlog odbijanja (opciono).</summary>
    public string? RazlogOdbijanja { get; set; }

    /// <summary>Kada je uneto (UTC).</summary>
    public DateTime KreiranoAt { get; set; }

    /// <summary>Kada je odlučeno (UTC).</summary>
    public DateTime? OdlucenoAt { get; set; }

    /// <summary>Da li pokriva dan.</summary>
    public bool Pokriva(DateOnly d) => d >= Od && d <= Do;
}

/// <summary>
/// Pregled (DP ili PZ). Nastaje potvrdom rasporeda ili zakazivanjem PZ-a; broj (<c>DP1-2026-0142</c>) se ne menja ni pri
/// prenosu ni pri doradi (R6.9). Pregled se ne briše, nego poništava (faza 06). Kolone za popunjavanje dodaje faza 04.
/// </summary>
[Auditable]
public class Pregled
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Broj, npr. <c>DP1-2026-0142</c>.</summary>
    public string Broj { get; set; } = string.Empty;

    /// <summary>DP1, DP2, PZ1…PZ4.</summary>
    public string Tip { get; set; } = string.Empty;

    /// <summary>Verzija šablona; vezuje se pri početku pregleda (faza 04).</summary>
    public int? SablonVerzijaId { get; set; }

    /// <summary>Dan kada treba da se uradi (menja se prenosom i ručnom izmenom).</summary>
    public DateOnly PlaniraniDatum { get; set; }

    /// <summary>Dan na koji je prvi put raspoređen (za ispunjenje plana i „prenet sa“).</summary>
    public DateOnly PrvobitniDatum { get; set; }

    /// <summary><see cref="StatusPregleda"/>.</summary>
    public string Status { get; set; } = StatusPregleda.Planiran;

    /// <summary>Inženjer kome je dodeljen.</summary>
    public string DodeljenId { get; set; } = string.Empty;

    /// <summary>Napomena (npr. uz „Promeni PZ“).</summary>
    public string? Napomena { get; set; }

    /// <summary>Kada je kreiran (UTC).</summary>
    public DateTime KreiranoAt { get; set; }

    /// <summary>Da li je DP (dnevni pregled).</summary>
    public bool JeDp => Tip.StartsWith("DP", StringComparison.Ordinal);

    /// <summary>Učesnici PZ-a sa dva inženjera (R3.14).</summary>
    public List<PregledUcesnik> Ucesnici { get; set; } = [];
}

/// <summary>Statusi pregleda (spec §6.4). „Nije započet“ i „propušten“ su izvedeni.</summary>
public static class StatusPregleda
{
    /// <summary>U rasporedu, nije započet.</summary>
    public const string Planiran = "planiran";

    /// <summary>Započet, nije predat.</summary>
    public const string URadu = "u_radu";

    /// <summary>Predat, čeka overu.</summary>
    public const string CekaOveru = "ceka_overu";

    /// <summary>Overen.</summary>
    public const string Overen = "overen";

    /// <summary>Vraćen na doradu.</summary>
    public const string VracenNaDoradu = "vracen_na_doradu";

    /// <summary>Poništen.</summary>
    public const string Ponisten = "ponisten";
}

/// <summary>Drugi inženjer na PZ-u i sekcije koje radi (R3.14).</summary>
public class PregledUcesnik
{
    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Inženjer.</summary>
    public string KorisnikId { get; set; } = string.Empty;

    /// <summary>Šifre sekcija koje radi.</summary>
    public string[] Sekcije { get; set; } = [];
}

/// <summary>Događaj u istoriji pregleda (R6.7): dodela, prebacivanje, prenos, promena PZ-a…</summary>
public class PregledDogadjaj
{
    /// <summary>PK.</summary>
    public long Id { get; set; }

    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Navigacija na pregled.</summary>
    public Pregled? Pregled { get; set; }

    /// <summary>Vrsta: <c>dodela</c>, <c>prebacivanje</c>, <c>prenos</c>, <c>promena_pz</c>, <c>ucesnik</c>, <c>izmena_rasporeda</c>.</summary>
    public string Vrsta { get; set; } = string.Empty;

    /// <summary>Detalji kao JSON (npr. stari i novi datum).</summary>
    public string Podaci { get; set; } = "{}";

    /// <summary>Ko (null = sistem, npr. automatski prenos).</summary>
    public string? KorisnikId { get; set; }

    /// <summary>Kada (UTC).</summary>
    public DateTime At { get; set; }
}

/// <summary>Zahtev inženjera da se pregled prebaci na drugog (R3.11, R3.12).</summary>
[Auditable]
public class ZahtevPrebacivanja
{
    /// <summary>PK.</summary>
    public int Id { get; set; }

    /// <summary>Pregled.</summary>
    public int PregledId { get; set; }

    /// <summary>Ko traži.</summary>
    public string PodneoId { get; set; } = string.Empty;

    /// <summary>Predlog zamene (opciono).</summary>
    public string? PredlozenaZamenaId { get; set; }

    /// <summary>Razlog (obavezan).</summary>
    public string Razlog { get; set; } = string.Empty;

    /// <summary><c>zahtev</c>, <c>odobreno</c>, <c>odbijeno</c>.</summary>
    public string Status { get; set; } = "zahtev";

    /// <summary>Izabrana zamena.</summary>
    public string? ZamenaId { get; set; }

    /// <summary>Ko je odlučio.</summary>
    public string? OdlucioId { get; set; }

    /// <summary>Kada je podnet (UTC).</summary>
    public DateTime KreiranoAt { get; set; }

    /// <summary>Kada je odlučeno (UTC).</summary>
    public DateTime? OdlucenoAt { get; set; }
}
