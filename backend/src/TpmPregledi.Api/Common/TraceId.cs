using System.Diagnostics;

namespace TpmPregledi.Api.Common;

/// <summary>
/// Identifikator zahteva koji ide u RFC 7807 odgovor (<c>traceId</c>) i koji podrška grep-uje u logu.
/// </summary>
/// <remarks>
/// Serilog-ov <c>{TraceId}</c> token u outputTemplate-u je ugrađen i vezan za
/// <c>Activity.Current.TraceId</c> (W3C, 32 hex znaka) — ASP.NET pravi <c>Activity</c> za svaki
/// zahtev, pa Serilog sam puni token. Property „TraceId“ push-ovan kroz LogContext taj token
/// IGNORIŠE, zato se ovde čita isti <c>Activity</c> da odgovor i log nose identičan string.
/// <c>HttpContext.TraceIdentifier</c> je fallback samo kad Activity nema (npr. ručni unit test).
/// </remarks>
public static class TraceId
{
    /// <summary>W3C trace ID tekućeg zahteva ili <c>HttpContext.TraceIdentifier</c>.</summary>
    public static string Trenutni(HttpContext ctx)
    {
        var activity = Activity.Current;
        return activity is { IdFormat: ActivityIdFormat.W3C }
            ? activity.TraceId.ToString()
            : ctx.TraceIdentifier;
    }
}
