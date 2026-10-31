using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class MzdyTests
{
    static readonly ParametreMzdy P = Mzdy.Parametre(2026)!;

    [Fact]
    public void Cista_mzda_1800_podla_prikladu()
    {
        // príklad 2026: hrubá 1 800 €, SP 169,20, ZP 90,00, NČZD 497,23 → preddavok 198,28; bonus 200 → čistá 1 542,52
        var z = new Zamestnanec { Vyhlasenie = true, DanovyBonusMesacne = 200 };
        var v = Mzdy.Vypocitaj(P, z, 1800);
        Assert.Equal(169.20m, v.SpZamestnanec);
        Assert.Equal(90.00m, v.ZpZamestnanec);
        Assert.Equal(1540.80m, v.ZakladPredNczd);
        Assert.Equal(1043.57m, v.ZakladDane);
        Assert.Equal(198.28m, v.Preddavok);
        Assert.Equal(1542.52m, v.Cista);
        Assert.Equal(453.60m, v.SpZamestnavatel); // 25,2 %
        Assert.Equal(198.00m, v.ZpZamestnavatel); // 11 %
        Assert.Equal(2451.60m, v.CenaPrace);
    }

    [Fact]
    public void Bez_vyhlasenia_sa_neuplatni_nezdanitelna_cast_ani_bonus()
    {
        var v = Mzdy.Vypocitaj(P, new Zamestnanec { Vyhlasenie = false, DanovyBonusMesacne = 100 }, 1000);
        Assert.Equal(0m, v.Nczd);
        Assert.Equal(0m, v.DanovyBonus);
        Assert.Equal(Math.Round((1000m - 94m - 50m) * 0.19m, 2), v.Preddavok);
    }

    [Fact]
    public void Strop_vymeriavacieho_zakladu_a_vyssie_pasmo()
    {
        var v = Mzdy.Vypocitaj(P, new Zamestnanec(), 20000);
        Assert.Equal(Math.Round(16764m * 0.094m, 2), v.SpZamestnanec);
        Assert.Equal(1000m, v.ZpZamestnanec); // zdravotné bez stropu
        Assert.True(v.Preddavok > (v.ZakladDane * 0.25m));
    }

    [Fact]
    public void Zauctovanie_a_rekapitulacia()
    {
        var a = new Zamestnanec { Id = 1, Vyhlasenie = true, ZdravotnaPoistovna = "VšZP" };
        var b = new Zamestnanec { Id = 2, Vyhlasenie = true, ZdravotnaPoistovna = "Dôvera" };
        var mzdy = new[] { Mzdy.Spracuj(P, a, 2026, 3, 1800), Mzdy.Spracuj(P, b, 2026, 3, 1200) };
        var r = Mzdy.Rekapitulacia(2026, 3, mzdy);
        Assert.Equal(3000m, r.Hruba);
        Assert.Equal(2, r.Zdravotne.Count);
        Assert.Equal(mzdy.Sum(m => m.CistaMzda), r.NaVyplatu);

        var z = Mzdy.Zauctuj(1, 2026, mzdy);
        Assert.All(z, x => Assert.Equal(new DateOnly(2026, 3, 31), x.Datum));
        var pv = Podvojne.Predvaha(z, Podvojne.PredvolenyRozvrh(1), 2026);
        // 331 po zaúčtovaní = čistá mzda na výplatu
        Assert.Equal(-r.NaVyplatu, pv.Single(o => o.Ucet == "331").Zostatok);
        Assert.Equal(-(r.SocialnaPoistovna + r.Zdravotne.Sum(x => x.Suma)), pv.Single(o => o.Ucet == "336").Zostatok);
        Assert.Equal(-r.PreddavkyNaDan, pv.Single(o => o.Ucet == "342").Zostatok);
    }

    [Fact]
    public void Pracuje_v_mesiaci()
    {
        var z = new Zamestnanec { DatumNastupu = new DateOnly(2026, 3, 15), DatumUkoncenia = new DateOnly(2026, 6, 1) };
        Assert.False(Mzdy.PracujeVMesiaci(z, 2026, 2));
        Assert.True(Mzdy.PracujeVMesiaci(z, 2026, 3));
        Assert.True(Mzdy.PracujeVMesiaci(z, 2026, 6));
        Assert.False(Mzdy.PracujeVMesiaci(z, 2026, 7));
    }
}
