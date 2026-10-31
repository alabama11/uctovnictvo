using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Uctovnictvo.Core.Model;
using static Uctovnictvo.Core.Services.PdfVyuctovanie;

namespace Uctovnictvo.Core.Services;

/// <summary>Tlač faktúry (bez DPH, s QR PAY by square), upomienky k faktúre, pokladničného dokladu a pokladničnej knihy.</summary>
public static class PdfFaktury
{
    static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");
    const string Ram = "#444444";
    const string Tlmene = "#EEF2F6";

    static PdfFaktury() => QuestPDF.Settings.License = LicenseType.Community;

    static string Mn(decimal d) => d.ToString(d == Math.Round(d) ? "N0" : "0.###", Sk);

    static string Adresa(UctovnaJednotka j) =>
        string.Join(", ", new[] { j.Ulica, string.Join(" ", new[] { j.Psc, j.Obec }.Where(s => !string.IsNullOrWhiteSpace(s))) }.Where(s => !string.IsNullOrWhiteSpace(s)));

    static void Strana(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(t => t.FontSize(9.5f).FontFamily("Arial", "Lato"));
    }

    public static byte[] Faktura(Faktura f, UctovnaJednotka j) => Document.Create(doc => doc.Page(page =>
    {
        Strana(page);
        page.Content().Column(col =>
        {
            col.Spacing(8);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(Faktury.NazovTypu(f.Typ)).FontSize(18).Bold();
                r.AutoItem().AlignBottom().Text($"č. {f.Cislo}").FontSize(14).Bold();
            });
            col.Item().LineHorizontal(1);

            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("Dodávateľ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(j.Nazov).Bold().FontSize(11);
                    var a = Adresa(j);
                    if (a.Length > 0) c.Item().Text(a);
                    if (!string.IsNullOrWhiteSpace(j.Ico)) c.Item().Text($"IČO: {j.Ico}");
                    if (!string.IsNullOrWhiteSpace(j.Dic)) c.Item().Text($"DIČ: {j.Dic}");
                    c.Item().Text("Nie je platiteľom DPH.");
                    if (!string.IsNullOrWhiteSpace(j.ZapisVRegistri)) c.Item().PaddingTop(2).Text(j.ZapisVRegistri).FontSize(7.5f);
                    if (!string.IsNullOrWhiteSpace(j.Email) || !string.IsNullOrWhiteSpace(j.Telefon))
                        c.Item().PaddingTop(2).Text(string.Join("   ", new[] { j.Telefon, j.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))).FontSize(8.5f);
                });
                r.ConstantItem(16);
                r.RelativeItem().Border(0.75f).BorderColor(Ram).Padding(8).Column(c =>
                {
                    c.Item().Text("Odberateľ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(f.PartnerNazov).Bold().FontSize(11);
                    foreach (var riadok in (f.PartnerAdresa ?? "").Split(", ", StringSplitOptions.RemoveEmptyEntries))
                        c.Item().Text(riadok);
                    if (!string.IsNullOrWhiteSpace(f.PartnerIco)) c.Item().PaddingTop(3).Text($"IČO: {f.PartnerIco}");
                    if (!string.IsNullOrWhiteSpace(f.PartnerDic)) c.Item().Text($"DIČ: {f.PartnerDic}");
                    if (!string.IsNullOrWhiteSpace(f.PartnerIcDph)) c.Item().Text($"IČ DPH: {f.PartnerIcDph}");
                });
            });

            var iban = f.FormaUhrady == FormaUhrady.Prevod && Iban.JePlatny(j.Iban) ? j.Iban : null;
            col.Item().Background(Tlmene).Padding(8).Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    Riadok(c, "Dátum vystavenia:", Datum(f.DatumVystavenia));
                    if (f.DatumDodania is { } dd) Riadok(c, "Dátum dodania:", Datum(dd));
                    Riadok(c, "Dátum splatnosti:", Datum(f.DatumSplatnosti), true);
                    Riadok(c, "Forma úhrady:", Faktury.NazovFormy(f.FormaUhrady));
                });
                r.ConstantItem(24);
                r.RelativeItem().Column(c =>
                {
                    if (iban is not null) Riadok(c, "IBAN:", Iban.Formatuj(iban), true);
                    if (!string.IsNullOrWhiteSpace(f.VariabilnySymbol)) Riadok(c, "Variabilný symbol:", f.VariabilnySymbol!, true);
                    if (!string.IsNullOrWhiteSpace(f.KonstantnySymbol)) Riadok(c, "Konštantný symbol:", f.KonstantnySymbol!);
                    if (!string.IsNullOrWhiteSpace(f.PovodnaFaktura)) Riadok(c, "K faktúre č.:", f.PovodnaFaktura!);
                });
            });

            if (!string.IsNullOrWhiteSpace(f.TextNad)) col.Item().Text(f.TextNad);

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(24);
                    cd.RelativeColumn();
                    cd.ConstantColumn(54);
                    cd.ConstantColumn(40);
                    cd.ConstantColumn(72);
                    cd.ConstantColumn(78);
                });
                t.Header(h =>
                {
                    h.Cell().Element(Hlavicka).Text("#").Bold();
                    h.Cell().Element(Hlavicka).Text("Popis").Bold();
                    h.Cell().Element(Hlavicka).AlignRight().Text("Množstvo").Bold();
                    h.Cell().Element(Hlavicka).Text("MJ").Bold();
                    h.Cell().Element(Hlavicka).AlignRight().Text("Cena za MJ").Bold();
                    h.Cell().Element(Hlavicka).AlignRight().Text("Spolu").Bold();
                });
                var i = 1;
                foreach (var p in f.Polozky.OrderBy(p => p.Poradie))
                {
                    t.Cell().Element(Bunka).Text($"{i++}");
                    t.Cell().Element(Bunka).Text(p.Popis);
                    t.Cell().Element(Bunka).AlignRight().Text(Mn(p.Mnozstvo));
                    t.Cell().Element(Bunka).Text(p.Jednotka ?? "");
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(p.CenaZaJednotku));
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(p.Suma));
                }
            });

            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    if (iban is not null && f.Zostava > 0)
                        c.Item().Width(92).Column(q =>
                        {
                            q.Item().Image(PayBySquare.QrPng(new PlatbaQr(iban, f.Zostava, f.VariabilnySymbol, f.KonstantnySymbol,
                                Poznamka: $"Faktura {f.Cislo}", Splatnost: f.DatumSplatnosti, Prijemca: j.Nazov)));
                            q.Item().AlignCenter().Text("PAY by square").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                        });
                });
                r.ConstantItem(230).Column(c =>
                {
                    Riadok(c, "Spolu:", Eur(f.Suma));
                    if (f.Uhradene != 0) Riadok(c, "Uhradené:", Eur(-f.Uhradene));
                    c.Item().PaddingTop(4).Background(Tlmene).Padding(6).Row(rr =>
                    {
                        rr.RelativeItem().Text(f.Suma < 0 ? "Na vrátenie:" : "K úhrade:").Bold().FontSize(12);
                        rr.AutoItem().Text(Eur(Math.Abs(f.Zostava))).Bold().FontSize(12);
                    });
                    c.Item().PaddingTop(3).AlignRight().Text("Dodávateľ nie je platiteľom DPH.").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            if (!string.IsNullOrWhiteSpace(f.TextPod)) col.Item().PaddingTop(6).Text(f.TextPod);
            if (f.Typ == TypFaktury.Zalohova)
                col.Item().PaddingTop(6).Text("Zálohová faktúra nie je daňovým dokladom. Po prijatí platby bude vystavená vyúčtovacia faktúra.").FontSize(8);

            col.Item().PaddingTop(30).Row(r =>
            {
                r.RelativeItem();
                r.ConstantItem(200).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f);
                    c.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(j.Statutar) ? "Vystavil" : $"Vystavil: {j.Statutar}").FontSize(8);
                });
            });
        });
        page.Footer().AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Colors.Grey.Darken1));
            t.Span($"{Faktury.NazovTypu(f.Typ)} {f.Cislo} · strana ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    })).WithMetadata(new DocumentMetadata { Title = $"{Faktury.NazovTypu(f.Typ)} {f.Cislo}", Creator = "Účtovníctvo" }).GeneratePdf();

    /// <summary>Upomienka odberateľovi so zoznamom jeho faktúr po splatnosti.</summary>
    public static byte[] Upomienka(IReadOnlyList<Faktura> faktury, UctovnaJednotka j, DateOnly datum, int lehotaDni = 7) =>
        Document.Create(doc => doc.Page(page =>
        {
            Strana(page);
            var prva = faktury[0];
            page.Content().Column(col =>
            {
                col.Spacing(8);
                col.Item().Text(j.Nazov).Bold().FontSize(12);
                var a = Adresa(j);
                if (a.Length > 0) col.Item().Text(a + (string.IsNullOrWhiteSpace(j.Ico) ? "" : $", IČO: {j.Ico}"));
                col.Item().LineHorizontal(0.5f);
                col.Item().AlignRight().Width(240).Column(c =>
                {
                    c.Item().Text(prva.PartnerNazov).Bold();
                    foreach (var riadok in (prva.PartnerAdresa ?? "").Split(", ", StringSplitOptions.RemoveEmptyEntries)) c.Item().Text(riadok);
                });
                col.Item().PaddingTop(10).Text($"{j.Obec ?? ""}{(string.IsNullOrWhiteSpace(j.Obec) ? "" : ", ")}{Datum(datum)}");
                col.Item().PaddingTop(6).Text("Upomienka – výzva na úhradu po splatnosti").Bold().FontSize(13);
                col.Item().Text("Dovoľujeme si vás upozorniť, že podľa našej evidencie nie sú uhradené tieto faktúry:");
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(70); cd.ConstantColumn(70); cd.ConstantColumn(60); cd.ConstantColumn(80); });
                    t.Header(h =>
                    {
                        h.Cell().Element(Hlavicka).Text("Faktúra").Bold();
                        h.Cell().Element(Hlavicka).Text("Vystavená").Bold();
                        h.Cell().Element(Hlavicka).Text("Splatná").Bold();
                        h.Cell().Element(Hlavicka).AlignRight().Text("Dní po spl.").Bold();
                        h.Cell().Element(Hlavicka).AlignRight().Text("Dlžná suma").Bold();
                    });
                    foreach (var f in faktury)
                    {
                        t.Cell().Element(Bunka).Text($"{f.Cislo} (VS {f.VariabilnySymbol})");
                        t.Cell().Element(Bunka).Text(Datum(f.DatumVystavenia));
                        t.Cell().Element(Bunka).Text(Datum(f.DatumSplatnosti));
                        t.Cell().Element(Bunka).AlignRight().Text($"{Math.Max(0, datum.DayNumber - f.DatumSplatnosti.DayNumber)}");
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(f.Zostava));
                    }
                    t.Cell().ColumnSpan(4).Element(Bunka).Text("Spolu").Bold();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(faktury.Sum(f => f.Zostava))).Bold();
                });
                var iban = Iban.JePlatny(j.Iban) ? Iban.Formatuj(j.Iban) : "(uveďte IBAN v údajoch firmy)";
                col.Item().Text($"Prosíme vás o úhradu dlžnej sumy do {Datum(datum.AddDays(lehotaDni))} na účet {iban} s uvedením variabilného symbolu faktúry. Ak ste medzitým zaplatili, považujte túto upomienku za bezpredmetnú.");
                if (faktury.Count == 1 && Iban.JePlatny(j.Iban))
                    col.Item().Width(92).Image(PayBySquare.QrPng(new PlatbaQr(j.Iban!, prva.Zostava, prva.VariabilnySymbol, Poznamka: $"Faktura {prva.Cislo}", Prijemca: j.Nazov)));
                col.Item().PaddingTop(30).AlignRight().Width(200).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f);
                    c.Item().AlignCenter().Text(j.Statutar ?? j.Nazov).FontSize(8.5f);
                });
            });
        })).WithMetadata(new DocumentMetadata { Title = "Upomienka", Creator = "Účtovníctvo" }).GeneratePdf();

    public static byte[] PokladnicnyDoklad(PokladnicnyDoklad d, UctovnaJednotka j) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A5.Landscape());
        page.Margin(28);
        page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial", "Lato"));
        page.Content().Border(1).BorderColor(Ram).Padding(12).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text(j.Nazov).Bold();
                    var a = Adresa(j);
                    if (a.Length > 0) c.Item().Text(a).FontSize(8.5f);
                    if (!string.IsNullOrWhiteSpace(j.Ico)) c.Item().Text($"IČO: {j.Ico}").FontSize(8.5f);
                });
                r.AutoItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text(d.Druh == DruhPokladnicnehoDokladu.Prijem ? "PRÍJMOVÝ POKLADNIČNÝ DOKLAD" : "VÝDAVKOVÝ POKLADNIČNÝ DOKLAD").Bold().FontSize(12);
                    c.Item().AlignRight().Text($"č. {d.Cislo}").Bold();
                    c.Item().AlignRight().Text($"zo dňa {Datum(d.Datum)}");
                });
            });
            col.Item().LineHorizontal(0.5f);
            Riadok(col, d.Druh == DruhPokladnicnehoDokladu.Prijem ? "Prijaté od:" : "Vyplatené komu:", d.Osoba ?? "");
            Riadok(col, "Účel platby:", d.Ucel);
            col.Item().Background(Tlmene).Padding(6).Row(r =>
            {
                r.RelativeItem().Text("Suma:").Bold().FontSize(12);
                r.AutoItem().Text(Eur(d.Suma)).Bold().FontSize(12);
            });
            col.Item().Text($"Slovom: {Slovom.Eura(d.Suma)}").FontSize(9);
            col.Item().Text("Nie je platiteľom DPH.").FontSize(8);
            col.Item().PaddingTop(28).Row(r =>
            {
                foreach (var popis in new[] { "Vyhotovil", "Schválil", d.Druh == DruhPokladnicnehoDokladu.Prijem ? "Prijal (pokladník)" : "Príjemca" })
                {
                    r.RelativeItem().PaddingHorizontal(8).Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().AlignCenter().Text(popis).FontSize(8);
                    });
                }
            });
        });
    })).WithMetadata(new DocumentMetadata { Title = d.Cislo, Creator = "Účtovníctvo" }).GeneratePdf();

    public static byte[] PokladnicnaKniha(UctovnaJednotka j, int rok, decimal zaciatok, IReadOnlyList<RiadokPokladne> riadky) =>
        Document.Create(doc => doc.Page(page =>
        {
            Strana(page);
            page.Content().Column(col =>
            {
                col.Spacing(6);
                col.Item().Text($"Pokladničná kniha {rok}").FontSize(15).Bold();
                col.Item().Text($"{j.Nazov}{(string.IsNullOrWhiteSpace(j.Ico) ? "" : $", IČO {j.Ico}")}");
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.ConstantColumn(58); cd.ConstantColumn(72); cd.RelativeColumn(); cd.ConstantColumn(66); cd.ConstantColumn(66); cd.ConstantColumn(72); });
                    t.Header(h =>
                    {
                        foreach (var (n, prava) in new[] { ("Dátum", false), ("Doklad", false), ("Účel / osoba", false), ("Príjem", true), ("Výdavok", true), ("Zostatok", true) })
                        {
                            var c = h.Cell().Element(Hlavicka);
                            (prava ? c.AlignRight() : c).Text(n).Bold();
                        }
                    });
                    t.Cell().ColumnSpan(5).Element(Bunka).Text($"Zostatok k 1. 1. {rok}").Italic();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(zaciatok));
                    foreach (var r in riadky)
                    {
                        t.Cell().Element(Bunka).Text(Datum(r.Doklad.Datum));
                        t.Cell().Element(Bunka).Text(r.Doklad.Cislo);
                        t.Cell().Element(Bunka).Text(string.IsNullOrWhiteSpace(r.Doklad.Osoba) ? r.Doklad.Ucel : $"{r.Doklad.Ucel} – {r.Doklad.Osoba}");
                        t.Cell().Element(Bunka).AlignRight().Text(r.Prijem == 0 ? "" : Eur(r.Prijem));
                        t.Cell().Element(Bunka).AlignRight().Text(r.Vydavok == 0 ? "" : Eur(r.Vydavok));
                        t.Cell().Element(Bunka).AlignRight().Text(Eur(r.Zostatok));
                    }
                    t.Cell().ColumnSpan(3).Element(Bunka).Text("Spolu").Bold();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(riadky.Sum(r => r.Prijem))).Bold();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(riadky.Sum(r => r.Vydavok))).Bold();
                    t.Cell().Element(Bunka).AlignRight().Text(Eur(riadky.Count > 0 ? riadky[^1].Zostatok : zaciatok)).Bold();
                });
            });
            page.Footer().AlignCenter().Text(t => { t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        })).WithMetadata(new DocumentMetadata { Title = $"Pokladničná kniha {rok}", Creator = "Účtovníctvo" }).GeneratePdf();
}

