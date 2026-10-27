namespace TpmPregledi.Domain.Common;

/// <summary>
/// Označava entitet čije se izmene (CREATE/UPDATE/DELETE) automatski upisuju u <c>audit_log</c>
/// kroz <c>AuditSaveChangesInterceptor</c>. Pravilo: svi šifarnici, matični podaci, korisnici
/// i transakcioni unosi; ne log tabele.
/// </summary>
/// <remarks>
/// Ne stavlja se na <c>AuditLog</c> (rekurzija) ni na <c>RefreshToken</c> (šum).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditableAttribute : Attribute;
