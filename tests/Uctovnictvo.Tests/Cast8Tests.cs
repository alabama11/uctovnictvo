using ClosedXML.Excel;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class OpravySvbTests
{
    [Fact]
    public void Zmena_vlastnika_v_polovici_augusta_sa_deli_podla_mesiacov_uzivania()
    {
        var (byty, kat) = Vzor.Dom();
        var b3 = byty[2];
        b3.Vlastnici[0].VlastnikDo = new DateOnly(2025, 8, 14);
        b3.Vlastnici.Add(new Vlastnik { Meno = "Nový Vlastník", VlastnikOd = new DateOnly(2025, 8, 15) });

        var casti = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat).Byty.Where(b => b.Priestor == b3).ToList();

        Assert.Equal(2, casti.Count);
        Assert.Equal(7, casti[0].MesiaceUzivania);   // január – júl
        Assert.Equal(5, casti[1].MesiaceUzivania);   // august (15. deň už patrí novému) – december
        var sprava = kat[0];
        Assert.Equal(Math.Round(243.00m * 7 / 12, 2, MidpointRounding.AwayFromZero), casti[0].Polozky.Single(x => x.Kategoria == sprava).Zaloha);
        Assert.Equal(Math.Round(243.00m * 5 / 12, 2, MidpointRounding.AwayFromZero), casti[1].Polozky.Single(x => x.Kategoria == sprava).Zaloha);
        Assert.Equal(0m, casti[0].Mesiace[7].Predpis);
        Assert.NotEqual(0m, casti[1].Mesiace[7].Predpis);
        Assert.InRange(casti[0].Mesiace.Sum(m => m.Predpis) - casti[0].PredpisZaloh, -0.10m, 0.10m);
    }

    [Fact]
    public void Kontaktna_osoba_bez_vlastnictva_nedeli_vyuctovanie()
    {
        var (byty, kat) = Vzor.Dom();
        var b3 = byty[2];
        b3.Vlastnici.Add(new Vlastnik { Meno = "Syn – kontakt", JeVlastnik = false, Hlavny = false, VlastnikOd = new DateOnly(2025, 5, 1) });
        var casti = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat).Byty.Where(b => b.Priestor == b3).ToList();
        Assert.Single(casti);
        Assert.Equal("PaedDr. Patrik Voštinár PhD.", casti[0].Vlastnik);
        Assert.Equal(2, casti[0].Adresati.Count);
    }

    [Fact]
    public void Voda_podla_tlaciva_dve_ceny()
    {
        var (byty, kat) = Vzor.Dom();
        var pr = byty[2].Roky.Single(r => r.Rok == Vzor.Rok);
        pr.StavVodomeruZaciatok = 100; pr.StavVodomeruKoniec = 120;    // stĺpce 2, 3
        pr.StavVodomeru2Zaciatok = 120; pr.StavVodomeru2Koniec = 131;  // stĺpce 7, 8
        var voda = new VodaRoka { Rok = Vzor.Rok, Datum1 = new(2025, 1, 1), Datum2 = new(2025, 8, 15), Datum3 = new(2025, 8, 15), Datum4 = new(2025, 12, 31), Cena1 = 3m, Cena2 = 3.2m };

        var v = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat, voda);
        var b = v.Byty.First(x => x.Priestor == byty[2]);

        Assert.True(b.Voda!.PodlaTlaciva);
        Assert.Equal(20m, b.Voda.Spotreba1);       // 4 = 3 − 2
        Assert.Equal(60m, b.Voda.Naklad1);         // 5 = 4 × 1. cena
        Assert.Equal(11m, b.Voda.Spotreba2);       // 9 = 8 − 7
        Assert.Equal(35.2m, b.Voda.Naklad2);       // 10 = 9 × 2. cena
        Assert.Equal(39.60m, b.Voda.Zaloha);       // 11 = predpis vodné a stočné
        Assert.Equal(95.2m, b.Voda.Naklad);        // 12 = 5 + 10
        Assert.Equal(-55.6m, b.Voda.Rozdiel);      // 13 = 11 − 12
        Assert.Equal(31m, b.Voda.Spotreba);
        var polozka = b.Polozky.Single(x => x.Kategoria.Kluc == KlucRozpoctu.SpotrebaVody);
        Assert.Equal(95.2m, polozka.Skutocne);

        var dir = Directory.CreateTempSubdirectory("voda").FullName;
        var j = new UctovnaJednotka { Nazov = "SVB", Iban = "SK5911000000002628782959" };
        PdfVyuctovanie.Uloz(j, new[] { b }, new NastaveniaVyuctovania(new DateOnly(2026, 1, 20), "Donovaly"), Path.Combine(dir, "v.pdf"));
        Assert.True(new FileInfo(Path.Combine(dir, "v.pdf")).Length > 1000);
        var out_ = Environment.GetEnvironmentVariable("UCT_PDF_DIR");
        if (out_ is not null) File.Copy(Path.Combine(dir, "v.pdf"), Path.Combine(out_, "vyuctovanie-voda.pdf"), true);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Import_tlaciva_podla_vzoru()
    {
        var dir = Directory.CreateTempSubdirectory("tl").FullName;
        var subor = Path.Combine(dir, "t.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("voda1");
            ws.Cell("A1").Value = "Tlačivo o prehľade spotreby vody podľa bytových podružných vodomerov";
            ws.Cell("B2").Value = "1.dátum odpočtu ku dňu"; ws.Cell("C2").Value = new DateTime(2026, 1, 1);
            ws.Cell("B3").Value = "2.dátum odpočtu ku dňu"; ws.Cell("C3").Value = new DateTime(2026, 8, 15);
            ws.Cell("E2").Value = "3.dátum odpočtu ku dňu"; ws.Cell("J2").Value = new DateTime(2026, 8, 15);
            ws.Cell("E3").Value = "4.dátum odpočtu ku dňu"; ws.Cell("J3").Value = new DateTime(2026, 12, 31);
            ws.Cell("M2").Value = "1. cena vodné a stočné za 1m3 vody"; ws.Cell("Q2").Value = 3;
            ws.Cell("M3").Value = "2. cena vodné a stočné za 1m3 vody"; ws.Cell("Q3").Value = 3.2;
            ws.Cell("D10").FormulaA1 = "=C2"; ws.Cell("I10").Value = "1. cena za vodné a stočné";
            ws.Cell("A11").Value = "č. stlpca";
            string[] cols = { "C", "D", "F", "H", "I", "J", "K", "M", "O", "P", "Q", "R", "S" };
            for (int i = 0; i < cols.Length; i++) ws.Cell(cols[i] + "11").Value = i + 1;
            ws.Cell("A12").Value = 1; ws.Cell("D12").Value = 100; ws.Cell("F12").Value = 120; ws.Cell("K12").Value = 120; ws.Cell("M12").Value = 131;
            ws.Cell("H12").FormulaA1 = "=F12-D12";
            ws.Cell("A13").Value = 2;
            ws.Cell("A14").Value = 3; ws.Cell("C14").Value = "V-77"; ws.Cell("D14").Value = 5.5; ws.Cell("F14").Value = "7,25";
            wb.SaveAs(subor);
        }
        var t = OdpoctyVody.Nacitaj(subor);
        Assert.Equal(new DateOnly(2026, 1, 1), t.Datum1);
        Assert.Equal(new DateOnly(2026, 8, 15), t.Datum2);
        Assert.Equal(new DateOnly(2026, 8, 15), t.Datum3);
        Assert.Equal(new DateOnly(2026, 12, 31), t.Datum4);
        Assert.Equal(3m, t.Cena1);
        Assert.Equal(3.2m, t.Cena2);
        Assert.Equal(2, t.Riadky.Count);                 // byt 2 nemá žiadne údaje
        Assert.Equal(new RiadokTlacivaVody("1", null, 100, 120, null, 120, 131), t.Riadky[0]);
        Assert.Equal(new RiadokTlacivaVody("3", "V-77", 5.5m, 7.25m, null, null, null), t.Riadky[1]);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Export_tlaciva_sa_da_naimportovat_spat()
    {
        var (byty, kat) = Vzor.Dom();
        var pr = byty[2].Roky.Single(r => r.Rok == Vzor.Rok);
        pr.StavVodomeru2Zaciatok = 182.285m; pr.StavVodomeru2Koniec = 190m;
        byty[2].CisloVodomeru2 = "NOVY1";
        var voda = new VodaRoka { Datum1 = new(2025, 1, 1), Datum4 = new(2025, 12, 31), Cena1 = 3.84m, Cena2 = 4m };
        var dir = Directory.CreateTempSubdirectory("tl").FullName;
        var subor = Path.Combine(dir, "t.xlsx");
        OdpoctyVody.Export(new UctovnaJednotka { Nazov = "SVB" }, byty, voda, kat.First(k => k.Kluc == KlucRozpoctu.SpotrebaVody), Vzor.Rok, subor);
        var t = OdpoctyVody.Nacitaj(subor);
        Assert.Equal(60, t.Riadky.Count);
        Assert.Equal(3.84m, t.Cena1);
        Assert.Equal(new DateOnly(2025, 12, 31), t.Datum4);
        var r3 = t.Riadky.Single(r => r.Byt == "3");
        Assert.Equal("69241621", r3.Vodomer1);
        Assert.Equal("NOVY1", r3.Vodomer2);
        Assert.Equal(143m, r3.Stav2);
        Assert.Equal(182.285m, r3.Stav3);
        Assert.Equal(190m, r3.Stav8);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Prezencna_listina_so_spoluvlastnikmi()
    {
        var (byty, _) = Vzor.Dom();
        byty[2].Vlastnici[0].SpoluvlastnickyPodiel = "1/2";
        byty[2].Vlastnici.Add(new Vlastnik { Meno = "Spoluvlastníčka", SpoluvlastnickyPodiel = "1/2", Hlavny = false });
        byty[2].Vlastnici.Add(new Vlastnik { Meno = "Nájomca", JeVlastnik = false, Hlavny = false });
        var dir = Directory.CreateTempSubdirectory("pl").FullName;
        var f = Path.Combine(dir, "p.pdf");
        PdfDokumenty.PrezencnaListina(new UctovnaJednotka { Nazov = "SVB" }, byty, new DateOnly(2026, 5, 26), f);
        Assert.True(new FileInfo(f).Length > 1000);
        Assert.Equal(2, byty[2].VlastniciKu(new DateOnly(2026, 5, 26)).Count);
        var out_ = Environment.GetEnvironmentVariable("UCT_PDF_DIR");
        if (out_ is not null) File.Copy(f, Path.Combine(out_, "prezencna.pdf"), true);
        Directory.Delete(dir, true);
    }
}
