using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Uctovnictvo.Core.Model;
using static Uctovnictvo.Core.Services.PdfVyuctovanie;

namespace Uctovnictvo.Core.Services;

/// <summary>Ďalšie tlačivá spoločenstva: upomienka, predpis platieb (evidenčný list), prezenčná listina, ročná správa.</summary>
public static class PdfDokumenty
{
    static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");
    static readonly string[] Mesiace = { "január", "február", "marec", "apríl", "máj", "jún", "júl", "august", "september", "október", "november", "december" };

    static PdfDokumenty() => QuestPDF.Settings.License = LicenseType.Community;

    static Document Strany<T>(IEnumerable<T> polozky, Action<IContainer, T> obsah, string titulok) =>
        Document.Create(doc =>
        {
            foreach (var x in polozky)
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(t => t.FontSize(9.5f).FontFamily("Arial", "Lato"));
                    page.Content().Element(c => obsah(c, x));
                });
        }).WithMetadata(new DocumentMetadata { Title = titulok, Creator = "Účtovníctvo" });

    static void Hlavicka(ColumnDescriptor col, UctovnaJednotka j)
    {
        col.Item().Text(j.Nazov).Bold().FontSize(12);
        var adresa = string.Join(", ", new[] { j.Ulica, string.Join(" ", new[] { j.Psc, j.Obec }.Where(s => !string.IsNullOrWhiteSpace(s))) }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (adresa.Length > 0) col.Item().Text(adresa);
        if (!string.IsNullOrWhiteSpace(j.Ico)) col.Item().Text($"IČO: {j.Ico}");
        col.Item().PaddingVertical(4).LineHorizontal(0.5f);
    }

    static void Adresat(ColumnDescriptor col, IEnumerable<Vlastnik> vlastnici)
    {
        var v = vlastnici.FirstOrDefault();
        if (v is null) return;
        col.Item().AlignRight().Width(230).Column(c =>
        {
            c.Item().Text(string.Join(", ", vlastnici.Select(x => x.Meno))).Bold();
            if (!string.IsNullOrWhiteSpace(v.Ulica)) c.Item().Text(v.Ulica);
            var obec = string.Join(" ", new[] { v.Psc, v.Obec }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (obec.Length > 0) c.Item().Text(obec);
        });
    }

    static void Podpis(ColumnDescriptor col, UctovnaJednotka j, string miesto, DateOnly datum)
    {
        col.Item().PaddingTop(24).Row(row =>
        {
            row.RelativeItem().AlignBottom().Text($"{miesto}, {Datum(datum)}");
            row.RelativeItem().AlignRight().Column(c =>
            {
                c.Item().PaddingTop(24).LineHorizontal(0.5f);
                c.Item().AlignCenter().Text(j.Statutar ?? "").Bold();
                c.Item().AlignCenter().Text($"za {j.Nazov}").FontSize(8);
            });
        });
    }

    static void Qr(RowDescriptor row, UctovnaJednotka j, decimal suma, string vs, string poznamka)
    {
        if (!Iban.JePlatny(j.Iban) || suma <= 0) return;
        row.ConstantItem(12);
        row.ConstantItem(95).Column(c =>
        {
            c.Item().Image(PayBySquare.QrPng(new PlatbaQr(j.Iban!, suma, vs, Poznamka: poznamka, Prijemca: j.Nazov)));
            c.Item().AlignCenter().Text("PAY by square").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    // ---------- upomienka ----------

    public static void Upomienky(UctovnaJednotka j, IEnumerable<KontoPriestoru> kont, string miesto, DateOnly datum, int splatnostDni, string path) =>
        Strany(kont, (c, k) => Upomienka(c, j, k, miesto, datum, splatnostDni), $"Upomienky {j.Nazov}").GeneratePdf(path);

    public static byte[] Upomienka(UctovnaJednotka j, KontoPriestoru k, string miesto, DateOnly datum, int splatnostDni) =>
        Strany(new[] { k }, (c, x) => Upomienka(c, j, x, miesto, datum, splatnostDni), "Upomienka").GeneratePdf();

    static void Upomienka(IContainer container, UctovnaJednotka j, KontoPriestoru k, string miesto, DateOnly datum, int splatnostDni)
    {
        var p = k.Priestor;
        var vs = p.VariabilnySymbol ?? p.Cislo;
        container.Column(col =>
        {
            col.Spacing(8);
            Hlavicka(col, j);
            Adresat(col, p.SucasniVlastnici);
            col.Item().PaddingTop(10).Text($"Upomienka – neuhradené platby za {(p.Typ == TypPriestoru.Byt ? "byt" : "priestor")} č. {p.Cislo}").Bold().FontSize(12);
            col.Item().Text($"Podľa našej evidencie k {Datum(new DateOnly(k.Rok, k.KuMesiacu, 1).AddMonths(1).AddDays(-1))} " +
                            $"evidujeme nedoplatok vo výške {Eur(k.Dlh)}. Prehľad predpisu a platieb v roku {k.Rok}:");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(1.4f); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                t.Header(h =>
                {
                    foreach (var s in new[] { "mesiac", "predpis", "zaplatené", "zostatok (+ / −)" })
                        h.Cell().Element(PdfVyuctovanie.Hlavicka).Text(s).Bold();
                });
                if (k.ZostatokMinulyRok != 0)
                {
                    t.Cell().Element(Bunka).Text($"zostatok z roka {k.Rok - 1}");
                    t.Cell().Element(Bunka).Text("");
                    t.Cell().Element(Bunka).Text("");
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(k.ZostatokMinulyRok));
                }
                foreach (var m in k.Mesiace.Where(m => m.Mesiac <= k.KuMesiacu))
                {
                    t.Cell().Element(Bunka).Text(Mesiace[m.Mesiac - 1]);
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(m.Predpis));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(m.Zaplatene));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(m.Zostatok)).FontColor(m.Zostatok < 0 ? Colors.Red.Darken2 : Colors.Black);
                }
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(t =>
                {
                    t.Span($"Prosíme o úhradu sumy ");
                    t.Span(Eur(k.Dlh)).Bold();
                    t.Span($" do {splatnostDni} dní na účet ");
                    t.Span(Iban.Formatuj(j.Iban)).Bold();
                    t.Span($", variabilný symbol {vs}. Ak ste medzitým zaplatili, považujte túto upomienku za bezpredmetnú.");
                });
                Qr(row, j, k.Dlh, vs, $"Nedoplatok byt {p.Cislo}");
            });
            Podpis(col, j, miesto, datum);
        });
    }

    // ---------- predpis platieb (evidenčný list) ----------

    public record PredpisBytu(Priestor Priestor, int Rok, IReadOnlyList<(string Nazov, decimal Rocne)> Polozky, decimal Poistne, DateOnly PlatnyOd)
    {
        public decimal Rocne => Polozky.Sum(x => x.Rocne);
        public decimal Mesacne => Math.Round(Rocne / 12m, 2, MidpointRounding.AwayFromZero);
    }

    public static PredpisBytu Predpis(Priestor p, IEnumerable<KategoriaNakladov> kategorie, int rok, DateOnly platnyOd) =>
        new(p, rok,
            kategorie.OrderBy(k => k.Poradie).ThenBy(k => k.Id)
                .Select(k => (k.Nazov, p.Predpisy.Where(x => x.Rok == rok && x.KategoriaId == k.Id).Sum(x => x.RocnaSuma)))
                .Where(x => x.Item2 != 0).ToList(),
            p.Roky.FirstOrDefault(r => r.Rok == rok)?.PoistnePredpis ?? 0m,
            platnyOd);

    public static void EvidencneListy(UctovnaJednotka j, IEnumerable<PredpisBytu> predpisy, string miesto, DateOnly datum, string path) =>
        Strany(predpisy, (c, x) => EvidencnyList(c, j, x, miesto, datum), $"Predpis platieb {j.Nazov}").GeneratePdf(path);

    public static byte[] EvidencnyList(UctovnaJednotka j, PredpisBytu pr, string miesto, DateOnly datum) =>
        Strany(new[] { pr }, (c, x) => EvidencnyList(c, j, x, miesto, datum), "Predpis platieb").GeneratePdf();

    static void EvidencnyList(IContainer container, UctovnaJednotka j, PredpisBytu pr, string miesto, DateOnly datum)
    {
        var p = pr.Priestor;
        var vs = p.VariabilnySymbol ?? p.Cislo;
        container.Column(col =>
        {
            col.Spacing(8);
            Hlavicka(col, j);
            Adresat(col, p.SucasniVlastnici);
            col.Item().PaddingTop(10).Text($"Evidenčný list – predpis mesačných platieb na rok {pr.Rok}").Bold().FontSize(12);
            col.Item().Text($"{(p.Typ == TypPriestoru.Byt ? "Byt" : "Priestor")} č. {p.Cislo}, plocha {p.Plocha.ToString("N2", Sk)} m², " +
                            $"podiel {(p.PodielMenovatel > 0 ? $"{p.PodielCitatel}/{p.PodielMenovatel}" : "–")}, počet osôb {p.PocetOsob}. Platí od {Datum(pr.PlatnyOd)}.");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                t.Header(h =>
                {
                    h.Cell().Element(PdfVyuctovanie.Hlavicka).Text("položka").Bold();
                    h.Cell().Element(PdfVyuctovanie.Hlavicka).AlignRight().Text("ročne").Bold();
                    h.Cell().Element(PdfVyuctovanie.Hlavicka).AlignRight().Text("mesačne").Bold();
                });
                foreach (var (nazov, rocne) in pr.Polozky)
                {
                    t.Cell().Element(Bunka).Text(nazov);
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(rocne));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(Math.Round(rocne / 12m, 2, MidpointRounding.AwayFromZero)));
                }
                t.Cell().Element(Bunka).Text("mesačná platba spolu").Bold();
                t.Cell().Element(Bunka).AlignRight().Text(Eur(pr.Rocne)).Bold();
                t.Cell().Element(Bunka).AlignRight().Text(Eur(pr.Mesacne)).Bold();
            });
            if (pr.Poistne > 0)
                col.Item().Text($"Ročné poistenie bytu {Eur(pr.Poistne)} sa platí jednorazovo spolu s februárovou platbou.");
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(t =>
                {
                    t.Span("Mesačnú platbu ");
                    t.Span(Eur(pr.Mesacne)).Bold();
                    t.Span(" uhrádzajte vždy do konca mesiaca na účet ");
                    t.Span(Iban.Formatuj(j.Iban)).Bold();
                    t.Span($", variabilný symbol {vs}. Odporúčame nastaviť si trvalý príkaz. QR kód vpravo vyplní jednu mesačnú platbu.");
                });
                Qr(row, j, pr.Mesacne, vs, $"Zalohy byt {p.Cislo}");
            });
            Podpis(col, j, miesto, datum);
        });
    }

    // ---------- prezenčná listina ----------

    public static void PrezencnaListina(UctovnaJednotka j, IReadOnlyList<Priestor> priestory, DateOnly datumSchodze, string path)
    {
        var spoluPodiel = priestory.Sum(p => p.PodielMenovatel > 0 ? (decimal)p.PodielCitatel / p.PodielMenovatel : 0m);
        var spoluPlocha = priestory.Sum(p => p.Plocha);
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial", "Lato"));
            page.Header().Column(col =>
            {
                col.Item().Text(j.Nazov).Bold().FontSize(12);
                col.Item().Text($"Prezenčná listina – schôdza vlastníkov dňa {Datum(datumSchodze)}").Bold().FontSize(11);
                col.Item().PaddingBottom(6).Text("Hlas = spoluvlastnícky podiel na spoločných častiach (ak nie je zadaný, podiel plochy).").FontSize(7.5f).Italic();
            });
            page.Content().Table(t =>
            {
                t.ColumnsDefinition(c => { c.ConstantColumn(40); c.RelativeColumn(3); c.ConstantColumn(60); c.ConstantColumn(55); c.RelativeColumn(2); });
                t.Header(h =>
                {
                    foreach (var s in new[] { "byt", "vlastník", "podiel", "hlas %", "podpis / splnomocnenec" })
                        h.Cell().Element(PdfVyuctovanie.Hlavicka).Text(s).Bold();
                });
                foreach (var p in priestory.OrderBy(p => p.Cislo, CisloComparer.Instance))
                {
                    var hlas = spoluPodiel > 0 && p.PodielMenovatel > 0 ? (decimal)p.PodielCitatel / p.PodielMenovatel / spoluPodiel
                        : spoluPlocha > 0 ? p.Plocha / spoluPlocha : 0m;
                    t.Cell().Element(Bunka).PaddingVertical(5).Text(p.Cislo);
                    t.Cell().Element(Bunka).PaddingVertical(5).Text(p.MenaVlastnikov);
                    t.Cell().Element(Bunka).PaddingVertical(5).Text(p.PodielMenovatel > 0 ? $"{p.PodielCitatel}/{p.PodielMenovatel}" : "");
                    t.Cell().Element(Bunka).PaddingVertical(5).AlignRight().Text((hlas * 100).ToString("N2", Sk));
                    t.Cell().Element(Bunka).PaddingVertical(5).Text("");
                }
            });
            page.Footer().AlignRight().Text(t => { t.Span("strana "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        })).GeneratePdf(path);
    }

    // ---------- ročná správa o hospodárení ----------

    public static void RocnaSprava(UctovnaJednotka j, VysledokVyuctovania v, StavFondu fond, IReadOnlyList<KontoPriestoru> kont,
        string miesto, DateOnly datum, string path)
    {
        var kategorie = v.Byty.FirstOrDefault()?.Polozky.Select(p => p.Kategoria).Where(k => !k.JeFondOprav).ToList() ?? new();
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(9.5f).FontFamily("Arial", "Lato"));
            page.Content().Column(col =>
            {
                col.Spacing(8);
                Hlavicka(col, j);
                col.Item().Text($"Správa o hospodárení za rok {v.Rok}").Bold().FontSize(13);

                col.Item().Text("1. Náklady domu a zálohy vlastníkov").Bold();
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "položka", "skutočný náklad", "predpísané zálohy", "rozdiel" })
                            h.Cell().Element(PdfVyuctovanie.Hlavicka).Text(s).Bold();
                    });
                    decimal sn = 0, sz = 0;
                    foreach (var k in kategorie)
                    {
                        var naklad = Vyuctovanie.NakladDomu(k, v.Rok);
                        var zalohy = v.Byty.Sum(b => b.Polozky.Where(p => p.Kategoria.Id == k.Id).Sum(p => p.Zaloha));
                        sn += naklad; sz += zalohy;
                        t.Cell().Element(Bunka).Text(k.Nazov);
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(naklad));
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(zalohy));
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(zalohy - naklad));
                    }
                    t.Cell().Element(Bunka).Text("spolu").Bold();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(sn)).Bold();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(sz)).Bold();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(sz - sn)).Bold();
                });

                col.Item().Text("2. Fond prevádzky, údržby a opráv").Bold();
                col.Item().Width(330).Column(c =>
                {
                    c.Spacing(2);
                    Riadok(c, $"stav k 1. 1. {v.Rok}", Eur(fond.Zaciatok));
                    Riadok(c, "tvorba z predpisov vlastníkov", Eur(fond.Tvorba));
                    Riadok(c, "iné príjmy", Eur(fond.InePrijmy));
                    Riadok(c, "čerpanie", Eur(-fond.Cerpanie));
                    Riadok(c, $"stav k 31. 12. {v.Rok}", Eur(fond.Koniec), true);
                });
                if (fond.Pohyby.Count > 0)
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(70); c.RelativeColumn(); c.ConstantColumn(90); });
                        foreach (var x in fond.Pohyby)
                        {
                            t.Cell().Element(Bunka).Text(Datum(x.Datum));
                            t.Cell().Element(Bunka).Text(x.Popis);
                            t.Cell().Element(Bunka).AlignRight().Text(Eur(x.Typ == TypPohybuFondu.Cerpanie ? -x.Suma : x.Suma));
                        }
                    });

                col.Item().Text("3. Výsledky vyúčtovania a nedoplatky").Bold();
                var preplatky = v.Byty.Where(b => b.Vysledok > 0).ToList();
                var nedoplatky = v.Byty.Where(b => b.Vysledok < 0).ToList();
                var dlznici = kont.Where(k => k.Dlh > 0).ToList();
                col.Item().Width(330).Column(c =>
                {
                    c.Spacing(2);
                    Riadok(c, $"preplatky vyúčtovania ({preplatky.Count} bytov)", Eur(preplatky.Sum(b => b.Vysledok)));
                    Riadok(c, $"nedoplatky vyúčtovania ({nedoplatky.Count} bytov)", Eur(nedoplatky.Sum(b => b.Vysledok)));
                    Riadok(c, $"neuhradené predpisy k 31. 12. ({dlznici.Count} bytov)", Eur(-dlznici.Sum(k => k.Dlh)));
                });
                Podpis(col, j, miesto, datum);
            });
        })).GeneratePdf(path);
    }
}
