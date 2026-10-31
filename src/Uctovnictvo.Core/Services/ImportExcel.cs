using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public record VysledokImportu(int Pridane, int Aktualizovane, IReadOnlyList<string> Upozornenia);

/// <summary>
/// Hromadný import bytov, vlastníkov, účtov a predpisov z Excelu. Stĺpce sa hľadajú podľa názvu v prvom riadku
/// (na veľkosti písmen a diakritike nezáleží), takže stačí premenovať hlavičky vo vlastnej tabuľke.
/// Byt sa páruje podľa čísla – existujúce sa aktualizujú, nové sa pridajú.
/// </summary>
public static class ImportExcel
{
    public const string PredponaPredpisu = "Predpis: ";

    static readonly (string Stlpec, string Popis)[] Zakladne =
    {
        ("Číslo", "číslo bytu / priestoru (povinné)"),
        ("Typ", "byt alebo nebytový"),
        ("Vchod", ""),
        ("Poschodie", ""),
        ("Plocha m2", "podlahová plocha"),
        ("Balkón m2", ""),
        ("Osoby", "počet osôb"),
        ("Podiel", "čitateľ spoluvlastníckeho podielu"),
        ("Z celku", "menovateľ podielu"),
        ("VS", "variabilný symbol"),
        ("Vodomer", "číslo vodomeru"),
        ("Vlastník", "viac vlastníkov oddeľte bodkočiarkou"),
        ("Ulica", "adresa vlastníka"),
        ("Obec", ""),
        ("PSČ", ""),
        ("E-mail", ""),
        ("Telefón", ""),
        ("IBAN", "účty, z ktorých platí; viac oddeľte bodkočiarkou"),
        ("Stav vodomeru", "počiatočný stav vodomeru v roku importu (m³)"),
        ("Poistné", "ročné poistné – predpis"),
        ("Zostatok min. roka", "+ preplatok / − nedoplatok z minulého roka"),
    };

