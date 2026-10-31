using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class DanovePriznanieTests
{
    static readonly ParametreFo P2025 = DanovePriznanie.Fo(2025)!;
    static readonly ParametreFo P2026 = DanovePriznanie.Fo(2026)!;

    [Theory]
    [InlineData(2025, 20000, 5753.79)]
    [InlineData(2025, 25426.27, 5753.79)]
    [InlineData(2025, 30000, 4610.36)]   // 12 110,36 − 30 000 / 4
    [InlineData(2025, 60000, 0)]
    [InlineData(2026, 26083.13, 5966.73)]
    [InlineData(2026, 30000, 4661.11)]   // 14 661,11 − 30 000 / 3
    [InlineData(2026, 43983.32, 0)]
    public void Nezdanitelna_cast_podla_financnej_spravy(int rok, decimal zd, decimal ocakavane) =>
        Assert.Equal(ocakavane, DanovePriznanie.NezdanitelnaCast(DanovePriznanie.Fo(rok)!, zd));

    [Fact]
    public void Dan_podla_pasiem_z_prikladov_financnej_spravy()
    {
        // FS FAQ 2025: ZD 62 000 €, príjmy nad 100 000 € → 9 203,87 + 3 389,64
        Assert.Equal(12593.51m, DanovePriznanie.DanFo(P2025, 62000, 150000));
        // FS FAQ 2026: ZD 60 000 €, príjmy nad 100 000 € → 8 356,83 + 4 004,17
        Assert.Equal(12361.00m, DanovePriznanie.DanFo(P2026, 60000, 150000));
        // príjmy do 100 000 € → 15 %
        Assert.Equal(9000m, DanovePriznanie.DanFo(P2026, 60000, 100000));
    }

    [Fact]
    public void Szco_s_pausalnymi_vydavkami()
    {
        // príklad: príjmy 32 400 €, paušál 60 % = 19 440 € + poistné 2 352 € → ZD 10 608 €
        var v = new VstupyDp { PausalneVydavky = true, ZaplatenePoistne = 2352 };
        var r = DanovePriznanie.VypocitajFo(P2025, 32400, 15000, 0, v);
        Assert.Equal(21792m, r.Vydavky);
        Assert.Equal(10608m, r.ZakladDane);
        Assert.Equal(5753.79m, r.Nczd);
        Assert.Equal(4854.21m, r.ZakladPoNczd);
        Assert.Equal(728.13m, r.Dan);
        Assert.True(r.PovinnostPodat);
        Assert.Contains(r.Riadky, x => x.Riadok == "r. 94" && x.Suma == 4854.21m);
    }

    [Fact]
    public void Pausal_je_obmedzeny_na_20000()
    {
        var r = DanovePriznanie.VypocitajFo(P2026, 50000, 0, 0, new VstupyDp { PausalneVydavky = true, ZaplatenePoistne = 0 });
        Assert.Equal(20000m, r.Vydavky);
    }

    [Fact]
    public void Strata_a_nizke_prijmy()
    {
        var r = DanovePriznanie.VypocitajFo(P2025, 2000, 3000, 0, new VstupyDp());
        Assert.Equal(1000m, r.Strata);
        Assert.Equal(0m, r.Dan);
        Assert.True(r.PovinnostPodat); // strata → povinnosť podať
        var nizke = DanovePriznanie.VypocitajFo(P2025, 2000, 500, 0, new VstupyDp());
        Assert.False(nizke.PovinnostPodat); // príjmy do 2 876,90 €
    }

    [Fact]
    public void Sro_dan_a_minimalna_dan()
    {
        var rozvrh = Podvojne.PredvolenyRozvrh(1);
        ObratUctu O(string u, decimal md, decimal d) => new(u, "", 0, 0, md, d);
        var pv = new List<ObratUctu> { O("602", 0, 80000), O("518", 30000, 0), O("513", 1000, 0), O("591", 4000, 0) };
        var r = DanovePriznanie.VypocitajPo(DanovePriznanie.Po(2026)!, pv, rozvrh, new VstupyDp { ZaplatenePreddavky = 2000 });
        Assert.Equal(49000m, r.VysledokHospodarenia); // 80 000 − 30 000 − 1 000 (VH pred zdanením, bez 591)
        Assert.Equal(1000m, r.Pripocitatelne);        // 513 reprezentácia je nedaňová
        Assert.Equal(50000m, r.ZakladDane);
        Assert.Equal(10m, r.Sadzba);
        Assert.Equal(5000m, r.Dan);
        Assert.Equal(960m, r.MinimalnaDan);
        Assert.Equal(5000m, r.DanovaPovinnost);
        Assert.Equal(3000m, r.NaUhradu);

        var strata = DanovePriznanie.VypocitajPo(DanovePriznanie.Po(2025)!, new List<ObratUctu> { O("602", 0, 20000), O("518", 25000, 0) }, rozvrh, new VstupyDp());
        Assert.Equal(5000m, strata.Strata);
        Assert.Equal(340m, strata.DanovaPovinnost);
        Assert.Equal(0m, DanovePriznanie.VypocitajPo(DanovePriznanie.Po(2025)!, new List<ObratUctu> { O("602", 0, 20000), O("518", 25000, 0) }, rozvrh, new VstupyDp { PrveObdobie = true }).DanovaPovinnost);
        Assert.Equal(21m, DanovePriznanie.VypocitajPo(DanovePriznanie.Po(2025)!, new List<ObratUctu> { O("602", 0, 150000) }, rozvrh, new VstupyDp()).Sadzba);
    }
}
