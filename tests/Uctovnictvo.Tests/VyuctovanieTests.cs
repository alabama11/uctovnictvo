using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class VyuctovanieTests
{
    [Fact]
    public void Byt_3_zodpoveda_vzoru_2025()
    {
        var (byty, kat) = Vzor.Dom();
        var v = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat);
        var b = v.Byty.Single(x => x.Priestor.Cislo == "3");

        Assert.Equal(new[] { 319.85m, 116.74m, 11.14m, 157.30m, 0m, 150.76m, 0m, 18.48m, 200.98m }, b.Polozky.Select(p => p.Skutocne));
        Assert.Equal(new[] { -76.85m, -25.90m, 42.30m, -37.78m, 23.88m, -111.16m, 0m, -18.48m, 0m }, b.Polozky.Select(p => p.Rozdiel));
        Assert.Equal(771.26m, b.PredpisZaloh);
        Assert.Equal(64.27m, b.MesacnyPredpis);
        Assert.Equal(752.98m, b.ZaplateneZalohy);
        Assert.Equal(-18.28m, b.NedoplatokNaZalohach);
        Assert.Equal(975.25m, b.SkutocneSpolu);
        Assert.Equal(-203.99m, b.VysledokNakladov);
        Assert.Equal(0m, b.RozdielMinulyRok);
        Assert.Equal(0m, b.RozdielPoistne);
        // vo vzore je −222,28 € kvôli zaokrúhleniu v Exceli; súčet zobrazených riadkov je −222,27 €
        Assert.Equal(-222.27m, b.Vysledok);

        Assert.NotNull(b.Voda);
        Assert.Equal(39.285m, b.Voda!.Spotreba);
        Assert.Equal(3.84m, b.Voda.CenaZaM3);
        Assert.Equal(4, b.Voda.Historia.Count);
        Assert.Empty(v.Upozornenia);
    }

    [Fact]
    public void Komin_sa_deli_len_medzi_vybrane_byty()
    {
        var (byty, kat) = Vzor.Dom();
        var v = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat);
        var komin = kat.Single(k => k.Nazov.StartsWith("čistenie")).Id;
        var podiely = v.Byty.Select(b => (b.Priestor.Cislo, b.Polozky.Single(p => p.Kategoria.Id == komin).Skutocne)).ToList();

        Assert.All(podiely.Where(x => new[] { "42", "50", "52", "53", "55" }.Contains(x.Cislo)), x => Assert.Equal(48m, x.Skutocne));
        Assert.All(podiely.Where(x => !new[] { "42", "50", "52", "53", "55" }.Contains(x.Cislo)), x => Assert.Equal(0m, x.Skutocne));
    }

    [Fact]
    public void Neuhradeny_nedoplatok_minuleho_roka_a_poistne_sa_prenesu()
    {
        var p = new Priestor { Id = 1, Cislo = "1", Plocha = 50 };
        p.Roky.Add(new PriestorRok { Rok = 2025, ZostatokMinulyRok = -100m, PoistnePredpis = 30m });
        p.Uhrady.Add(new Uhrada { Datum = new DateOnly(2025, 3, 1), Suma = 40m, Ucel = UcelUhrady.MinulyRok });

        var b = Vyuctovanie.Vypocitaj(2025, new[] { p }, Array.Empty<KategoriaNakladov>()).Byty[0];

        Assert.Equal(-60m, b.RozdielMinulyRok);
        Assert.Equal(-30m, b.RozdielPoistne);
        Assert.Equal(-90m, b.Vysledok);
    }

    [Fact]
    public void Upozorni_na_chybajuci_odpocet_predpis_a_nulovy_kluc()
    {
        var p = new Priestor { Id = 1, Cislo = "1", Plocha = 50 };
        var voda = new KategoriaNakladov { Id = 1, Nazov = "Voda", Kluc = KlucRozpoctu.SpotrebaVody };
        voda.Naklady.Add(new NakladRoka { Rok = 2025, Suma = 100m });

        var v = Vyuctovanie.Vypocitaj(2025, new[] { p }, new[] { voda });

        Assert.Contains(v.Upozornenia, u => u.Contains("nemá odpočet vody"));
        Assert.Contains(v.Upozornenia, u => u.Contains("nemožno rozpočítať"));
        Assert.Contains(v.Upozornenia, u => u.Contains("nemá predpis"));
    }

    [Fact]
    public void Rozdelenie_na_centy_presne_sedi_na_sumu()
    {
        var casti = Vyuctovanie.RozdelNaCenty(100m, new[] { 1m, 1m, 1m });
        Assert.Equal(new[] { 33.34m, 33.33m, 33.33m }, casti);
    }

    [Fact]
    public void Byty_su_zoradene_prirodzene()
    {
        var cisla = new[] { "10", "2", "A10", "A2", "1" }.OrderBy(x => x, CisloComparer.Instance);
        Assert.Equal(new[] { "1", "2", "10", "A2", "A10" }, cisla);
    }
}