    /// <summary>Prázdna šablóna (alebo s existujúcimi údajmi) na vyplnenie v Exceli.</summary>
    public static void Sablona(UctovnaJednotka j, IReadOnlyList<Priestor> priestory, IReadOnlyList<KategoriaNakladov> kategorie, int rok, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Byty");
        var hlavicka = Zakladne.Select(z => z.Stlpec).Concat(kategorie.Select(k => PredponaPredpisu + k.Nazov)).ToList();
        for (int c = 0; c < hlavicka.Count; c++) ws.Cell(1, c + 1).Value = hlavicka[c];
        ws.Row(1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DDE6F0"));

        int r = 1;
        foreach (var p in priestory.OrderBy(p => p.Cislo, CisloComparer.Instance))
        {
            r++;
            var v = p.SucasniVlastnici.ToList();
            var pr = p.Roky.FirstOrDefault(x => x.Rok == rok);
            object?[] hodnoty =
            {
                p.Cislo, p.Typ == TypPriestoru.Byt ? "byt" : "nebytový", p.Vchod, p.Poschodie, p.Plocha, p.PlochaBalkona, p.PocetOsob,
                p.PodielCitatel, p.PodielMenovatel, p.VariabilnySymbol, p.CisloVodomeru,
                string.Join("; ", v.Select(x => x.Meno)), v.FirstOrDefault()?.Ulica, v.FirstOrDefault()?.Obec, v.FirstOrDefault()?.Psc,
                string.Join("; ", v.Select(x => x.Email).Where(e => !string.IsNullOrWhiteSpace(e))),
                v.FirstOrDefault()?.Telefon,
                string.Join("; ", v.SelectMany(x => x.Ucty).Select(u => u.Iban)),
                pr?.StavVodomeruZaciatok, pr?.PoistnePredpis, pr?.ZostatokMinulyRok,
            };
            for (int c = 0; c < hodnoty.Length; c++) ws.Cell(r, c + 1).Value = XLCellValue.FromObject(hodnoty[c]);
            for (int k = 0; k < kategorie.Count; k++)
                ws.Cell(r, hodnoty.Length + k + 1).Value = p.Predpisy.Where(x => x.Rok == rok && x.KategoriaId == kategorie[k].Id).Sum(x => x.RocnaSuma);
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Max(r, 2));

        var info = wb.AddWorksheet("Návod");
        info.Cell(1, 1).Value = $"{j.Nazov} – import údajov, rok predpisov {rok}";
        info.Cell(1, 1).Style.Font.SetBold();
        info.Cell(2, 1).Value = "Vyplňte hárok „Byty“, jeden riadok = jeden byt. Byty sa párujú podľa čísla: existujúce sa aktualizujú, nové sa pridajú. Prázdne bunky sa ignorujú.";
        info.Cell(3, 1).Value = $"Stĺpce „{PredponaPredpisu}…“ sú ročné zálohy pre jednotlivé položky nákladov; názov za dvojbodkou musí zodpovedať položke v programe.";
        int ir = 5;
        foreach (var (s, popis) in Zakladne)
        {
            info.Cell(ir, 1).Value = s;
            info.Cell(ir++, 2).Value = popis;
        }
        info.Column(1).Width = 24;
        info.Column(2).Width = 80;
        wb.SaveAs(path);
    }

    public static VysledokImportu Importuj(AppDbContext db, UctovnaJednotka j, int rok, string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.FirstOrDefault(w => Kluc(w.Name) == "byty") ?? wb.Worksheet(1);
        var hlavicka = ws.Row(1).CellsUsed().GroupBy(c => Kluc(c.GetString()))
            .ToDictionary(g => g.Key, g => g.First().Address.ColumnNumber);
        int? Stlpec(params string[] mena) => mena.Select(Kluc).Where(hlavicka.ContainsKey).Select(m => (int?)hlavicka[m]).FirstOrDefault();

        var upozornenia = new List<string>();
        var cisloStlpec = Stlpec("Číslo", "Číslo bytu", "Byt", "č. bytu");
        if (cisloStlpec is null)
            return new VysledokImportu(0, 0, new[] { "V prvom riadku chýba stĺpec „Číslo“ (číslo bytu)." });

        var kategorie = db.Kategorie.Where(k => k.JednotkaId == j.Id).ToList();
        var predpisStlpce = new Dictionary<int, KategoriaNakladov>();
        foreach (var (kluc, stlpec) in hlavicka)
        {
            if (!kluc.StartsWith(Kluc(PredponaPredpisu).TrimEnd())) continue;
            var nazov = kluc[Kluc(PredponaPredpisu).TrimEnd().Length..].Trim();
            var k = kategorie.FirstOrDefault(x => Kluc(x.Nazov) == nazov);
            if (k is null) upozornenia.Add($"Stĺpec „{ws.Cell(1, stlpec).GetString()}“: položka nákladov s týmto názvom v programe nie je, stĺpec sa vynechal.");
            else predpisStlpce[stlpec] = k;
        }

        var priestory = db.Priestory.Where(p => p.JednotkaId == j.Id)
            .Include(p => p.Vlastnici).ThenInclude(v => v.Ucty).Include(p => p.Roky).Include(p => p.Predpisy)
            .AsSplitQuery().ToList();
        int pridane = 0, aktualizovane = 0;
        var posledny = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int r = 2; r <= posledny; r++)
        {
            string? Text(params string[] mena) => Stlpec(mena) is { } c && !ws.Cell(r, c).IsEmpty() ? ws.Cell(r, c).GetFormattedString().Trim() : null;
            decimal? Cislo(params string[] mena)
            {
                if (Stlpec(mena) is not { } c || ws.Cell(r, c).IsEmpty()) return null;
                var bunka = ws.Cell(r, c);
                if (bunka.DataType == XLDataType.Number) return (decimal)bunka.GetDouble();
                var s = bunka.GetString().Replace(" ", "").Replace(" ", "").Replace("€", "").Replace(',', '.');
                if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
                upozornenia.Add($"Riadok {r}: „{bunka.GetString()}“ v stĺpci {ws.Cell(1, c).GetString()} nie je číslo.");
                return null;
            }

            var cislo = ws.Cell(r, cisloStlpec.Value).GetFormattedString().Trim();
            if (cislo.Length == 0) continue;

            var p = priestory.FirstOrDefault(x => x.Cislo.Trim() == cislo);
            if (p is null)
            {
                p = new Priestor { JednotkaId = j.Id, Cislo = cislo, VariabilnySymbol = cislo };
                db.Priestory.Add(p);
                priestory.Add(p);
                pridane++;
            }
            else aktualizovane++;

            if (Text("Typ") is { } typ) p.Typ = Kluc(typ).StartsWith("neb") ? TypPriestoru.NebytovyPriestor : TypPriestoru.Byt;
            if (Text("Vchod") is { } vchod) p.Vchod = vchod;
            if (Text("Poschodie") is { } posch) p.Poschodie = posch;
            if (Cislo("Plocha m2", "Plocha", "Plocha bytu", "m2") is { } plocha) p.Plocha = plocha;
            if (Cislo("Balkón m2", "Balkón", "Plocha balkóna") is { } balkon) p.PlochaBalkona = balkon;
            if (Cislo("Osoby", "Počet osôb") is { } osoby) p.PocetOsob = (int)osoby;
            if (Text("Podiel") is { } podielText && podielText.Contains('/'))
            {
                var casti = podielText.Split('/');
                if (int.TryParse(casti[0].Trim(), out var a) && int.TryParse(casti[1].Trim(), out var b)) { p.PodielCitatel = a; p.PodielMenovatel = b; }
            }
            else if (Cislo("Podiel") is { } citatel) p.PodielCitatel = (int)citatel;
            if (Cislo("Z celku", "Menovateľ") is { } menovatel) p.PodielMenovatel = (int)menovatel;
            if (Text("VS", "Variabilný symbol") is { } vs) p.VariabilnySymbol = vs;
            if (Text("Vodomer", "Číslo vodomeru") is { } vodomer) p.CisloVodomeru = vodomer;

            if (Text("Vlastník", "Vlastníci", "Meno") is { } mena)
            {
                var zoznam = mena.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var emaily = (Text("E-mail", "Email") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var ibany = (Text("IBAN", "Účet") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int i = 0; i < zoznam.Length; i++)
                {
                    var v = p.Vlastnici.FirstOrDefault(x => Kluc(x.Meno) == Kluc(zoznam[i]));
                    if (v is null)
                    {
                        v = new Vlastnik { Meno = zoznam[i], Hlavny = i == 0 && !p.Vlastnici.Any(x => x.Hlavny) };
                        p.Vlastnici.Add(v);
                    }
                    if (Text("Ulica") is { } ulica) v.Ulica = ulica;
                    if (Text("Obec") is { } obec) v.Obec = obec;
                    if (Text("PSČ", "PSC") is { } psc) v.Psc = psc;
                    if (i < emaily.Length) v.Email = emaily[i];
                    else if (emaily.Length == 1 && i == 0) v.Email = emaily[0];
                    if (i == 0 && Text("Telefón", "Telefon") is { } tel) v.Telefon = tel;
                    if (i == 0)
                        foreach (var iban in ibany)
                        {
                            if (!Iban.JePlatny(iban)) upozornenia.Add($"Byt {cislo}: IBAN „{iban}“ nie je platný (uložil sa, skontrolujte ho).");
                            if (!p.Vlastnici.SelectMany(x => x.Ucty).Any(u => Iban.Normalizuj(u.Iban) == Iban.Normalizuj(iban)))
                                v.Ucty.Add(new BankovyUcet { Iban = Iban.Normalizuj(iban) });
                        }
                }
            }

            var stav = Cislo("Stav vodomeru", "Stav vodomeru na začiatku");
            var poistne = Cislo("Poistné", "Poistenie");
            var zostatok = Cislo("Zostatok min. roka", "Zostatok", "Nedoplatok");
            if (stav is not null || poistne is not null || zostatok is not null)
            {
                var pr = p.Roky.FirstOrDefault(x => x.Rok == rok);
                if (pr is null) { pr = new PriestorRok { Rok = rok }; p.Roky.Add(pr); }
                if (stav is not null) pr.StavVodomeruZaciatok = stav;
                if (poistne is not null) pr.PoistnePredpis = poistne.Value;
                if (zostatok is not null) pr.ZostatokMinulyRok = zostatok.Value;
            }

            foreach (var (stlpec, k) in predpisStlpce)
            {
                if (ws.Cell(r, stlpec).IsEmpty()) continue;
                var bunka = ws.Cell(r, stlpec);
                decimal suma;
                if (bunka.DataType == XLDataType.Number) suma = (decimal)bunka.GetDouble();
                else if (!decimal.TryParse(bunka.GetString().Replace(',', '.').Replace(" ", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out suma)) continue;
                suma = Math.Round(suma, 2, MidpointRounding.AwayFromZero);
                var x = p.Predpisy.FirstOrDefault(y => y.Rok == rok && y.KategoriaId == k.Id);
                if (x is null) p.Predpisy.Add(new PredpisPolozky { Rok = rok, KategoriaId = k.Id, RocnaSuma = suma });
                else x.RocnaSuma = suma;
            }
        }
        db.SaveChanges();
        return new VysledokImportu(pridane, aktualizovane, upozornenia);
    }

    /// <summary>Názov stĺpca bez diakritiky, medzier na okrajoch a veľkých písmen.</summary>
    static string Kluc(string s)
    {
        var norm = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Replace("²", "2").Replace("  ", " ");
    }
}
