namespace TpmPregledi.Api.Common;

/// <summary>Imena cookie-ja na jednom mestu.</summary>
public static class CookieNames
{
    /// <summary>HttpOnly cookie u kome živi sirovi refresh token (<c>&lt;slug&gt;.refresh</c>).</summary>
    public const string RefreshToken = "tpm-pregledi.refresh";
}