/// <summary>Suma slovom pre pokladničné doklady (napr. 125,50 → „stodvadsaťpäť eur 50 centov“).</summary>
public static class Slovom
{
    static readonly string[] Jednotky = { "", "jeden", "dva", "tri", "štyri", "päť", "šesť", "sedem", "osem", "deväť", "desať",
        "jedenásť", "dvanásť", "trinásť", "štrnásť", "pätnásť", "šestnásť", "sedemnásť", "osemnásť", "devätnásť" };
    static readonly string[] Desiatky = { "", "", "dvadsať", "tridsať", "štyridsať", "päťdesiat", "šesťdesiat", "sedemdesiat", "osemdesiat", "deväťdesiat" };
    static readonly string[] Stovky = { "", "sto", "dvesto", "tristo", "štyristo", "päťsto", "šesťsto", "sedemsto", "osemsto", "deväťsto" };

    public static string Eura(decimal suma)
    {
        suma = Math.Round(Math.Abs(suma), 2);
        var cele = (long)Math.Floor(suma);
        var centy = (int)((suma - cele) * 100);
        var s = cele == 0 ? "nula" : Cislo(cele);
        var eur = cele == 1 ? "euro" : cele is >= 2 and <= 4 ? "eurá" : "eur";
        return centy == 0 ? $"{s} {eur}" : $"{s} {eur} {centy} {(centy == 1 ? "cent" : centy is >= 2 and <= 4 ? "centy" : "centov")}";
    }

    public static string Cislo(long n)
    {
        if (n == 0) return "";
        if (n >= 1_000_000)
        {
            var mil = n / 1_000_000;
            var slovo = mil == 1 ? "milión" : mil is >= 2 and <= 4 ? "milióny" : "miliónov";
            return (mil == 1 ? "jeden" : Cislo(mil)) + slovo + Cislo(n % 1_000_000);
        }
        if (n >= 1000)
        {
            var tis = n / 1000;
            var pred = tis == 1 ? "" : tis == 2 ? "dve" : Cislo(tis);
            return pred + "tisíc" + Cislo(n % 1000);
        }
        var st = n / 100;
        var zv = n % 100;
        var s = Stovky[st];
        if (zv < 20) return s + Jednotky[zv];
        return s + Desiatky[zv / 10] + Jednotky[zv % 10];
    }
}
