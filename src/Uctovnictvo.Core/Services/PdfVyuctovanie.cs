using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public record NastaveniaVyuctovania(DateOnly DatumVystavenia, string Miesto, int SplatnostDni = 14);

/// <summary>Vyúčtovací list pre vlastníka – rozloženie podľa doterajšieho vzoru spoločenstva.</summary>
public static class PdfVyuctovanie
{
    static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");
    static readonly string[] Mesiace = { "január", "február", "marec", "apríl", "máj", "jún", "júl", "august", "september", "október", "november", "december" };
    const string Ram = "#444444";
    const string Tlmene = "#EEF2F6";

    static PdfVyuctovanie()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string Eur(decimal d) => d.ToString("N2", Sk) + " €";
    static string M3(decimal d) => d.ToString("N3", Sk) + " m³";
    static string Datum(DateOnly? d) => d?.ToString("d.M.yyyy", Sk) ?? "";

    /// <summary>Jeden PDF súbor s listom pre každý priestor (každý na samostatnej strane).</summary>
    public static void Uloz(UctovnaJednotka jednotka, IEnumerable<VyuctovanieBytu> byty, NastaveniaVyuctovania n, string path) =>
        Vytvor(jednotka, byty, n).GeneratePdf(path);

    public static Document Vytvor(UctovnaJednotka jednotka, IEnumerable<VyuctovanieBytu> byty, NastaveniaVyuctovania n) =>
        Document.Create(doc =>
        {
            foreach (var b in byty)
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily("Arial", "Lato"));
                    page.Content().Element(c => List(c, jednotka, b, n));
                });
        }).WithMetadata(new DocumentMetadata { Title = $"Vyúčtovanie {jednotka.Nazov}", Creator = "Účtovníctvo" });

    static void List(IContainer container, UctovnaJednotka j, VyuctovanieBytu b, NastaveniaVyuctovania n)
    {
        var p = b.Priestor;
        var adresa = string.Join(", ", new[] { j.Ulica, j.Obec }.Where(s => !string.IsNullOrWhiteSpace(s)));
        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().AlignCenter().Text($"{j.Nazov}{(adresa.Length > 0 ? ", " + adresa : "")}").Bold().FontSize(12);
            col.Item().AlignCenter().Text($"Ročné vyúčtovanie platieb za rok {b.Rok} – {(p.Typ == TypPriestoru.Byt ? "byt" : "nebytový priestor")} č. {p.Cislo}").Bold().FontSize(11);

            // údaje o byte
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(1.1f); c.RelativeColumn(2.4f); c.RelativeColumn(1.3f); c.RelativeColumn(1.2f); });
                void Pole(string nazov, string hodnota)
                {
                    t.Cell().PaddingVertical(1).Text(nazov).FontColor(Colors.Grey.Darken2);
                    t.Cell().PaddingVertical(1).Text(hodnota).Bold();
                }
                Pole("Vlastník:", p.MenaVlastnikov);
                Pole("Plocha bytu:", $"{p.Plocha.ToString("N2", Sk)} m²");
                Pole("Poschodie:", p.Poschodie ?? "");
                Pole("Plocha balkóna:", p.PlochaBalkona > 0 ? $"{p.PlochaBalkona.ToString("N2", Sk)} m²" : "–");
                Pole("Podiel na spol. častiach:", p.PodielMenovatel > 0 ? $"{p.PodielCitatel}/{p.PodielMenovatel}" : "–");
                Pole("Ročná poistka – predpis:", Eur(b.PoistnePredpis));
            });

            // mesačné platby + minulý rok
            col.Item().Row(row =>
            {
                row.RelativeItem(3).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1.3f); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(1.2f); });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "mesiac", "zálohový mesačný predpis", "zaplatené zálohy", "poistenie zapl. v mesiaci", $"vyúčtovanie {b.Rok - 1} zapl. v roku {b.Rok}" })
                            h.Cell().Element(Hlavicka).Text(s).Bold().FontSize(7.5f);
                    });
                    foreach (var m in b.Mesiace)
                    {
                        t.Cell().Element(Bunka).Text(Mesiace[m.Mesiac - 1]);
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(m.Predpis));
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(m.ZaplateneZalohy));
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(m.ZaplatenePoistne));
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(m.ZaplateneMinulyRok));
                    }
                });
                row.ConstantItem(12);
                row.RelativeItem(2).Column(c =>
                {
                    c.Spacing(3);
                    c.Item().Text($"Vyúčtovanie za rok {b.Rok - 1}").Bold();
                    c.Item().Text("(+) preplatok, (−) nedoplatok").FontSize(7).FontColor(Colors.Grey.Darken1);
                    Riadok(c, $"výsledok vyúčtovania {b.Rok - 1}", Eur(b.ZostatokMinulyRok));
                    Riadok(c, b.ZostatokMinulyRok >= 0 ? "vrátené / započítané" : "zaplatené", Eur(b.ZaplateneMinulyRok));
                    Riadok(c, "zostáva", Eur(b.RozdielMinulyRok), true);
                    c.Item().PaddingTop(8).Text($"Poistenie za rok {b.Rok}").Bold();
                    Riadok(c, "predpis", Eur(b.PoistnePredpis));
                    Riadok(c, "zaplatené", Eur(b.PoistneZaplatene));
                    Riadok(c, "rozdiel", Eur(b.RozdielPoistne), true);
                    c.Item().PaddingTop(8).Text($"Zálohy v roku {b.Rok}").Bold();
                    Riadok(c, "predpis v roku", Eur(b.PredpisZaloh));
                    Riadok(c, "zaplatené v banke", Eur(b.ZaplateneZalohy));
                    Riadok(c, "nedoplatok na zálohových platbách", Eur(b.NedoplatokNaZalohach), true);
                });
            });

            // vyúčtovanie nákladov
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(3.2f); c.RelativeColumn(1.3f); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                t.Header(h =>
                {
                    h.Cell().Element(Hlavicka).Text("skutočne zaplatené náklady").Bold();
                    h.Cell().Element(Hlavicka).AlignRight().Text("za celý byt. dom").Bold();
                    h.Cell().Element(Hlavicka).AlignRight().Text("záloha").Bold();
                    h.Cell().Element(Hlavicka).AlignRight().Text("skutočné").Bold();
                    h.Cell().Element(Hlavicka).AlignRight().Text("rozdiel").Bold();
                });
                foreach (var pol in b.Polozky.Where(x => !x.Kategoria.JeFondOprav))
                {
                    var len = Vyuctovanie.VybranePriestory(pol.Kategoria);
                    t.Cell().Element(Bunka).Text(txt =>
                    {
                        txt.Span(pol.Kategoria.Nazov);
                        if (len is not null) txt.Span($" (len byty {string.Join(", ", len)})").FontSize(7);
                    });
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(pol.NakladDomu));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(pol.Zaloha));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(pol.Skutocne));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(pol.Rozdiel)).FontColor(pol.Rozdiel < 0 ? Colors.Red.Darken2 : Colors.Black);
                }
                foreach (var fo in b.Polozky.Where(x => x.Kategoria.JeFondOprav))
                {
                    t.Cell().Element(Bunka).Text($"{fo.Kategoria.Nazov} – predpis za rok {b.Rok}");
                    t.Cell().Element(Bunka).Text("");
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(fo.Zaloha));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(fo.Skutocne));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(fo.Rozdiel));
                }
                t.Cell().ColumnSpan(3).Element(Bunka).AlignRight().Text("skutočné náklady na byt celkom vrátane FO").Bold();
                t.Cell().Element(Bunka).AlignRight().Text(Eur(b.SkutocneSpolu)).Bold();
                t.Cell().Element(Bunka).Text("");
                t.Cell().ColumnSpan(3).Element(Bunka).AlignRight().Text("za skutočne zaplatené náklady").Bold();
                t.Cell().ColumnSpan(2).Element(Bunka).AlignRight().Text($"{(b.VysledokNakladov < 0 ? "nedoplatok" : "preplatok")}  {Eur(b.VysledokNakladov)}").Bold();
            });
            if (!string.IsNullOrWhiteSpace(j.IbanFondOprav) && b.Polozky.Any(x => x.Kategoria.JeFondOprav))
                col.Item().Text($"Fond opráv je vedený na samostatnom účte {Iban.Formatuj(j.IbanFondOprav)}. Na tento účet prosím neuhrádzajte zálohové platby.").FontSize(7.5f).Italic();

            // voda
            if (b.Voda is { } v)
            {
                col.Item().Border(0.5f).BorderColor(Ram).Padding(6).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Spacing(2);
                        c.Item().Text("Informácia o spotrebe vody").Bold();
                        if (!string.IsNullOrWhiteSpace(v.CisloVodomeru)) Riadok(c, "číslo vodomeru", v.CisloVodomeru);
                        c.Item().PaddingTop(4).Text("prehľad spotreby vody za roky:").FontColor(Colors.Blue.Darken2);
                        foreach (var (rok, sp) in v.Historia)
                            Riadok(c, $"rok {rok}", M3(sp));
                    });
                    row.ConstantItem(16);
                    row.RelativeItem(1.4f).Column(c =>
                    {
                        c.Spacing(2);
                        Riadok(c, $"stav vodomera k {Datum(v.DatumZaciatok)}", v.StavZaciatok is { } z ? M3(z) : "–");
                        Riadok(c, $"stav vodomera k {Datum(v.DatumKoniec)}", v.StavKoniec is { } k ? M3(k) : "–");
                        Riadok(c, "spotreba vody za rok spolu", M3(v.Spotreba), true);
                        Riadok(c, $"cena za 1 m³ vodné a stočné v roku {b.Rok}", Eur(v.CenaZaM3) + "/m³");
                        Riadok(c, "cena za vodné a stočné spolu za rok", Eur(v.Naklad));
                        Riadok(c, "zálohová platba za vodu v roku", Eur(v.Zaloha));
                        Riadok(c, "preplatok (+) / nedoplatok (−) za vodu", Eur(v.Rozdiel), true);
                    });
                });
            }

            // sumarizácia
            col.Item().Text("Sumarizácia").Bold().FontSize(11);
            col.Item().Width(360).Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(2.2f); c.RelativeColumn(); });
                void S(string a, decimal d, bool tucne = false)
                {
                    t.Cell().Element(Bunka).Text(a).Bold(tucne);
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(d)).Bold(tucne).FontColor(d < 0 ? Colors.Red.Darken2 : Colors.Black);
                }
                S("nedoplatok na zálohových platbách v predpísaných platbách", b.NedoplatokNaZalohach);
                S($"{(b.VysledokNakladov < 0 ? "nedoplatok" : "preplatok")} za skutočne zaplatené náklady za rok {b.Rok}", b.VysledokNakladov);
                S($"zostatok vyúčtovania za rok {b.Rok - 1}", b.RozdielMinulyRok);
                S($"poistné za rok {b.Rok}", b.RozdielPoistne);
                t.Cell().Background(Tlmene).Element(Bunka).Text(b.Vysledok < 0 ? "DOPLATIŤ" : "PREPLATOK").Bold().FontSize(11);
                t.Cell().Background(Tlmene).Element(Bunka).AlignRight().Text(Eur(Math.Abs(b.Vysledok))).Bold().FontSize(11);
            });
            if (b.Vysledok < 0)
                col.Item().Text(t =>
                {
                    t.Span($"Sumu uhraďte do {n.SplatnostDni} dní odo dňa doručenia tohto vyúčtovania na účet ");
                    t.Span(Iban.Formatuj(j.Iban)).Bold();
                    t.Span($", VS = {p.VariabilnySymbol ?? p.Cislo}.");
                });
            else if (b.Vysledok > 0)
                col.Item().Text("Preplatok vám bude vrátený na účet, z ktorého platíte zálohy, alebo započítaný podľa rozhodnutia spoločenstva.");

            col.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem().AlignBottom().Text($"{n.Miesto}, {Datum(n.DatumVystavenia)}");
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().PaddingTop(24).LineHorizontal(0.5f);
                    c.Item().AlignCenter().Text(j.Statutar ?? "").Bold();
                    c.Item().AlignCenter().Text($"predseda {j.Nazov}").FontSize(7.5f);
                });
            });
        });
    }

    static void Riadok(ColumnDescriptor c, string nazov, string hodnota, bool tucne = false) =>
        c.Item().Row(r =>
        {
            r.RelativeItem().Text(nazov).Bold(tucne);
            r.AutoItem().Text(hodnota).Bold(tucne);
        });

    static IContainer Hlavicka(IContainer c) =>
        c.Border(0.5f).BorderColor(Ram).Background(Tlmene).PaddingVertical(2).PaddingHorizontal(3).AlignMiddle();

    static IContainer Bunka(IContainer c) =>
        c.Border(0.5f).BorderColor(Ram).PaddingVertical(1.5f).PaddingHorizontal(3);
}

static class TextExtensions
{
    public static TextSpanDescriptor Bold(this TextSpanDescriptor t, bool tucne) => tucne ? t.Bold() : t;
}
