namespace TpmPregledi.Domain.Entities;

/// <summary>Stanje uređaja na jednom predatom pregledu.</summary>
public enum StanjeUredjaja
{
    /// <summary>Sve stavke uređaja ispravne.</summary>
    Ispravan,

    /// <summary>Bar jedna stavka neispravna.</summary>
    Neispravan,

    /// <summary>„Nije u funkciji“ na tom pregledu.</summary>
    VanFunkcije,
}

/// <summary>Stanje uređaja na pregledu (ulaz za istoriju i analitiku).</summary>
/// <param name="Datum">Planirani dan pregleda.</param>
/// <param name="Stanje">Stanje.</param>
public sealed record StanjeNaDan(DateOnly Datum, StanjeUredjaja Stanje);

/// <summary>Analitika po uređaju (R8.6).</summary>
/// <param name="DanaBezKvaraSada">Dana bez kvara do danas (0 ako je kvar otvoren).</param>
/// <param name="NajduzeBezKvara">Najduži period bez kvara (dana).</param>
/// <param name="DanaOdPoslednjegKvara">Dana od kraja poslednjeg kvara (null ako ga nije bilo).</param>
/// <param name="BrojKvarova">Broj kvarova u periodu.</param>
/// <param name="ProsekTrajanja">Prosečno trajanje kvara (dana).</param>
/// <param name="Trajanja">Trajanje svakog kvara (dana).</param>
public sealed record AnalitikaUredjaja(int DanaBezKvaraSada, int NajduzeBezKvara, int? DanaOdPoslednjegKvara, int BrojKvarova, double ProsekTrajanja, IReadOnlyList<int> Trajanja);

/// <summary>
/// Pravila računanja (R8.7): između dva pregleda važi stanje sa poslednjeg pregleda; kvar traje od pregleda koji ga je prijavio
/// do prvog pregleda „ispravno“; dani „nije u funkciji“ se ne računaju ni kao ispravni ni kao kvar.
/// </summary>
public static class AnalitikaPravila
{
    /// <summary>Stanje za svaki dan perioda (null = još nije bilo pregleda).</summary>
    public static IReadOnlyList<(DateOnly Dan, StanjeUredjaja? Stanje)> Istorija(IEnumerable<StanjeNaDan> stanja, DateOnly od, DateOnly doDana)
    {
        var niz = stanja.OrderBy(s => s.Datum).ToList();
        var rez = new List<(DateOnly, StanjeUredjaja?)>();
        var i = 0;
        StanjeUredjaja? tekuce = null;
        for (; i < niz.Count && niz[i].Datum < od; i++) tekuce = niz[i].Stanje;
        for (var d = od; d <= doDana; d = d.AddDays(1))
        {
            for (; i < niz.Count && niz[i].Datum <= d; i++) tekuce = niz[i].Stanje;
            rez.Add((d, tekuce));
        }
        return rez;
    }

    /// <summary>Analitika iz dnevne istorije: periodi kvara su uzastopni dani „neispravan“ (van funkcije ih ne prekida ni ne produžava).</summary>
    public static AnalitikaUredjaja Izracunaj(IReadOnlyList<(DateOnly Dan, StanjeUredjaja? Stanje)> istorija)
    {
        var kvarovi = new List<int>();
        int bez = 0, najduze = 0, uKvaru = 0;
        DateOnly? krajPoslednjeg = null;
        foreach (var (dan, s) in istorija)
        {
            switch (s)
            {
                case StanjeUredjaja.Neispravan:
                    uKvaru++;
                    najduze = Math.Max(najduze, bez);
                    bez = 0;
                    break;
                case StanjeUredjaja.Ispravan:
                    if (uKvaru > 0) { kvarovi.Add(uKvaru); uKvaru = 0; krajPoslednjeg = dan; }
                    bez++;
                    break;
            }
        }
        najduze = Math.Max(najduze, bez);
        var poslednji = istorija.Count > 0 ? istorija[^1].Dan : DateOnly.MinValue;
        var zbir = kvarovi.Concat(uKvaru > 0 ? [uKvaru] : []).ToList();
        return new AnalitikaUredjaja(uKvaru > 0 ? 0 : bez, najduze,
            krajPoslednjeg is { } k ? poslednji.DayNumber - k.DayNumber : null, zbir.Count, zbir.Count == 0 ? 0 : Math.Round(zbir.Average(), 1), zbir);
    }

    private static readonly TimeZoneInfo Beograd = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Belgrade");

    /// <summary>Lokalni (beogradski) dan UTC trenutka — „urađen na planirani dan“ se poredi sa lokalnim kalendarom.</summary>
    public static DateOnly LokalniDan(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Beograd));

    /// <summary>
    /// Kašnjenje overe u satima radnih dana (R6.3): sati vikenda se ne računaju. Rok se poredi sa ovim brojem.
    /// </summary>
    public static double SatiRadnihDana(DateTime od, DateTime doVremena)
    {
        if (doVremena <= od) return 0;
        double sati = 0;
        var t = od;
        while (t < doVremena)
        {
            var sledeci = t.Date.AddDays(1) < doVremena ? t.Date.AddDays(1) : doVremena;
            if (t.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) sati += (sledeci - t).TotalHours;
            t = sledeci;
        }
        return sati;
    }
}
