using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Uctovnictvo.Core.Model;
using static Uctovnictvo.Core.Services.PdfVyuctovanie;

namespace Uctovnictvo.Core.Services;

/// <summary>Tlač účtovnej závierky mikro účtovnej jednotky (pomôcka na prepis do Úč MÚJ 1-01), obratovej predvahy a hlavnej knihy.</summary>
public static class PdfZavierka
{
    static PdfZavierka() => QuestPDF.Settings.License = LicenseType.Community;

    static void Strana(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily("Arial", "Lato"));
        page.Footer().AlignCenter().Text(t => { t.DefaultTextStyle(s => s.FontSize(7.5f)); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
    }

    static void Hlava(ColumnDescriptor col, UctovnaJednotka j, string titulok)
    {
        col.Item().Text(titulok).FontSize(15).Bold();
        col.Item().Text($"{j.Nazov}{(string.IsNullOrWhiteSpace(j.Ico) ? "" : $", IČO {j.Ico}")}{(string.IsNullOrWhiteSpace(j.Dic) ? "" : $", DIČ {j.Dic}")}");
        col.Item().PaddingBottom(4).LineHorizontal(0.5f);
    }

    static void Vykaz(ColumnDescriptor col, string nadpis, IReadOnlyList<RiadokVykazu> riadky, int rok)
    {
        col.Item().PaddingTop(8).Text(nadpis).Bold().FontSize(10.5f);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c => { c.ConstantColumn(38); c.RelativeColumn(); c.ConstantColumn(30); c.ConstantColumn(80); c.ConstantColumn(80); });
            t.Header(h =>
            {
                h.Cell().Element(Hlavicka).Text("Ozn.").Bold();
                h.Cell().Element(Hlavicka).Text("Text").Bold();
                h.Cell().Element(Hlavicka).AlignRight().Text("Č. r.").Bold();
                h.Cell().Element(Hlavicka).AlignRight().Text($"Bežné ({rok})").Bold();
                h.Cell().Element(Hlavicka).AlignRight().Text($"Predch. ({rok - 1})").Bold();
            });
            foreach (var r in riadky)
            {
                IContainer B(IContainer c) => r.Sucet ? Bunka(c).Background("#EEF2F6") : Bunka(c);
                var txt = t.Cell().Element(B).Text(r.Oznacenie); if (r.Sucet) txt.Bold();
                var tx2 = t.Cell().Element(B).Text(r.Text); if (r.Sucet) tx2.Bold();
                t.Cell().Element(B).AlignRight().Text(r.Riadok.ToString("00"));
                var b = t.Cell().Element(B).AlignRight().Text(Eur(r.Bezne)); if (r.Sucet) b.Bold();
                t.Cell().Element(B).AlignRight().Text(Eur(r.Minule));
            }
        });
    }

    public static byte[] Zavierka(UctovnaJednotka j, int rok, IReadOnlyList<RiadokVykazu> suvaha, IReadOnlyList<RiadokVykazu> vzas,
        IReadOnlyList<ObratUctu> predvaha, IReadOnlyList<string> kontroly) =>
        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Strana(page);
                page.Content().Column(col =>
                {
                    Hlava(col, j, $"Súvaha k 31. 12. {rok}");
                    col.Item().Text("Mikro účtovná jednotka – pomôcka na vyplnenie formulára Úč MÚJ 1-01 (súvaha, netto hodnoty v eurách na dve desatinné miesta). " +
                                    "Účtovnú závierku podajte cez eDane / Finančnú správu; tento výpis nie je úradný formulár.").FontSize(7.5f);
                    if (kontroly.Count > 0)
                        col.Item().PaddingTop(6).Background("#FFF4E5").Padding(6).Column(c => { foreach (var k in kontroly) c.Item().Text("⚠ " + k); });
                    Vykaz(col, "Aktíva", suvaha.Where(r => r.Riadok <= 23).ToList(), rok);
                    Vykaz(col, "Pasíva", suvaha.Where(r => r.Riadok >= 24).ToList(), rok);
                });
            });
            doc.Page(page =>
            {
                Strana(page);
                page.Content().Column(col =>
                {
                    Hlava(col, j, $"Výkaz ziskov a strát {rok}");
                    Vykaz(col, "Výkaz ziskov a strát (Úč MÚJ 2-01)", vzas, rok);
                });
            });
            doc.Page(page =>
            {
                Strana(page);
                page.Content().Column(col =>
                {
                    Hlava(col, j, $"Obratová predvaha {rok}");
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(44); c.RelativeColumn(); c.ConstantColumn(66); c.ConstantColumn(66); c.ConstantColumn(66); c.ConstantColumn(70); });
                        t.Header(h =>
                        {
                            foreach (var (n, cis) in new[] { ("Účet", false), ("Názov", false), ("Poč. stav", true), ("Obrat MD", true), ("Obrat D", true), ("Zostatok", true) })
                            { var c = h.Cell().Element(Hlavicka); (cis ? c.AlignRight() : c).Text(n).Bold(); }
                        });
                        foreach (var o in predvaha)
                        {
                            t.Cell().Element(Bunka).Text(o.Ucet);
                            t.Cell().Element(Bunka).Text(o.Nazov);
                            t.Cell().Element(Bunka).AlignRight().Text(Eur(o.Ps));
                            t.Cell().Element(Bunka).AlignRight().Text(Eur(o.ObratMd));
                            t.Cell().Element(Bunka).AlignRight().Text(Eur(o.ObratD));
                            t.Cell().Element(Bunka).AlignRight().Text(Eur(o.Zostatok));
                        }
                        t.Cell().Element(Bunka); t.Cell().Element(Bunka).Text("Spolu").Bold();
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(predvaha.Sum(o => o.Ps))).Bold();
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(predvaha.Sum(o => o.ObratMd))).Bold();
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(predvaha.Sum(o => o.ObratD))).Bold();
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(predvaha.Sum(o => o.Zostatok))).Bold();
                    });
                    col.Item().PaddingTop(6).Text($"Výsledok hospodárenia {rok}: {Eur(Podvojne.VysledokHospodarenia(predvaha))}").Bold();
                });
            });
        }).WithMetadata(new DocumentMetadata { Title = $"Účtovná závierka {rok}", Creator = "Účtovníctvo" }).GeneratePdf();

    /// <summary>Účtovný denník (chronologicky).</summary>
    public static byte[] Dennik(UctovnaJednotka j, int rok, IReadOnlyList<UctovnyZapis> zapisy) =>
        Document.Create(doc => doc.Page(page =>
        {
            Strana(page);
            page.Size(PageSizes.A4.Landscape());
            page.Content().Column(col =>
            {
                Hlava(col, j, $"Účtovný denník {rok}");
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(56); c.ConstantColumn(96); c.RelativeColumn(); c.ConstantColumn(54); c.ConstantColumn(54); c.ConstantColumn(76); });
                    t.Header(h =>
                    {
                        foreach (var (n, cis) in new[] { ("Dátum", false), ("Doklad", false), ("Popis", false), ("MD", false), ("D", false), ("Suma", true) })
                        { var c = h.Cell().Element(Hlavicka); (cis ? c.AlignRight() : c).Text(n).Bold(); }
                    });
                    foreach (var z in zapisy)
                    {
                        t.Cell().Element(Bunka).Text(Datum(z.Datum));
                        t.Cell().Element(Bunka).Text(z.Doklad);
                        t.Cell().Element(Bunka).Text(string.IsNullOrWhiteSpace(z.Partner) || z.Popis.Contains(z.Partner) ? z.Popis : $"{z.Popis} – {z.Partner}");
                        t.Cell().Element(Bunka).Text(z.Md);
                        t.Cell().Element(Bunka).Text(z.D);
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(z.Suma));
                    }
                    t.Cell().Element(Bunka); t.Cell().Element(Bunka); t.Cell().Element(Bunka).Text("Spolu").Bold(); t.Cell().Element(Bunka); t.Cell().Element(Bunka);
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(zapisy.Sum(z => z.Suma))).Bold();
                });
            });
        })).WithMetadata(new DocumentMetadata { Title = $"Účtovný denník {rok}", Creator = "Účtovníctvo" }).GeneratePdf();
}
