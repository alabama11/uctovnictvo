using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Uctovnictvo.Core.Model;
using static Uctovnictvo.Core.Services.PdfVyuctovanie;

namespace Uctovnictvo.Core.Services;

/// <summary>Podklad k daňovému priznaniu – hodnoty po riadkoch na prepis do eFormulára Finančnej správy.</summary>
public static class PdfDanovePriznanie
{
    static PdfDanovePriznanie() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Vytvor(UctovnaJednotka j, string titulok, IReadOnlyList<RiadokDp> riadky, string info) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial", "Lato"));
            page.Footer().AlignCenter().Text(t => { t.DefaultTextStyle(s => s.FontSize(7.5f)); t.Span($"Vytvorené {DateTime.Now:d. M. yyyy} programom Účtovníctvo"); });
            page.Content().Column(col =>
            {
                col.Item().Text(titulok).FontSize(15).Bold();
                col.Item().Text($"{j.Nazov}{(string.IsNullOrWhiteSpace(j.Ico) ? "" : $", IČO {j.Ico}")}{(string.IsNullOrWhiteSpace(j.Dic) ? "" : $", DIČ {j.Dic}")}");
                col.Item().PaddingBottom(4).LineHorizontal(0.5f);
                col.Item().Text("Pomôcka na vyplnenie tlačiva v eFormulári Finančnej správy (financnasprava.sk). Nie je to úradné tlačivo; " +
                                "pred podaním hodnoty skontrolujte, riadky bez čísla sú medzivýpočty.").FontSize(7.5f);
                if (!string.IsNullOrWhiteSpace(info)) col.Item().PaddingTop(6).Background("#EEF2F6").Padding(6).Text(info);
                col.Item().PaddingTop(8).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(90); c.RelativeColumn(); c.ConstantColumn(90); });
                    t.Header(h =>
                    {
                        h.Cell().Element(Hlavicka).Text("Riadok").Bold();
                        h.Cell().Element(Hlavicka).Text("Položka").Bold();
                        h.Cell().Element(Hlavicka).AlignRight().Text("Suma").Bold();
                    });
                    foreach (var r in riadky)
                    {
                        IContainer B(IContainer c) => r.Zvyraznit ? Bunka(c).Background("#EEF2F6") : Bunka(c);
                        t.Cell().Element(B).Text(r.Riadok);
                        var tx = t.Cell().Element(B).Text(r.Text); if (r.Zvyraznit) tx.Bold();
                        var s = t.Cell().Element(B).AlignRight().Text(r.Suma is { } x ? (r.Text == "Sadzba dane" ? $"{x:0} %" : Eur(x)) : ""); if (r.Zvyraznit) s.Bold();
                    }
                });
            });
        })).WithMetadata(new DocumentMetadata { Title = titulok, Creator = "Účtovníctvo" }).GeneratePdf();
}
