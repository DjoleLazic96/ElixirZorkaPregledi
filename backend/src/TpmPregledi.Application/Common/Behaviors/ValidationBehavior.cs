using System.Text.Json;
using TpmPregledi.Application.Common.Exceptions;
using FluentValidation;
using MediatR;

namespace TpmPregledi.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline korak koji pokreće sve registrovane FluentValidation validatore za
/// <typeparamref name="TRequest"/> pre handlera. Handler zato pretpostavlja validan oblik
/// ulaza i bavi se samo poslovnim pravilima.
/// </summary>
/// <remarks>
/// Pad validacije → <see cref="ValidationFailedException"/> → 400 sa mapom polje → poruke.
/// Imena polja se prevode u camelCase (<c>Sifra</c> → <c>sifra</c>) da odgovaraju JSON
/// imenima koja frontend šalje i prikazuje uz polja forme.
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next(ct);

        var ctx = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(ctx, ct))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(ct);

        var errors = failures
            .GroupBy(f => JsonNamingPolicy.CamelCase.ConvertName(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

        throw new ValidationFailedException(errors);
    }
}
