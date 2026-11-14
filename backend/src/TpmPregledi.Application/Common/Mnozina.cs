namespace TpmPregledi.Application.Common;

/// <summary>Srpska množina u porukama (isto pravilo kao <c>mnozina()</c> na frontu): 1 → jednina, 2–4 → paukal, ostalo i 11–14 → množina.</summary>
public static class Mnozina
{
    /// <summary>„1 stavka“, „3 stavke“, „12 stavki“.</summary>
    public static string Od(int n, string jednina, string paukal, string mnozina)
    {
        var dve = n % 100;
        var jedna = n % 10;
        var oblik = dve is >= 11 and <= 14 ? mnozina : jedna == 1 ? jednina : jedna is >= 2 and <= 4 ? paukal : mnozina;
        return $"{n} {oblik}";
    }
}
