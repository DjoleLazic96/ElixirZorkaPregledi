using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Domain.Identity;

namespace TpmPregledi.Application.Features.Korisnici;

/// <summary>
/// Četiri zaštite upravljanja korisnicima (blueprint 07 §5), čiste funkcije bez I/O da budu
/// direktno testabilne. Kršenje → <see cref="BusinessRuleException"/> → 422 sa gotovom porukom.
/// </summary>
/// <remarks>
/// <para>
/// Sve štite od „self-DoS“ scenarija u kome admin greškom zaključa sistem: promenom sopstvene
/// role, deaktivacijom sebe, ili uklanjanjem poslednjeg aktivnog admina.
/// </para>
/// <para>
/// <b>Poznato ograničenje (TOCTOU):</b> invarijanta „bar jedan aktivan admin“ se obezbeđuje
/// read-then-mutate obrascem bez transakcije/row-lock-a. Dve istovremene deaktivacije dva
/// različita admina mogu obe da prođu. Prihvaćeno: površina je admin-only, operatora je malo.
/// Zapisano u <c>docs/04</c>.
/// </para>
/// </remarks>
public static class KorisnikGuards
{
    /// <summary>Poruka: popunjavanje pregleda samo za Inženjera (R1.8).</summary>
    public const string PorukaPopunjavanjeSamoInzenjer =
        "Pravo „Popunjavanje pregleda“ može da ima samo Inženjer.";

    /// <summary>Poruka: Admin ne sme sebi da oduzme upravljanje korisnicima (zaključao bi se).</summary>
    public const string PorukaSamooduzimanjeKorisnika =
        "Ne možete sebi oduzeti pravo upravljanja korisnicima.";

    /// <summary>Pravo popunjavanja pregleda ima samo Inženjer (R1.8).</summary>
    /// <param name="rola">Rola korisnika.</param>
    /// <param name="prava">Nova prava.</param>
    public static void PopunjavanjeSamoInzenjer(string rola, IEnumerable<string> prava)
    {
        if (rola != Roles.Inzenjer && prava.Contains(Prava.PreglediPopunjavanje))
            throw new BusinessRuleException(PorukaPopunjavanjeSamoInzenjer);
    }

    /// <summary>Korisnik ne može sebi da oduzme pravo <c>korisnici</c>.</summary>
    /// <param name="ciljaniKorisnikId">Korisnik čija se prava menjaju.</param>
    /// <param name="akterId">Ko menja.</param>
    /// <param name="prava">Nova prava.</param>
    public static void ZabraniSamooduzimanjeKorisnika(string ciljaniKorisnikId, string? akterId, IEnumerable<string> prava)
    {
        if (ciljaniKorisnikId == akterId && !prava.Contains(Prava.Korisnici))
            throw new BusinessRuleException(PorukaSamooduzimanjeKorisnika);
    }

    /// <summary>Poruka guarda 1.</summary>
    public const string PorukaPromenaSopstveneRole =
        "Ne možete promeniti sopstvenu rolu.";

    /// <summary>Poruka guarda 2.</summary>
    public const string PorukaSamodeaktivacija =
        "Ne možete deaktivirati sami sebe.";

    /// <summary>Poruka guarda 3.</summary>
    public const string PorukaSkidanjeAdminaPoslednjem =
        "Ne možete skinuti rolu Admin poslednjem aktivnom administratoru.";

    /// <summary>Poruka guarda 4.</summary>
    public const string PorukaDeaktivacijaPoslednjegAdmina =
        "Ne možete deaktivirati poslednjeg aktivnog administratora.";

    /// <summary>Guard 1: admin ne može da promeni sopstvenu rolu (ni Admin → Korisnik, ni obrnuto).</summary>
    /// <param name="ciljaniKorisnikId">Korisnik čija se rola menja.</param>
    /// <param name="trenutniKorisnikId">Admin koji izvodi akciju (iz <c>ICurrentUser</c>).</param>
    public static void ZabraniPromenuSopstveneRole(string ciljaniKorisnikId, string? trenutniKorisnikId)
    {
        if (JeIsti(ciljaniKorisnikId, trenutniKorisnikId))
            throw new BusinessRuleException(PorukaPromenaSopstveneRole);
    }

    /// <summary>Guard 2: admin ne može da deaktivira sam sebe.</summary>
    /// <param name="ciljaniKorisnikId">Korisnik koji se deaktivira.</param>
    /// <param name="trenutniKorisnikId">Admin koji izvodi akciju.</param>
    public static void ZabraniSamodeaktivaciju(string ciljaniKorisnikId, string? trenutniKorisnikId)
    {
        if (JeIsti(ciljaniKorisnikId, trenutniKorisnikId))
            throw new BusinessRuleException(PorukaSamodeaktivacija);
    }

    /// <summary>Guard 3: poslednjem aktivnom adminu se ne može skinuti rola Admin.</summary>
    /// <param name="ciljaniKorisnikId">Korisnik kome se skida rola.</param>
    /// <param name="ciljaniJeAdmin">Da li ciljani trenutno ima rolu Admin.</param>
    /// <param name="ciljaniJeAktivan">Da li je ciljani trenutno aktivan.</param>
    /// <param name="sviAktivniAdminIds">ID-jevi svih aktivnih admina (uključujući ciljanog).</param>
    public static void ZabraniSkidanjeAdminaPoslednjem(
        string ciljaniKorisnikId, bool ciljaniJeAdmin, bool ciljaniJeAktivan,
        IReadOnlyCollection<string> sviAktivniAdminIds)
    {
        if (JePoslednjiAktivanAdmin(ciljaniKorisnikId, ciljaniJeAdmin, ciljaniJeAktivan, sviAktivniAdminIds))
            throw new BusinessRuleException(PorukaSkidanjeAdminaPoslednjem);
    }

    /// <summary>Guard 4: poslednji aktivan admin se ne može deaktivirati.</summary>
    /// <param name="ciljaniKorisnikId">Korisnik koji se deaktivira.</param>
    /// <param name="ciljaniJeAdmin">Da li ciljani trenutno ima rolu Admin.</param>
    /// <param name="ciljaniJeAktivan">Da li je ciljani trenutno aktivan.</param>
    /// <param name="sviAktivniAdminIds">ID-jevi svih aktivnih admina (uključujući ciljanog).</param>
    public static void ZabraniDeaktivacijuPoslednjegAdmina(
        string ciljaniKorisnikId, bool ciljaniJeAdmin, bool ciljaniJeAktivan,
        IReadOnlyCollection<string> sviAktivniAdminIds)
    {
        if (JePoslednjiAktivanAdmin(ciljaniKorisnikId, ciljaniJeAdmin, ciljaniJeAktivan, sviAktivniAdminIds))
            throw new BusinessRuleException(PorukaDeaktivacijaPoslednjegAdmina);
    }

    // Bez prijavljenog aktera (null) self-zaštita se ne aktivira — ostale zaštite i dalje važe.
    private static bool JeIsti(string ciljani, string? trenutni) =>
        trenutni is not null && ciljani == trenutni;

    // Ako ciljani nije aktivan admin, nema šta da se izgubi; inače mora postojati DRUGI aktivan admin.
    private static bool JePoslednjiAktivanAdmin(
        string ciljani, bool jeAdmin, bool jeAktivan, IReadOnlyCollection<string> aktivniAdmini)
    {
        if (!jeAdmin || !jeAktivan) return false;
        return aktivniAdmini.Count(id => id != ciljani) == 0;
    }
}
