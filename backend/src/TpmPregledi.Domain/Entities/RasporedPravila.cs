namespace TpmPregledi.Domain.Entities;

/// <summary>Novo mesto pregleda posle prenosa.</summary>
/// <param name="Pregled">Pregled koji se pomera.</param>
/// <param name="NoviDatum">Novi planirani datum.</param>
/// <param name="NoviDodeljenId">Inženjer na novom mestu.</param>
public sealed record Pomeranje(Pregled Pregled, DateOnly NoviDatum, string NoviDodeljenId);

/// <summary>
/// Pravila rasporeda bez baze (čiste funkcije, direktno testabilne): radni dan, redosled DP i PZ, rotacija i prenos
/// propuštenog DP-a (R3.1–R3.5, R3.10).
/// </summary>
public static class RasporedPravila
{
    /// <summary>Radni dan: ponedeljak–petak koji nije neradni dan (R3.16).</summary>
    public static bool JeRadniDan(DateOnly d, IReadOnlySet<DateOnly> neradni) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !neradni.Contains(d);

    /// <summary>Da li se tog dana radi DP: radni dan, a na dan PZ-a samo ako podešavanje to dozvoljava (R3.9).</summary>
    public static bool JeDanZaDp(DateOnly d, IReadOnlySet<DateOnly> neradni, IReadOnlySet<DateOnly> daniPz, bool dpNaDanPz) =>
        JeRadniDan(d, neradni) && (dpNaDanPz || !daniPz.Contains(d));

    /// <summary>Prvi dan za DP posle datog dana.</summary>
    public static DateOnly SledeciDanZaDp(DateOnly posle, IReadOnlySet<DateOnly> neradni, IReadOnlySet<DateOnly> daniPz, bool dpNaDanPz)
    {
        var d = posle.AddDays(1);
        while (!JeDanZaDp(d, neradni, daniPz, dpNaDanPz)) d = d.AddDays(1);
        return d;
    }

    /// <summary>DP se radi naizmenično DP1, DP2 (R3.1); prvi je DP1.</summary>
    public static string SledeciDp(string? poslednji) => poslednji == "DP1" ? "DP2" : "DP1";

    /// <summary>PZ se radi redom PZ1 → PZ4, pa opet PZ1 (R3.1, R3.7); prvi je PZ1.</summary>
    public static string SledeciPz(string? poslednji) => poslednji switch
    {
        "PZ1" => "PZ2",
        "PZ2" => "PZ3",
        "PZ3" => "PZ4",
        _ => "PZ1",
    };

    /// <summary>
    /// Sledeći inženjer po rotaciji (R3.10): posle <paramref name="poslednji"/> u <paramref name="redosled"/>, preskačući
    /// one koji nisu dostupni (odsustvo). Null ako niko nije dostupan.
    /// </summary>
    public static string? SledeciInzenjer(IReadOnlyList<string> redosled, string? poslednji, Func<string, bool> dostupan)
    {
        if (redosled.Count == 0) return null;
        var start = poslednji is null ? -1 : IndexOf(redosled, poslednji);
        for (var k = 1; k <= redosled.Count; k++)
        {
            var kandidat = redosled[(start + k + redosled.Count) % redosled.Count];
            if (dostupan(kandidat)) return kandidat;
        }
        return null;
    }

    /// <summary>
    /// Prenos (R3.2–R3.5). <paramref name="dpOdDana"/> su DP-ovi od propuštenog dana nadalje, po datumu (prvi je propušten
    /// ili započet). Svaki pregled ide na mesto sledećeg; inženjeri ostaju na svojim danima, pa pregled preuzima inženjera
    /// tog dana. Poslednji dobija sledeći dan za DP i inženjera po rotaciji. Započet pregled (R3.5) zadržava svog inženjera.
    /// </summary>
    public static IReadOnlyList<Pomeranje> Prenesi(
        IReadOnlyList<Pregled> dpOdDana, Func<DateOnly, DateOnly> sledeciDan, Func<DateOnly, string?, string> poRotaciji)
    {
        var rez = new List<Pomeranje>(dpOdDana.Count);
        for (var i = 0; i < dpOdDana.Count; i++)
        {
            var p = dpOdDana[i];
            DateOnly datum;
            string inzenjer;
            if (i + 1 < dpOdDana.Count)
            {
                datum = dpOdDana[i + 1].PlaniraniDatum;
                inzenjer = dpOdDana[i + 1].DodeljenId;
            }
            else
            {
                datum = sledeciDan(p.PlaniraniDatum);
                inzenjer = poRotaciji(datum, dpOdDana.Count > 1 ? dpOdDana[^1].DodeljenId : p.DodeljenId);
            }
            if (p.Status == StatusPregleda.URadu) inzenjer = p.DodeljenId;
            rez.Add(new Pomeranje(p, datum, inzenjer));
        }
        return rez;
    }

    /// <summary>
    /// Povlačenje niza unazad (zamerka 29, obrnuto od <see cref="Prenesi"/>): oslobođen dan <paramref name="dan"/> preuzima
    /// prvi kasniji planirani DP, svaki sledeći ide na mesto prethodnog, a poslednji dan niza ostaje slobodan. Inženjeri ostaju
    /// na svojim danima (R3.4): pregled preuzima inženjera dana na koji dolazi; oslobođen dan dobija <paramref name="inzenjerZaDan"/>.
    /// Vraća pomeranja, oslobođen poslednji dan i inženjera koji je bio na njemu.
    /// </summary>
    public static (IReadOnlyList<Pomeranje> Pomeranja, DateOnly? Oslobodjen, string? InzenjerOslobodjenog) Povuci(
        DateOnly dan, string inzenjerZaDan, IReadOnlyList<Pregled> dpPosle)
    {
        if (dpPosle.Count == 0) return ([], null, null);
        var rez = new List<Pomeranje>(dpPosle.Count);
        for (var i = 0; i < dpPosle.Count; i++)
            rez.Add(i == 0
                ? new Pomeranje(dpPosle[0], dan, inzenjerZaDan)
                : new Pomeranje(dpPosle[i], dpPosle[i - 1].PlaniraniDatum, dpPosle[i - 1].DodeljenId));
        return (rez, dpPosle[^1].PlaniraniDatum, dpPosle[^1].DodeljenId);
    }

    private static int IndexOf(IReadOnlyList<string> l, string v)
    {
        for (var i = 0; i < l.Count; i++) if (l[i] == v) return i;
        return -1;
    }
}
