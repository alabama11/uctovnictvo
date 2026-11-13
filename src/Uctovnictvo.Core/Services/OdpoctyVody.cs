using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Jeden byt z tlačiva odpočtov: stĺpce 1, 2, 3, 6, 7 a 8.</summary>
public record RiadokTlacivaVody(string Byt, string? Vodomer1, decimal? Stav2, decimal? Stav3, string? Vodomer2, decimal? Stav7, decimal? Stav8);

public record TlacivoVody(
    DateOnly? Datum1, DateOnly? Datum2, DateOnly? Datum3, DateOnly? Datum4,
    decimal? Cena1, decimal? Cena2,
    IReadOnlyList<RiadokTlacivaVody> Riadky);

/// <summary>
/// „Tlačivo o prehľade spotreby vody podľa bytových podružných vodomerov“ – import a export v Exceli.
/// Stĺpce sa hľadajú podľa riadku „č. stĺpca“ s číslami 1 až 13, hlavička podľa textov
/// „1. dátum odpočtu ku dňu“ … „4. dátum“ a „1. / 2. cena vodné a stočné za 1 m3 vody“.
/// </summary>
public static partial class OdpoctyVody
{
    public static TlacivoVody Nacitaj(string path)
    {
        using var wb = new XLWorkbook(path);
        foreach (var ws in wb.Worksheets)
        {
            var t = Nacitaj(ws);
            if (t is not null) return t;
        }
        throw new InvalidDataException("V súbore sa nenašlo tlačivo odpočtov vody: chýba riadok „č. stĺpca“ s číslami stĺpcov 1 až 13.");
    }

    static TlacivoVody? Nacitaj(IXLWorksheet ws)
    {
        var used = ws.RangeUsed();
        if (used is null) return null;
        int maxR = used.LastRow().RowNumber(), maxC = used.LastColumn().ColumnNumber();

        // riadok s číslami stĺpcov
        IXLCell? cStlpca = null;
        for (int r = 1; r <= maxR && cStlpca is null; r++)
            for (int c = 1; c <= maxC; c++)
                if (Norm(ws.Cell(r, c)).StartsWith("c. stlpca") || Norm(ws.Cell(r, c)).StartsWith("c.stlpca") || Norm(ws.Cell(r, c)).StartsWith("cislo stlpca"))
                { cStlpca = ws.Cell(r, c); break; }
        if (cStlpca is null) return null;
        int hr = cStlpca.Address.RowNumber, bytStlpec = cStlpca.Address.ColumnNumber;
        var stlpce = new Dictionary<int, int>();
        for (int c = 1; c <= maxC; c++)
            if (Cislo(ws.Cell(hr, c)) is { } n && n == Math.Floor(n) && n is >= 1 and <= 13 && !stlpce.ContainsKey((int)n))
                stlpce[(int)n] = c;
        if (!stlpce.ContainsKey(2) || !stlpce.ContainsKey(3))
            throw new InvalidDataException($"Hárok „{ws.Name}“: v riadku „č. stĺpca“ chýbajú stĺpce 2 a 3 (stavy vodomeru).");

        // hlavička: dátumy a ceny
        var datumy = new DateOnly?[5];
        var ceny = new decimal?[3];
        for (int r = 1; r < hr; r++)
            for (int c = 1; c <= maxC; c++)
            {
                var text = Norm(ws.Cell(r, c));
                if (text.Length == 0) continue;
                var md = DatumRegex().Match(text);
                if (md.Success)
                    datumy[int.Parse(md.Groups[1].Value)] ??= PrvyVpravo(ws, r, c, maxC, Datum);
                var mc = CenaRegex().Match(text);
                if (mc.Success)
                    ceny[int.Parse(mc.Groups[1].Value)] ??= PrvyVpravo(ws, r, c, maxC, Cislo);
            }

        var riadky = new List<RiadokTlacivaVody>();
        decimal? D(int r, int n) => stlpce.TryGetValue(n, out var c) ? Cislo(ws.Cell(r, c)) : null;
        string? T(int r, int n) => stlpce.TryGetValue(n, out var c) ? Text(ws.Cell(r, c)) : null;
        for (int r = hr + 1; r <= maxR; r++)
        {
            var byt = Text(ws.Cell(r, bytStlpec));
            if (byt is null) continue;
            var riadok = new RiadokTlacivaVody(byt, T(r, 1), D(r, 2), D(r, 3), T(r, 6), D(r, 7), D(r, 8));
            if (riadok is { Vodomer1: null, Stav2: null, Stav3: null, Vodomer2: null, Stav7: null, Stav8: null }) continue;
            riadky.Add(riadok);
        }
        return new TlacivoVody(datumy[1], datumy[2], datumy[3], datumy[4], ceny[1], ceny[2], riadky);
    }

