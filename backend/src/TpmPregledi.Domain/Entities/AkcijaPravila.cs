namespace TpmPregledi.Domain.Entities;

/// <summary>Vrste i hitnosti akcija (R7.1, R7.2) i rok iz hitnosti.</summary>
public static class AkcijaPravila
{
    /// <summary>Očistiti / Podmazati ili dopuniti / Zameniti / Popraviti ili sanirati / Ostalo.</summary>
    public static readonly string[] Vrste = ["ocistiti", "podmazati", "zameniti", "popraviti", "ostalo"];

    /// <summary>Hitno / U narednih 7 dana / U sledećem planskom zastoju / Pratiti.</summary>
    public static readonly string[] Hitnosti = ["hitno", "7_dana", "sledeci_pz", "pratiti"];

    /// <summary>Rok: Hitno = sutra (do kraja smene ili sutra), 7 dana = +7, sledeći PZ = dan sledećeg planiranog PZ-a, Pratiti = bez roka.</summary>
    public static DateOnly? Rok(string hitnost, DateOnly danas, DateOnly? sledeciPz) => hitnost switch
    {
        "hitno" => danas.AddDays(1),
        "7_dana" => danas.AddDays(7),
        "sledeci_pz" => sledeciPz,
        _ => null,
    };
}
