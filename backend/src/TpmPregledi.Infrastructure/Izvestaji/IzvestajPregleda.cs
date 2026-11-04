using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TpmPregledi.Application.Features.Pregledi;

namespace TpmPregledi.Infrastructure.Izvestaji;

/// <summary>
/// Izveštaji pregleda (R6.1, R11.1, R11.2): PDF (QuestPDF) i Word (OpenXML) sa istim sadržajem — zaglavlje sa verzijom
/// šablona, rezime problematičnih stavki na početku, pa ceo pregled po sekcijama; Excel arhive (ClosedXML).
/// </summary>
public sealed class IzvestajPregleda : IIzvestajPregleda
{
    static IzvestajPregleda() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly System.Globalization.CultureInfo Sr = new("sr-Latn-RS");

    private static string Status(string s) => s switch
    {
        "planiran" => "Planiran", "u_radu" => "U radu", "ceka_overu" => "Čeka overu", "overen" => "Overen",
        "vracen_na_doradu" => "Vraćen na doradu", "ponisten" => "Poništen", _ => s,
    };

    private static string Vreme(DateTime? utc) => utc is null ? "—" : TimeZoneInfo.ConvertTimeFromUtc(utc.Value, Beograd).ToString("dd.MM.yyyy HH:mm", Sr);

    private static readonly TimeZoneInfo Beograd = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Belgrade");

    private static IEnumerable<(string Labela, string Vrednost)> Zaglavlje(PregledDetaljDto p) =>
    [
        ("Broj", p.Dorada > 0 ? $"{p.Broj} (dorada {p.Dorada})" : p.Broj),
        ("Šablon", $"{p.Tip}, verzija {p.VerzijaBroj?.ToString() ?? "—"}"),
        ("Planirani datum", p.PlaniraniDatum.ToString("dd.MM.yyyy", Sr)),
        ("Inženjer", p.Inzenjer),
        ("Početak", Vreme(p.ZapocetAt)),
        ("Predaja", Vreme(p.PredatAt)),
        ("Aktivno vreme", $"{p.AktivnoSekundi / 60} min ({p.Delova} {(p.Delova == 1 ? "deo" : "dela")})"),
        ("Status", Status(p.Status)),
        ("Overio", p.Overio is null ? "—" : $"{p.Overio}, {Vreme(p.OverenoAt)}"),
    ];

    private static IEnumerable<(string Sekcija, DetaljUredjajDto Uredjaj, DetaljStavkaDto Stavka)> Problematicne(PregledDetaljDto p) =>
        p.Sekcije.SelectMany(s => s.Uredjaji.SelectMany(u => u.Stavke.Where(x => x.Neispravan).Select(x => (s.Sifra, u, x))));

