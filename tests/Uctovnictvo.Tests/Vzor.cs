using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Tests;

/// <summary>
/// Dom so 60 bytmi postavený tak, aby byt č. 3 zodpovedal vzoru „rok 2025 byt 3.pdf“
/// (plocha 44,53 z 2 982,41 m², spotreba 39,285 z 1 000,85 m³).
/// </summary>
public static class Vzor
{
    public const int Rok = 2025;

    public static (List<Priestor> Byty, List<KategoriaNakladov> Kategorie) Dom()
    {
        int id = 1;
        KategoriaNakladov K(string nazov, KlucRozpoctu kluc, decimal naklad, bool fo = false, string? len = null)
        {
            var k = new KategoriaNakladov { Id = id, Poradie = id, Nazov = nazov, Kluc = kluc, JeFondOprav = fo, LenPriestory = len };
            id++;
            if (!fo) k.Naklady.Add(new NakladRoka { Rok = Rok, Suma = naklad });
            return k;
        }
        var kat = new List<KategoriaNakladov>
        {
            K("správcovská činnosť", KlucRozpoctu.RovnakoNaPriestor, 19190.88m),
            K("upratovanie spol. častí domu, kosenie, sneh", KlucRozpoctu.Plocha, 7818.96m),
            K("el. energia spol. priestorov", KlucRozpoctu.Plocha, 746.01m),
            K("stála služba", KlucRozpoctu.RovnakoNaPriestor, 9438.12m),
            K("odmeny pre volené funkcie", KlucRozpoctu.RovnakoNaPriestor, 0m),
            K("vodné a stočné", KlucRozpoctu.SpotrebaVody, 3840.89m),
            K("čistenie a revízia komínov", KlucRozpoctu.RovnakoNaPriestor, 240m, len: "42,50,52,53,55"),
            K("iné náklady", KlucRozpoctu.RovnakoNaPriestor, 1108.73m),
            K("fond opráv", KlucRozpoctu.Plocha, 0m, fo: true),
        };

        decimal[] zalohyBytu3 = { 243.00m, 90.84m, 53.44m, 119.52m, 23.88m, 39.60m, 0m, 0m, 200.98m };
        var byty = new List<Priestor>();
        for (int n = 1; n <= 60; n++)
        {
            var p = new Priestor { Id = n, Cislo = n.ToString(), VariabilnySymbol = n.ToString() };
            var pr = new PriestorRok { Rok = Rok, StavVodomeruZaciatok = 0, DatumOdpoctuZaciatok = new DateOnly(2025, 1, 10), DatumOdpoctuKoniec = new DateOnly(2026, 1, 10) };
            if (n == 3)
            {
                p.Plocha = 44.53m;
                p.PlochaBalkona = 13.29m;
                p.CisloVodomeru = "69241621";
                pr.StavVodomeruZaciatok = 143.000m;
                pr.StavVodomeruKoniec = 182.285m;
                pr.PoistnePredpis = 27.02m;
                pr.ZostatokMinulyRok = -334.40m;
                p.Vlastnici.Add(new Vlastnik { Meno = "PaedDr. Patrik Voštinár PhD." });
                for (int i = 0; i < kat.Count; i++)
                    p.Predpisy.Add(new PredpisPolozky { Rok = Rok, KategoriaId = kat[i].Id, RocnaSuma = zalohyBytu3[i] });
                for (int m = 1; m <= 12; m++)
                    p.Uhrady.Add(new Uhrada { Datum = new DateOnly(Rok, m, 15), Suma = m == 2 ? 37.98m : 65m });
                p.Uhrady.Add(new Uhrada { Datum = new DateOnly(Rok, 2, 15), Suma = 27.02m, Ucel = UcelUhrady.Poistne });
                p.Uhrady.Add(new Uhrada { Datum = new DateOnly(Rok, 6, 15), Suma = 334.40m, Ucel = UcelUhrady.MinulyRok });
            }
            else
            {
                p.Plocha = 2937.88m / 59m;
                pr.StavVodomeruKoniec = 961.565m / 59m;
                for (int i = 0; i < kat.Count; i++)
                    p.Predpisy.Add(new PredpisPolozky { Rok = Rok, KategoriaId = kat[i].Id, RocnaSuma = zalohyBytu3[i] });
            }
            p.Roky.Add(pr);
            byty.Add(p);
        }
        // história spotreby bytu 3
        decimal[] hist = { 18.594m, 48.273m, 34.720m, 59.947m };
        for (int i = 0; i < 4; i++)
            byty[2].Roky.Add(new PriestorRok { Rok = 2021 + i, StavVodomeruZaciatok = 0, StavVodomeruKoniec = hist[i] });
        return (byty, kat);
    }
}
