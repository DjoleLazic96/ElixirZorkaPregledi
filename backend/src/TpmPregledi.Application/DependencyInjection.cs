using System.Reflection;
using TpmPregledi.Application.Common.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace TpmPregledi.Application;

/// <summary>
/// Composition root Application sloja: MediatR (handleri iz ovog assembly-ja), FluentValidation
/// validatori i <see cref="ValidationBehavior{TRequest,TResponse}"/> ispred svakog handlera.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registruje MediatR, validatore i pipeline ponašanja.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
