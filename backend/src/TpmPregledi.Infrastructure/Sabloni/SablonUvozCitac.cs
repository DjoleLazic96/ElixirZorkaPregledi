using ClosedXML.Excel;
using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Features.Sabloni;

namespace TpmPregledi.Infrastructure.Sabloni;

/// <summary>
/// Čita list „Stavke“ fajla za uvoz šablona. Kolone se traže po nazivu zaglavlja (ne po poziciji), pa dodata
/// ili premeštena kolona u Excelu ne kvari uvoz; kolone „Ispravka u odnosu na obrazac“ i „Proveriti“ se ignorišu.
/// </summary>
public sealed class SablonUvozCitac : ISablonUvozCitac
{
    private static readonly string[] Obavezne =
    [
        "Šablon", "Sekcija", "Redosled", "Uređaj – naziv", "Uređaj – oznaka", "Aktivnost",
        "Odgovor ispravan", "Odgovor neispravan", "Dodatno polje",
    ];

    /// <inheritdoc />
    public IReadOnlyList<UvozRed> Procitaj(Stream xlsx)
    {
        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(xlsx);
        }
        catch (Exception)
        {
            throw new BusinessRuleException("Fajl nije ispravan Excel (.xlsx).");
        }

        using (wb)
        {
            if (!wb.TryGetWorksheet("Stavke", out var ws))
                throw new BusinessRuleException("U fajlu nema lista „Stavke“.");

            var zaglavlje = ws.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber);
            var nedostaju = Obavezne.Where(k => !zaglavlje.ContainsKey(k)).ToList();
            if (nedostaju.Count > 0)
                throw new BusinessRuleException($"U listu „Stavke“ nedostaju kolone: {string.Join(", ", nedostaju)}.");

            string Tekst(IXLRow row, string kolona) => row.Cell(zaglavlje[kolona]).GetString().Trim();

            var redovi = new List<UvozRed>();
            var poslednji = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var i = 2; i <= poslednji; i++)
            {
                var row = ws.Row(i);
                if (row.IsEmpty()) continue;
                redovi.Add(new UvozRed(
                    Red: i,
                    Sablon: Tekst(row, "Šablon"),
                    Sekcija: Tekst(row, "Sekcija"),
                    Redosled: int.TryParse(Tekst(row, "Redosled"), out var r) ? r : null,
                    Naziv: Tekst(row, "Uređaj – naziv"),
                    Oznaka: Tekst(row, "Uređaj – oznaka"),
                    Aktivnost: Tekst(row, "Aktivnost"),
                    OdgovorIspravan: Tekst(row, "Odgovor ispravan"),
                    OdgovorNeispravan: Tekst(row, "Odgovor neispravan"),
                    Polje: Tekst(row, "Dodatno polje")));
            }
            return redovi;
        }
    }
}
