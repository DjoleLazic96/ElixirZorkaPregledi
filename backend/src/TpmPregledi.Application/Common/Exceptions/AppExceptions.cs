namespace TpmPregledi.Application.Common.Exceptions;

/// <summary>
/// Bazna klasa za sve izuzetke aplikacije. <c>ExceptionHandlingMiddleware</c> ih mapira u HTTP
/// statuse (tabela u blueprint-u 04 §4); sve ostalo je 500.
/// </summary>
public abstract class AppException(string message) : Exception(message);

/// <summary>Resurs ne postoji ili nije aktivan gde se traži aktivan (404).</summary>
public sealed class NotFoundException : AppException
{
    /// <summary>Gotova poruka za korisnika, npr. „Kategorija ne postoji.“</summary>
    public NotFoundException(string poruka) : base(poruka) { }

    /// <summary>Poruka iz imena entiteta i ključa, npr. „Korisnik sa ključem 'x' nije pronađen.“</summary>
    public NotFoundException(string entity, object key)
        : base($"{entity} sa ključem '{key}' nije pronađen.") { }
}

/// <summary>
/// Oblik ulaza nije validan (400). Mapa polje → poruke; ključevi su camelCase JSON imena
/// polja da frontend može da prikaže grešku uz odgovarajuće polje forme.
/// </summary>
public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : AppException("Jedno ili više polja nije validno.")
{
    /// <summary>Polje → poruke.</summary>
    public IDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>Konflikt stanja: konkurentna izmena ili duplikat ključa koji nije poslovno pravilo (409).</summary>
public sealed class ConflictException(string message) : AppException(message);

/// <summary>
/// Prijava nije uspela ili token ne važi (401). Podrazumevana poruka je <b>ista</b> za
/// nepostojećeg korisnika, pogrešnu lozinku, zaključan i deaktiviran nalog — ne otkrivamo razlog.
/// </summary>
public sealed class AuthenticationFailedException(string message = "Neispravno korisničko ime ili lozinka.")
    : AppException(message);

/// <summary>Prijavljen, ali bez prava (403).</summary>
public sealed class ForbiddenException(string message = "Pristup nije dozvoljen.")
    : AppException(message);

/// <summary>
/// Poslovno pravilo nije ispunjeno (422). Nosi gotove rečenice za prikaz korisniku
/// (<c>greske[]</c>), npr. „Kategorija sa ovom šifrom već postoji.“
/// </summary>
public sealed class BusinessRuleException : AppException
{
    /// <summary>Više poruka odjednom (npr. sve kontrole pri zaključavanju).</summary>
    public BusinessRuleException(IReadOnlyList<string> greske)
        : base("Poslovno pravilo nije ispunjeno.")
    {
        Greske = greske;
    }

    /// <summary>Jedna ili više poruka kao varargs — najčešći slučaj je tačno jedna.</summary>
    public BusinessRuleException(params string[] greske)
        : this((IReadOnlyList<string>)greske) { }

    /// <summary>Gotove rečenice za korisnika.</summary>
    public IReadOnlyList<string> Greske { get; }
}
