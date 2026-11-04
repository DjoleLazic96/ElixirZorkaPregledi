namespace TpmPregledi.Api.Common;

/// <summary>Ime politike za pravo: <c>Pravo:overa</c>. Jedno mesto da Program.cs i endpointi ne kucaju string.</summary>
public static class PravoPolitika
{
    /// <summary>Politika koja traži claim <c>pravo</c> sa datom vrednošću.</summary>
    public static string Za(string pravo) => $"Pravo:{pravo}";
}