    static T? PrvyVpravo<T>(IXLWorksheet ws, int r, int c, int maxC, Func<IXLCell, T?> citaj) where T : struct
    {
        for (int i = c + 1; i <= maxC; i++)
        {
            var cell = ws.Cell(r, i);
            if (cell.IsEmpty()) continue;
            if (citaj(cell) is { } v) return v;
        }
        return null;
    }

    [GeneratedRegex(@"^([1-4])\s*\.?\s*datum odpoctu")]
    private static partial Regex DatumRegex();

    [GeneratedRegex(@"^([12])\s*\.?\s*cena vodne")]
    private static partial Regex CenaRegex();

    static string Norm(IXLCell cell)
    {
        if (cell.IsEmpty() || cell.DataType != XLDataType.Text) return "";
        var s = cell.GetString().Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in s)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Regex.Replace(sb.ToString(), @"\s+", " ");
    }

    static string? Text(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var s = cell.DataType == XLDataType.Number ? cell.GetDouble().ToString(CultureInfo.InvariantCulture) : cell.GetFormattedString().Trim();
        return s.Length == 0 ? null : s;
    }

    static decimal? Cislo(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        try
        {
            if (cell.DataType == XLDataType.Number) return (decimal)cell.GetDouble();
            var s = cell.GetFormattedString().Replace(" ", "").Replace(" ", "").Replace("€", "").Replace(',', '.').Trim();
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
        catch { return null; }
    }

    static DateOnly? Datum(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        try
        {
            if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
            if (cell.DataType == XLDataType.Number)
            {
                var n = cell.GetDouble();
                return n is > 20000 and < 80000 ? DateOnly.FromDateTime(DateTime.FromOADate(n)) : null;
            }
            var s = cell.GetFormattedString().Trim();
            return DateOnly.TryParseExact(s, new[] { "d.M.yyyy", "d. M. yyyy", "d.M.yy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        }
        catch { return null; }
    }

    /// <summary>Zapíše tlačivo s údajmi bytov vo formáte vzoru (so vzorcami), dá sa vyplniť a naimportovať späť.</summary>
    public static void Export(UctovnaJednotka j, IReadOnlyList<Priestor> priestory, VodaRoka? voda, KategoriaNakladov? vodaKat, int rok, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("voda");
        ws.Cell("A1").Value = "Tlačivo o prehľade spotreby vody podľa bytových podružných vodomerov";
        ws.Cell("A1").Style.Font.SetBold().Font.SetFontSize(13);
        ws.Cell("A4").Value = $"{j.Nazov} – rok {rok}";
        void Hodnota(string adresa, DateOnly? d) { if (d is { } x) { ws.Cell(adresa).Value = x.ToDateTime(TimeOnly.MinValue); ws.Cell(adresa).Style.DateFormat.Format = "d.m.yyyy"; } }
        ws.Cell("B2").Value = "1.dátum odpočtu ku dňu"; Hodnota("C2", voda?.Datum1);
        ws.Cell("B3").Value = "2.dátum odpočtu ku dňu"; Hodnota("C3", voda?.Datum2);
        ws.Cell("E2").Value = "3.dátum odpočtu ku dňu"; Hodnota("J2", voda?.Datum3);
        ws.Cell("E3").Value = "4.dátum odpočtu ku dňu"; Hodnota("J3", voda?.Datum4);
        ws.Cell("M2").Value = "1. cena vodné a stočné za 1m3 vody"; ws.Cell("Q2").Value = voda?.Cena1 ?? 0m; ws.Cell("S2").Value = "€";
        ws.Cell("M3").Value = "2. cena vodné a stočné za 1m3 vody"; ws.Cell("Q3").Value = voda?.Cena2 ?? 0m; ws.Cell("S3").Value = "€";

        ws.Cell("A8").Value = "byt.č."; ws.Cell("B8").Value = "meno"; ws.Cell("C8").Value = "vodomer a stavy"; ws.Cell("J8").Value = "vodomer a stavy"; ws.Cell("Q8").Value = "Vyúčtovanie";
        string[] h9 = { "C9", "č. vodomeru", "D9", "stav vodomeru ku dňu", "F9", "stav vodomeru ku dňu", "H9", "spotreba vody v m3", "I9", "cena za vodné a stočné v danom období",
            "J9", "č. vodomeru výmena", "K9", "stav vodomeru ku dňu", "M9", "stav vodomeru ku dňu", "O9", "spotreba vody v m3", "P9", "cena za vodné a stočné v danom období",
            "Q9", "zálohová platba", "R9", "voda za rok celkom", "S9", " - nedoplatok /+ preplatok" };
        for (int i = 0; i < h9.Length; i += 2) ws.Cell(h9[i]).Value = h9[i + 1];
        ws.Cell("D10").FormulaA1 = "=C2"; ws.Cell("F10").FormulaA1 = "=C3"; ws.Cell("K10").FormulaA1 = "=J2"; ws.Cell("M10").FormulaA1 = "=J3";
        ws.Cell("I10").Value = "1. cena za vodné a stočné"; ws.Cell("P10").Value = "2. cena za vodné a stočné";
        foreach (var a in new[] { "D10", "F10", "K10", "M10" }) ws.Cell(a).Style.DateFormat.Format = "d.m.yyyy";
        ws.Cell("A11").Value = "č. stlpca";
        string[] cols = { "C", "D", "F", "H", "I", "J", "K", "M", "O", "P", "Q", "R", "S" };
        for (int i = 0; i < cols.Length; i++) ws.Cell(cols[i] + "11").Value = i + 1;
        ws.Range("A8:S11").Style.Font.SetBold().Alignment.SetWrapText().Fill.SetBackgroundColor(XLColor.FromHtml("#DDE6F0"));

        int r = 11;
        foreach (var p in priestory.OrderBy(p => p.Cislo, CisloComparer.Instance))
        {
            r++;
            var pr = p.Roky.FirstOrDefault(x => x.Rok == rok);
            ws.Cell(r, 1).Value = p.Cislo;
            ws.Cell(r, 2).Value = p.MenaVlastnikov;
            ws.Cell($"C{r}").Value = p.CisloVodomeru ?? "";
            if (pr?.StavVodomeruZaciatok is { } s2) ws.Cell($"D{r}").Value = s2;
            if (pr?.StavVodomeruKoniec is { } s3) ws.Cell($"F{r}").Value = s3;
            ws.Cell($"H{r}").FormulaA1 = $"=F{r}-D{r}";
            ws.Cell($"I{r}").FormulaA1 = $"=H{r}*$Q$2";
            ws.Cell($"J{r}").Value = p.CisloVodomeru2 ?? "";
            if (pr?.StavVodomeru2Zaciatok is { } s7) ws.Cell($"K{r}").Value = s7;
            if (pr?.StavVodomeru2Koniec is { } s8) ws.Cell($"M{r}").Value = s8;
            ws.Cell($"O{r}").FormulaA1 = $"=M{r}-K{r}";
            ws.Cell($"P{r}").FormulaA1 = $"=$Q$3*O{r}";
            ws.Cell($"Q{r}").Value = vodaKat is null ? 0m : p.Predpisy.Where(x => x.Rok == rok && x.KategoriaId == vodaKat.Id).Sum(x => x.RocnaSuma);
            ws.Cell($"R{r}").FormulaA1 = $"=I{r}+P{r}";
            ws.Cell($"S{r}").FormulaA1 = $"=Q{r}-R{r}";
            ws.Range($"I{r}:I{r}").Style.NumberFormat.Format = "#,##0.00";
            foreach (var c in new[] { "I", "P", "Q", "R", "S" }) ws.Cell($"{c}{r}").Style.NumberFormat.Format = "#,##0.00";
        }
        ws.Column(2).Width = 26;
        foreach (var c in cols) ws.Column(c).Width = 12;
        ws.Row(9).Height = 45;
        wb.SaveAs(path);
    }
}