    /// <inheritdoc />
    public byte[] Pdf(PregledDetaljDto p) => QuestPDF.Fluent.Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(28);
        page.DefaultTextStyle(t => t.FontSize(9));
        page.Header().Text($"TPM pregled {p.Broj}").FontSize(14).SemiBold();
        page.Content().PaddingVertical(8).Column(col =>
        {
            col.Spacing(6);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.ConstantColumn(110); c.RelativeColumn(); });
                foreach (var (l, v) in Zaglavlje(p)) { t.Cell().Text(l).SemiBold(); t.Cell().Text(v); }
            });

            var problemi = Problematicne(p).ToList();
            col.Item().PaddingTop(6).Text($"Problematične stavke ({problemi.Count})").FontSize(11).SemiBold();
            if (problemi.Count == 0) col.Item().Text("Nema neispravnih stavki.");
            foreach (var (sek, u, s) in problemi)
                col.Item().Text($"Sekcija {sek} · {u.Naziv} · {s.Aktivnost}: {s.Odgovor}. {s.Napomena} {string.Join("; ", s.Polja)} {(s.Fotografije.Count > 0 ? $"({s.Fotografije.Count} slika)" : "")}".Trim());
            var van = p.Sekcije.SelectMany(s => s.Uredjaji.Where(u => u.VanFunkcije)).Select(u => u.Naziv).ToList();
            if (van.Count > 0) col.Item().Text($"Nije u funkciji: {string.Join(", ", van)}");

            foreach (var sek in p.Sekcije)
            {
                col.Item().PaddingTop(8).Text($"Sekcija {sek.Sifra}").FontSize(11).SemiBold();
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(4); });
                    foreach (var h in new[] { "Uređaj", "Aktivnost", "Odgovor", "Napomena" })
                        t.Cell().Background(Colors.Grey.Lighten3).Padding(2).Text(h).SemiBold();
                    foreach (var u in sek.Uredjaji)
                        foreach (var s in u.Stavke)
                        {
                            t.Cell().Padding(2).Text(u.Naziv);
                            t.Cell().Padding(2).Text(s.Aktivnost);
                            var odg = t.Cell().Padding(2).Text(u.VanFunkcije ? "nije u funkciji" : s.Odgovor ?? "—");
                            if (s.Neispravan) odg.FontColor(Colors.Red.Darken2).SemiBold();
                            t.Cell().Padding(2).Text(string.Join(" ", new[] { s.Napomena, string.Join("; ", s.Polja) }.Where(x => !string.IsNullOrEmpty(x))));
                        }
                });
            }
        });
        page.Footer().AlignRight().Text(x => { x.Span("Strana "); x.CurrentPageNumber(); x.Span(" od "); x.TotalPages(); });
    })).GeneratePdf();

    /// <inheritdoc />
    public byte[] Docx(PregledDetaljDto p)
    {
        using var ms = new MemoryStream();
        using (var w = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = w.AddMainDocumentPart();
            var body = new W.Body();
            main.Document = new W.Document(body);

            W.Paragraph Par(string tekst, bool bold = false, string? velicina = null, string? boja = null)
            {
                var rp = new W.RunProperties();
                if (bold) rp.Append(new W.Bold());
                if (boja is not null) rp.Append(new W.Color { Val = boja });
                if (velicina is not null) rp.Append(new W.FontSize { Val = velicina });
                return new W.Paragraph(new W.Run(rp, new W.Text(tekst) { Space = SpaceProcessingModeValues.Preserve }));
            }

            W.TableCell Celija(string tekst, bool bold = false, string? boja = null) => new(Par(tekst, bold, null, boja));

            W.Table Tabela(IEnumerable<string[]> redovi, bool prviRedBold)
            {
                var t = new W.Table(new W.TableProperties(new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 4 }, new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 }, new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 }, new W.RightBorder { Val = W.BorderValues.Single, Size = 4 })));
                var prvi = true;
                foreach (var r in redovi)
                {
                    t.Append(new W.TableRow(r.Select(c => Celija(c, prvi && prviRedBold))));
                    prvi = false;
                }
                return t;
            }

            body.Append(Par($"TPM pregled {p.Broj}", true, "32"));
            body.Append(Tabela(Zaglavlje(p).Select(z => new[] { z.Labela, z.Vrednost }), false));
            var problemi = Problematicne(p).ToList();
            body.Append(Par($"Problematične stavke ({problemi.Count})", true, "24"));
            if (problemi.Count == 0) body.Append(Par("Nema neispravnih stavki."));
            foreach (var (sek, u, s) in problemi)
                body.Append(Par($"Sekcija {sek} · {u.Naziv} · {s.Aktivnost}: {s.Odgovor}. {s.Napomena} {string.Join("; ", s.Polja)}".Trim(), false, null, "A00000"));
            foreach (var sek in p.Sekcije)
            {
                body.Append(Par($"Sekcija {sek.Sifra}", true, "24"));
                var redovi = new List<string[]> { new[] { "Uređaj", "Aktivnost", "Odgovor", "Napomena" } };
                foreach (var u in sek.Uredjaji)
                    foreach (var s in u.Stavke)
                        redovi.Add([u.Naziv, s.Aktivnost, u.VanFunkcije ? "nije u funkciji" : s.Odgovor ?? "—",
                            string.Join(" ", new[] { s.Napomena, string.Join("; ", s.Polja) }.Where(x => !string.IsNullOrEmpty(x)))]);
                body.Append(Tabela(redovi, true));
            }
            body.Append(new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }, new W.PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720 }));
        }
        return ms.ToArray();
    }

    /// <inheritdoc />
    public byte[] ExcelTabela(string list, IReadOnlyList<string> zaglavlje, IEnumerable<IReadOnlyList<object?>> redovi)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(list);
        for (var i = 0; i < zaglavlje.Count; i++) ws.Cell(1, i + 1).Value = zaglavlje[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var red in redovi)
        {
            for (var i = 0; i < red.Count; i++)
            {
                var c = ws.Cell(r, i + 1);
                switch (red[i])
                {
                    case DateOnly d: c.Value = d.ToDateTime(TimeOnly.MinValue); c.Style.DateFormat.Format = "dd.MM.yyyy"; break;
                    case int n: c.Value = n; break;
                    case null: break;
                    default: c.Value = red[i]!.ToString(); break;
                }
            }
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <inheritdoc />
    public byte[] Excel(IReadOnlyList<PregledRedDto> redovi)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Pregledi");
        string[] zaglavlje = ["Broj", "Tip", "Planirani datum", "Status", "Dorada", "Inženjer", "Predat", "Overio", "Neispravnih"];
        for (var i = 0; i < zaglavlje.Length; i++) ws.Cell(1, i + 1).Value = zaglavlje[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var p in redovi)
        {
            ws.Cell(r, 1).Value = p.Broj;
            ws.Cell(r, 2).Value = p.Tip;
            ws.Cell(r, 3).Value = p.PlaniraniDatum.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 3).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(r, 4).Value = Status(p.Status);
            ws.Cell(r, 5).Value = p.Dorada;
            ws.Cell(r, 6).Value = p.Inzenjer;
            ws.Cell(r, 7).Value = Vreme(p.PredatAt);
            ws.Cell(r, 8).Value = p.Overio ?? string.Empty;
            ws.Cell(r, 9).Value = p.Neispravnih;
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
