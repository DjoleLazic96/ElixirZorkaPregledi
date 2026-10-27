using System.Net;
using System.Text.Json;
using TpmPregledi.Api.Common;
using TpmPregledi.Application.Common.Exceptions;

namespace TpmPregledi.Api.Middleware;

/// <summary>
/// Hvata izuzetke iz pipeline-a i pretvara ih u RFC 7807 <c>application/problem+json</c> odgovore.
/// Jedina tabela izuzetak → status je <see cref="MapStatus"/> (čista funkcija, unit-testabilna).
/// </summary>
/// <remarks>
/// <para>
/// Loguje samo 500 (<c>LogError</c>) — 4xx se vide iz Serilog request loga. Van Development-a 500
/// ne nosi <c>ex.Message</c> (može da sadrži connection string, putanje, interne tipove) ni stack
/// trace, već samo <c>traceId</c> kojim podrška nalazi red u logu.
/// </para>
/// <para>
/// <c>traceId</c> = <c>Activity.Current.TraceId</c> (W3C), isti identifikator koji Serilog sam upisuje
/// u <c>{TraceId}</c> token outputTemplate-a — zato se ovde ništa ne push-uje u LogContext.
/// </para>
/// </remarks>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    /// <summary>Poruka za 500 van Development-a; <c>{0}</c> je traceId.</summary>
    public const string Poruka500 = "Došlo je do neočekivane greške. Navedite ovaj kod podršci: {0}";

    /// <summary>Pokreće ostatak pipeline-a i pretvara izuzetak u problem+json odgovor.</summary>
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            await HandleAsync(ctx, ex);
        }
    }

    /// <summary>Izuzetak → (HTTP status, naslov). Tabela iz blueprint-a 04 §4 / 06 §6.</summary>
    public static (HttpStatusCode Status, string Title) MapStatus(Exception ex) => ex switch
    {
        ValidationFailedException     => (HttpStatusCode.BadRequest,          "Validacija nije prošla"),
        AuthenticationFailedException => (HttpStatusCode.Unauthorized,        "Prijava nije uspela"),
        ForbiddenException            => (HttpStatusCode.Forbidden,           "Pristup nije dozvoljen"),
        NotFoundException             => (HttpStatusCode.NotFound,            "Resurs nije pronađen"),
        ConflictException             => (HttpStatusCode.Conflict,            "Konflikt"),
        BusinessRuleException         => (HttpStatusCode.UnprocessableEntity, "Poslovno pravilo nije ispunjeno"),
        _                             => (HttpStatusCode.InternalServerError, "Neočekivana greška"),
    };

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var (status, title) = MapStatus(ex);
        var traceId = TraceId.Trenutni(ctx);

        if (status == HttpStatusCode.InternalServerError)
            logger.LogError(ex, "Neočekivana greška tokom obrade zahteva.");

        var detail = status == HttpStatusCode.InternalServerError && !env.IsDevelopment()
            ? string.Format(Poruka500, traceId)
            : ex.Message;

        var problem = new Dictionary<string, object?>
        {
            ["type"]    = "about:blank",
            ["title"]   = title,
            ["status"]  = (int)status,
            ["detail"]  = detail,
            ["traceId"] = traceId,
        };

        if (ex is ValidationFailedException vex)
            problem["errors"] = vex.Errors;

        if (ex is BusinessRuleException bex)
            problem["greske"] = bex.Greske;

        if (env.IsDevelopment() && status == HttpStatusCode.InternalServerError)
            problem["stack"] = ex.StackTrace;

        ctx.Response.StatusCode  = (int)status;
        ctx.Response.ContentType = "application/problem+json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
