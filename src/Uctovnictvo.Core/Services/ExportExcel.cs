using ClosedXML.Excel;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public static class ExportExcel
{
    const string FormatEur = "#,##0.00 \"€\"";
    static readonly string[] Mesiace = { "Jan", "Feb", "Mar", "Apr", "Máj", "Jún", "Júl", "Aug", "Sep", "Okt", "Nov", "Dec" };

    /// <summary>Súhrn ročného vyúčtovania: jeden riadok na priestor, skutočné náklady po kategóriách a výsledok.</summary>
    public static void Vyuctovanie(UctovnaJednotka jednotka, VysledokVyuctovania v, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet($"Vyúčtovanie {v.Rok}");
        ws.Cell(1, 1).Value = $"{jednotka.Nazov} – vyúčtovanie za rok {v.Rok}";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        var kategorie = v.Byty.FirstOrDefault()?.Polozky.Select(p => p.Kategoria).ToList() ?? new();
        var hlavicka = new List<string> { "Priestor", "Vlastník", "Plocha m²", "Voda m³" };
        hlavicka.AddRange(kategorie.Select(k => k.Nazov));
        hlavicka.AddRange(new[] { "Skutočné spolu", "Predpis záloh", "Zaplatené zálohy", "Nedoplatok na zálohách",
            "Výsledok za náklady", "Minulý rok", "Poistné", "Výsledok (+ preplatok / − doplatiť)" });
        int r = 3;
        for (int c = 0; c < hlavicka.Count; c++) ws.Cell(r, c + 1).Value = hlavicka[c];
        ws.Row(r).Style.Font.SetBold();
        ws.Row(r).Style.Alignment.WrapText = true;

        foreach (var b in v.Byty)
        {
            r++;
            int c = 1;
            ws.Cell(r, c++).Value = b.Priestor.Cislo;
            ws.Cell(r, c++).Value = b.Vlastnik + (b.JeCastRoka ? $" ({b.ObdobieOd:d.M.} – {b.ObdobieDo:d.M.})" : "");
            ws.Cell(r, c++).Value = b.Priestor.Plocha;
            ws.Cell(r, c++).Value = b.Voda?.Spotreba ?? 0m;
            foreach (var p in b.Polozky) ws.Cell(r, c++).Value = p.Skutocne;
            ws.Cell(r, c++).Value = b.SkutocneSpolu;
            ws.Cell(r, c++).Value = b.PredpisZaloh;
            ws.Cell(r, c++).Value = b.ZaplateneZalohy;
            ws.Cell(r, c++).Value = b.NedoplatokNaZalohach;
            ws.Cell(r, c++).Value = b.VysledokNakladov;
            ws.Cell(r, c++).Value = b.RozdielMinulyRok;
            ws.Cell(r, c++).Value = b.RozdielPoistne;
            ws.Cell(r, c++).Value = b.Vysledok;
        }

        r++;
        ws.Cell(r, 1).Value = "Spolu";
        for (int c = 3; c <= hlavicka.Count; c++)
        {
            var col = ws.Column(c).ColumnLetter();
            ws.Cell(r, c).FormulaA1 = $"SUM({col}4:{col}{r - 1})";
        }
        ws.Row(r).Style.Font.SetBold();
        ws.Range(4, 5, r, hlavicka.Count).Style.NumberFormat.Format = FormatEur;
        ws.Range(4, 3, r, 3).Style.NumberFormat.Format = "0.00";
        ws.Range(4, 4, r, 4).Style.NumberFormat.Format = "0.000";

        var nakladyWs = wb.AddWorksheet("Náklady domu");
        string[] h = { "Kategória", "Náklad domu", "Kľúč rozpočtu", "Len priestory", "Súčet podielov", "Rozdiel zaokrúhlenia" };
        for (int c = 0; c < h.Length; c++) nakladyWs.Cell(1, c + 1).Value = h[c];
        nakladyWs.Row(1).Style.Font.SetBold();
        int nr = 1;
        foreach (var k in kategorie)
        {
            nr++;
            var naklad = Services.Vyuctovanie.NakladDomu(k, v.Rok);
            var podiely = v.Byty.Sum(b => b.Polozky.First(p => p.Kategoria.Id == k.Id).Skutocne);
            nakladyWs.Cell(nr, 1).Value = k.Nazov;
            nakladyWs.Cell(nr, 2).Value = k.JeFondOprav ? podiely : naklad;
            nakladyWs.Cell(nr, 3).Value = k.JeFondOprav ? "fond opráv (nevyúčtováva sa)" : Services.Vyuctovanie.NazovKluca(k.Kluc);
            nakladyWs.Cell(nr, 4).Value = k.LenPriestory ?? "";
            nakladyWs.Cell(nr, 5).Value = podiely;
            nakladyWs.Cell(nr, 6).Value = k.JeFondOprav ? 0m : podiely - naklad;
        }
        nakladyWs.Range(2, 2, Math.Max(2, nr), 2).Style.NumberFormat.Format = FormatEur;
        nakladyWs.Range(2, 5, Math.Max(2, nr), 6).Style.NumberFormat.Format = FormatEur;

        if (v.Upozornenia.Count > 0)
        {
            var u = wb.AddWorksheet("Upozornenia");
            for (int i = 0; i < v.Upozornenia.Count; i++) u.Cell(i + 1, 1).Value = v.Upozornenia[i];
            u.Columns().AdjustToContents();
        }

        ws.SheetView.FreezeRows(3);
        ws.Columns().AdjustToContents();
        foreach (var c in Enumerable.Range(5, hlavicka.Count - 4)) ws.Column(c).Width = 13;
        nakladyWs.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    /// <summary>Prehľad úhrad za rok: mesiac po mesiaci, spolu, predpis a rozdiel; druhý hárok so všetkými platbami.</summary>
    public static void UhradyZaRok(UctovnaJednotka jednotka, IReadOnlyList<Priestor> priestory, int rok, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet($"Úhrady {rok}");
        ws.Cell(1, 1).Value = $"{jednotka.Nazov} – úhrady za rok {rok}";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        var hlavicka = new List<string> { "Priestor", "Vlastník", "Účty (IBAN)", "VS" };
        hlavicka.AddRange(Mesiace);
        hlavicka.AddRange(new[] { "Zaplatené spolu", "Predpis na rok (zálohy + poistné)", "Rozdiel (+ preplatok / − dlh)" });
        int r = 3;
        for (int c = 0; c < hlavicka.Count; c++) ws.Cell(r, c + 1).Value = hlavicka[c];
        ws.Row(r).Style.Font.SetBold();

        foreach (var p in priestory.OrderBy(p => p.Cislo, CisloComparer.Instance))
        {
            r++;
            var uhrady = p.Uhrady.Where(u => u.Datum.Year == rok).ToList();
            var predpisSpolu = p.Predpisy.Where(x => x.Rok == rok).Sum(x => x.RocnaSuma)
                + (p.Roky.FirstOrDefault(x => x.Rok == rok)?.PoistnePredpis ?? 0m);
            ws.Cell(r, 1).Value = p.Cislo;
            ws.Cell(r, 2).Value = string.Join(", ", p.Vlastnici.Select(v => v.Meno));
            ws.Cell(r, 3).Value = string.Join(", ", p.Vlastnici.SelectMany(v => v.Ucty).Select(u => Iban.Formatuj(u.Iban)));
            ws.Cell(r, 4).Value = p.VariabilnySymbol ?? "";
            for (int m = 1; m <= 12; m++)
                ws.Cell(r, 4 + m).Value = uhrady.Where(u => u.Datum.Month == m).Sum(u => u.Suma);
            var spolu = uhrady.Sum(u => u.Suma);
            ws.Cell(r, 17).Value = spolu;
            ws.Cell(r, 18).Value = predpisSpolu;
            ws.Cell(r, 19).Value = spolu - predpisSpolu;
        }
        r++;
        ws.Cell(r, 1).Value = "Spolu";
        for (int c = 5; c <= 19; c++)
        {
            var col = ws.Column(c).ColumnLetter();
            ws.Cell(r, c).FormulaA1 = $"SUM({col}4:{col}{r - 1})";
        }
        ws.Row(r).Style.Font.SetBold();
        ws.Range(4, 5, r, 19).Style.NumberFormat.Format = FormatEur;
        ws.SheetView.FreezeRows(3);
        ws.Columns().AdjustToContents();

        var detail = wb.AddWorksheet("Všetky platby");
        string[] h2 = { "Dátum", "Priestor", "Suma", "Účel", "IBAN platiteľa", "VS", "Poznámka" };
        for (int c = 0; c < h2.Length; c++) detail.Cell(1, c + 1).Value = h2[c];
        detail.Row(1).Style.Font.SetBold();
        int dr = 1;
        foreach (var (p, u) in priestory.SelectMany(p => p.Uhrady.Where(u => u.Datum.Year == rok).Select(u => (p, u))).OrderBy(x => x.u.Datum))
        {
            dr++;
            detail.Cell(dr, 1).Value = u.Datum.ToDateTime(TimeOnly.MinValue);
            detail.Cell(dr, 2).Value = p.Cislo;
            detail.Cell(dr, 3).Value = u.Suma;
            detail.Cell(dr, 4).Value = NazovUcelu(u.Ucel);
            detail.Cell(dr, 5).Value = Iban.Formatuj(u.IbanPlatitela);
            detail.Cell(dr, 6).Value = u.VariabilnySymbol ?? "";
            detail.Cell(dr, 7).Value = u.Poznamka ?? "";
        }
        detail.Column(1).Style.NumberFormat.Format = "d.m.yyyy";
        detail.Column(3).Style.NumberFormat.Format = FormatEur;
        detail.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    /// <summary>Jednoduchý hárok: hlavička + riadky; stĺpce s typom decimal dostanú formát eur, DateOnly formát dátumu.</summary>
    static void Harok(XLWorkbook wb, string nazov, string titulok, string[] hlavicka, IEnumerable<object?[]> riadky)
    {
        var ws = wb.AddWorksheet(nazov);
        ws.Cell(1, 1).Value = titulok;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        for (int c = 0; c < hlavicka.Length; c++) ws.Cell(3, c + 1).Value = hlavicka[c];
        ws.Row(3).Style.Font.SetBold();
        int r = 3;
        foreach (var riadok in riadky)
        {
            r++;
            for (int c = 0; c < riadok.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                switch (riadok[c])
                {
                    case null: break;
                    case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = FormatEur; break;
                    case DateOnly dt: cell.Value = dt.ToDateTime(TimeOnly.MinValue); cell.Style.NumberFormat.Format = "d.m.yyyy"; break;
                    case int n: cell.Value = n; break;
                    default: cell.Value = riadok[c]!.ToString(); break;
                }
            }
        }
        ws.SheetView.FreezeRows(3);
        ws.Columns().AdjustToContents();
    }

    public static void KnihaFaktur(UctovnaJednotka j, IEnumerable<Faktura> faktury, int rok, string path)
    {
        using var wb = new XLWorkbook();
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        string[] h = { "Číslo", "Typ", "Číslo dodávateľa", "Partner", "IČO", "VS", "Vystavená", "Dodanie", "Splatnosť", "Suma", "Uhradené", "Zostáva", "Stav" };
        foreach (var druh in new[] { DruhFaktury.Vydana, DruhFaktury.Prijata })
        {
            var zoznam = faktury.Where(f => f.Druh == druh && f.DatumVystavenia.Year == rok).OrderBy(f => f.DatumVystavenia).ThenBy(f => f.Cislo);
            Harok(wb, druh == DruhFaktury.Vydana ? "Vydané" : "Prijaté", $"{j.Nazov} – kniha {(druh == DruhFaktury.Vydana ? "vydaných" : "prijatých")} faktúr {rok}", h,
                zoznam.Select(f => new object?[] { f.Cislo, Faktury.NazovTypu(f.Typ), f.CisloDodavatela, f.PartnerNazov, f.PartnerIco, f.VariabilnySymbol,
                    f.DatumVystavenia, f.DatumDodania, f.DatumSplatnosti, f.Suma, f.Uhradene, f.Zostava, Faktury.Stav(f, dnes) }));
        }
        wb.SaveAs(path);
    }

    public static void BankovePohyby(UctovnaJednotka j, IEnumerable<BankovyPohyb> pohyby, int rok, string path)
    {
        using var wb = new XLWorkbook();
        Harok(wb, $"Banka {rok}", $"{j.Nazov} – bankové pohyby {rok}",
            new[] { "Dátum", "Suma", "Protiúčet", "Názov", "VS", "Popis", "Uhradené faktúry" },
            pohyby.Where(p => p.Datum.Year == rok).OrderBy(p => p.Datum).Select(p => new object?[]
            {
                p.Datum, p.Suma, Iban.Formatuj(p.ProtiIban), p.ProtiNazov, p.VariabilnySymbol, p.Popis,
                string.Join(", ", p.Uhrady.Select(u => u.Faktura?.Cislo)),
            }));
        wb.SaveAs(path);
    }

    public static void PokladnicnaKniha(UctovnaJednotka j, int rok, decimal zaciatok, IReadOnlyList<RiadokPokladne> riadky, string path)
    {
        using var wb = new XLWorkbook();
        var zoznam = new List<object?[]> { new object?[] { null, null, $"Zostatok k 1. 1. {rok}", null, null, null, zaciatok } };
        zoznam.AddRange(riadky.Select(r => new object?[] { r.Doklad.Datum, r.Doklad.Cislo, r.Doklad.Ucel, r.Doklad.Osoba,
            r.Prijem == 0 ? null : r.Prijem, r.Vydavok == 0 ? null : r.Vydavok, r.Zostatok }));
        Harok(wb, $"Pokladňa {rok}", $"{j.Nazov} – pokladničná kniha {rok}",
            new[] { "Dátum", "Doklad", "Účel", "Osoba", "Príjem", "Výdavok", "Zostatok" }, zoznam);
        wb.SaveAs(path);
    }

    public static string NazovUcelu(UcelUhrady u) => u switch
    {
        UcelUhrady.Zalohy => "zálohy",
        UcelUhrady.Poistne => "poistné",
        UcelUhrady.MinulyRok => "vyúčtovanie minulého roka",
        _ => u.ToString(),
    };
}
