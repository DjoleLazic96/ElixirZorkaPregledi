using System.ComponentModel.DataAnnotations;

namespace TpmPregledi.Infrastructure.Persistence.Seed;

/// <summary>
/// Sekcija <c>Admin</c> — inicijalni admin nalog. Produkcija: <c>Admin__InitialUsername</c>,
/// <c>Admin__InitialEmail</c>, <c>Admin__InitialPassword</c> iz <c>.env</c>-a. Lozinka namerno nema
/// <c>[Required]</c>: prazna lozinka ne obara start, već seed loguje Critical i preskače admina
/// (vidi <see cref="SeedData"/>).
/// </summary>
public sealed class AdminOptions
{
    /// <summary>Ime sekcije u konfiguraciji.</summary>
    public const string SectionName = "Admin";

    /// <summary>Korisničko ime inicijalnog admina.</summary>
    [Required]
    public string InitialUsername { get; init; } = "admin";

    /// <summary>E-mail inicijalnog admina.</summary>
    [Required, EmailAddress]
    public string InitialEmail { get; init; } = "admin@tpm-pregledi.local";

    /// <summary>Početna lozinka; prazna ili <c>Admin123!</c> → admin se ne kreira.</summary>
    public string InitialPassword { get; init; } = string.Empty;
}
