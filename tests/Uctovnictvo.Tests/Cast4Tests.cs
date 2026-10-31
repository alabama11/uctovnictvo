using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class PodvojneTests
{
    static Faktura Fa(DruhFaktury druh, string cislo, DateOnly d, decimal suma, TypFaktury typ = TypFaktury.Faktura)
    {
        var f = new Faktura { Druh = druh, Typ = typ, Cislo = cislo, DatumVystavenia = d, DatumDodania = d, DatumSplatnosti = d.AddDays(14), PartnerNazov = "Partner" };
        f.Polozky.Add(new PolozkaFaktury { Popis = "Služba", Mnozstvo = 1, CenaZaJednotku = suma });
        return f;
    }

    [Fact]
    public void Predkontacie_faktur_a_uhrad()
    {
        var vydana = Fa(DruhFaktury.Vydana, "20260001", new DateOnly(2026, 2, 3), 1000);
        var prijata = Fa(DruhFaktury.Prijata, "PF20260001", new DateOnly(2026, 3, 1), 200);
        prijata.KategoriaPohybu = new KategoriaPohybu { Kod = "material", Druh = DruhKategorie.Vydavok };
        var b = new BankovyPohyb { Datum = new DateOnly(2026, 2, 20), Suma = 1000 };
        Banka.Uhrad(vydana, b);
        var poplatok = new BankovyPohyb { Datum = new DateOnly(2026, 2, 28), Suma = -5, Kategoria = new KategoriaPohybu { Kod = "poplatky", Druh = DruhKategorie.Vydavok } };
        var nezaradeny = new BankovyPohyb { Datum = new DateOnly(2026, 3, 2), Suma = 50 };
        var vpd = new PokladnicnyDoklad { Cislo = "VPD2026001", Datum = new DateOnly(2026, 3, 5), Druh = DruhPokladnicnehoDokladu.Vydavok, Suma = 200, Ucel = "Úhrada" };
        vpd.Uhrady.Add(new UhradaFaktury { Faktura = prijata, Suma = 200, Datum = vpd.Datum });

        var z = Podvojne.Zauctuj(1, 2026, new[] { vydana, prijata }, new[] { b, poplatok, nezaradeny }, new[] { vpd }, Array.Empty<Majetok>());
        Assert.Contains(z, x => x.Md == "311" && x.D == "602" && x.Suma == 1000);
        Assert.Contains(z, x => x.Md == "501" && x.D == "321" && x.Suma == 200);
        Assert.Contains(z, x => x.Md == "221" && x.D == "311" && x.Suma == 1000);
        Assert.Contains(z, x => x.Md == "568" && x.D == "221" && x.Suma == 5);
        Assert.Contains(z, x => x.Md == "221" && x.D == "395" && x.Suma == 50);
        Assert.Contains(z, x => x.Md == "321" && x.D == "211" && x.Suma == 200);
        Assert.All(z, x => Assert.True(x.Suma > 0));
    }

    [Fact]
    public void Zalohova_faktura_sa_uctuje_az_uhradou_a_vyuctovanim()
    {
        var zf = Fa(DruhFaktury.Vydana, "ZF20260001", new DateOnly(2026, 1, 10), 500, TypFaktury.Zalohova);
        var b = new BankovyPohyb { Datum = new DateOnly(2026, 1, 15), Suma = 500 };
        Banka.Uhrad(zf, b);
        var f = Faktury.ZoZalohovej(zf, new UctovnaJednotka(), new DateOnly(2026, 2, 1), Array.Empty<string>());
        f.Polozky[0].CenaZaJednotku = 800; // skutočná dodávka 800, záloha 500 sa odpočíta
        var z = Podvojne.Zauctuj(1, 2026, new[] { zf, f }, new[] { b }, Array.Empty<PokladnicnyDoklad>(), Array.Empty<Majetok>());
        Assert.DoesNotContain(z, x => x.Doklad == "ZF20260001");
        Assert.Contains(z, x => x.Md == "221" && x.D == "324" && x.Suma == 500);
        Assert.Contains(z, x => x.Md == "311" && x.D == "602" && x.Suma == 800);
        Assert.Contains(z, x => x.Md == "324" && x.D == "311" && x.Suma == 500);
        var pv = Podvojne.Predvaha(z, Podvojne.PredvolenyRozvrh(1), 2026);
        Assert.Equal(0, pv.Single(o => o.Ucet == "324").Zostatok);
        Assert.Equal(300, pv.Single(o => o.Ucet == "311").Zostatok);
    }

    [Fact]
    public void Odpisy_sa_uctuju_k_31_decembru()
    {
        var m = new Majetok { InventarneCislo = "M2026001", Nazov = "PC", VstupnaCena = 2400, OdpisovaSkupina = 1, DatumZaradenia = new DateOnly(2026, 1, 5) };
        var z = Podvojne.Zauctuj(1, 2026, Array.Empty<Faktura>(), Array.Empty<BankovyPohyb>(), Array.Empty<PokladnicnyDoklad>(), new[] { m });
        Assert.Equal(2, z.Count);
        Assert.Contains(z, x => (x.Md, x.D, x.Suma, x.Datum) == ("022", "042", 2400m, new DateOnly(2026, 1, 5)));
        Assert.Contains(z, x => (x.Md, x.D, x.Suma, x.Datum) == ("551", "082", 600m, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Vyradenie_majetku_vynuluje_obstaravaciu_cenu()
    {
        // 12 000 €, 1. skupina, zaradené 1/2026, predané 20. 3. 2028 → odpis 2028 = 500, oprávky 6 500, ZC 5 500
        var m = new Majetok { InventarneCislo = "M1", Nazov = "Stroj", VstupnaCena = 12000, OdpisovaSkupina = 1, DatumZaradenia = new DateOnly(2026, 1, 10), DatumVyradenia = new DateOnly(2028, 3, 20) };
        var z = Podvojne.Zauctuj(1, 2028, Array.Empty<Faktura>(), Array.Empty<BankovyPohyb>(), Array.Empty<PokladnicnyDoklad>(), new[] { m });
        Assert.Contains(z, x => (x.Md, x.D, x.Suma) == ("551", "082", 500m));
        Assert.Contains(z, x => (x.Md, x.D, x.Suma) == ("082", "022", 6500m));
        Assert.Contains(z, x => (x.Md, x.D, x.Suma) == ("541", "022", 5500m));
    }

    [Fact]
    public void Zavierka_je_vyrovnana_a_prenasa_sa_do_dalsieho_roka()
    {
        var rozvrh = Podvojne.PredvolenyRozvrh(1);
        var ps = new List<UctovnyZapis>
        {
            new() { Datum = new(2026, 1, 1), Md = "221", D = "701", Suma = 5000, Zdroj = ZdrojZapisu.PociatocnyStav },
            new() { Datum = new(2026, 1, 1), Md = "701", D = "411", Suma = 5000, Zdroj = ZdrojZapisu.PociatocnyStav },
        };
        var vydana = Fa(DruhFaktury.Vydana, "20260001", new DateOnly(2026, 2, 3), 3000);
        var prijata = Fa(DruhFaktury.Prijata, "PF20260001", new DateOnly(2026, 3, 1), 1200);
        var b = new BankovyPohyb { Datum = new DateOnly(2026, 3, 1), Suma = 3000 };
        Banka.Uhrad(vydana, b);
        var m = new Majetok { InventarneCislo = "M1", Nazov = "PC", VstupnaCena = 2400, OdpisovaSkupina = 1, DatumZaradenia = new DateOnly(2026, 1, 5) };
        var nakup = new BankovyPohyb { Datum = new DateOnly(2026, 1, 5), Suma = -2400, Kategoria = new KategoriaPohybu { Kod = "majetok", Druh = DruhKategorie.Neutralny } };
        var zapisy = ps.Concat(Podvojne.Zauctuj(1, 2026, new[] { vydana, prijata }, new[] { b, nakup }, Array.Empty<PokladnicnyDoklad>(), new[] { m })).ToList();

        var pv = Podvojne.Predvaha(zapisy, rozvrh, 2026);
        Assert.Equal(pv.Sum(o => o.KsMd), pv.Sum(o => o.KsD));
        var vh = Podvojne.VysledokHospodarenia(pv);
        Assert.Equal(3000 - 1200 - 600, vh);

        var suv = Podvojne.Suvaha(pv, null);
        decimal S(int r) => suv.Single(x => x.Riadok == r).Bezne;
        Assert.Equal(S(1), S(24));
        Assert.Equal(1800, S(6)); // 2400 − oprávky 600
        Assert.Equal(5600, S(22)); // 5000 + 3000 − 2400
        Assert.Equal(1200, S(39));
        Assert.Equal(5000, S(27));
        Assert.Equal(vh, S(33));
        Assert.Empty(Podvojne.Kontroly(suv, pv));

        var vzas = Podvojne.VykazZiskovAStrat(pv, null);
        decimal V(int r) => vzas.Single(x => x.Riadok == r).Bezne;
        Assert.Equal(3000, V(3));
        Assert.Equal(1200, V(11));
        Assert.Equal(600, V(14));
        Assert.Equal(vh, V(38));
        Assert.Equal(3000 - 1200, V(19));

        var ps2027 = Podvojne.PociatocneStavy(1, pv, 2027);
        Assert.All(ps2027, z => Assert.Equal(ZdrojZapisu.PociatocnyStav, z.Zdroj));
        Assert.Contains(ps2027, z => z.Md == "701" && z.D == "431" && z.Suma == vh);
        var pv2027 = Podvojne.Predvaha(ps2027, rozvrh, 2027);
        var suv2027 = Podvojne.Suvaha(pv2027, pv);
        Assert.Equal(suv2027.Single(x => x.Riadok == 1).Bezne, suv2027.Single(x => x.Riadok == 24).Bezne);
        Assert.Equal(vh, suv2027.Single(x => x.Riadok == 33).Bezne); // 431 v schvaľovaní
        Assert.Equal(S(1), suv2027.Single(x => x.Riadok == 1).Minule);
    }

    [Fact]
    public void Kontrola_upozorni_na_nezaradene_a_nevyrovnane()
    {
        var zapisy = new List<UctovnyZapis>
        {
            new() { Datum = new(2026, 1, 1), Md = "221", D = "701", Suma = 100, Zdroj = ZdrojZapisu.PociatocnyStav },
            new() { Datum = new(2026, 5, 1), Md = "221", D = "395", Suma = 40, Zdroj = ZdrojZapisu.Banka },
        };
        var pv = Podvojne.Predvaha(zapisy, Podvojne.PredvolenyRozvrh(1), 2026);
        var k = Podvojne.Kontroly(Podvojne.Suvaha(pv, null), pv);
        Assert.Equal(2, k.Count);
    }

    [Fact]
    public void Ukazkova_firma_sa_zauctuje_s_vyrovnanou_suvahou()
    {
        var dir = Directory.CreateTempSubdirectory("pod").FullName;
        try
        {
            var cesta = Path.Combine(dir, "a.db");
            Databaza.Priprav(cesta);
            using (var c = AppDbContext.Open(cesta))
            {
                var j = Ukazka.VytvorFirmu(c, 2026);
                var f = c.Faktury.Where(x => x.JednotkaId == j.Id).Include(x => x.Polozky).Include(x => x.KategoriaPohybu).ToList();
                var b = c.BankovePohyby.Where(x => x.JednotkaId == j.Id).Include(x => x.Uhrady).ThenInclude(u => u.Faktura).Include(x => x.Kategoria).ToList();
                var d = c.PokladnicneDoklady.Where(x => x.JednotkaId == j.Id).Include(x => x.Uhrady).ThenInclude(u => u.Faktura).Include(x => x.Kategoria).ToList();
                var m = c.Majetok.Where(x => x.JednotkaId == j.Id).ToList();
                var z = Podvojne.Zauctuj(j.Id, 2026, f, b, d, m);
                Assert.NotEmpty(z);
                c.Ucty.AddRange(Podvojne.PredvolenyRozvrh(j.Id));
                c.UctovneZapisy.AddRange(z);
                c.SaveChanges();
                var pv = Podvojne.Predvaha(c.UctovneZapisy.Where(x => x.JednotkaId == j.Id).ToList(), c.Ucty.Where(u => u.JednotkaId == j.Id).ToList(), 2026);
                var s = Podvojne.Suvaha(pv, null);
                Assert.Equal(s.Single(x => x.Riadok == 1).Bezne, s.Single(x => x.Riadok == 24).Bezne);
                var zapisy = c.UctovneZapisy.Where(x => x.JednotkaId == j.Id).ToList();
                var vzas = Podvojne.VykazZiskovAStrat(pv, null);
                var pdf = PdfZavierka.Zavierka(j, 2026, s, vzas, pv, Podvojne.Kontroly(s, pv));
                Assert.True(pdf.Length > 5000);
                Assert.True(PdfZavierka.Dennik(j, 2026, zapisy).Length > 2000);
                var xlsx = Path.Combine(dir, "u.xlsx");
                ExportExcel.Uctovnictvo(j, 2026, zapisy, pv, Podvojne.Saldokonto(f, new DateOnly(2026, 12, 31)), s, vzas, xlsx);
                Assert.True(new FileInfo(xlsx).Length > 5000);
                var out_ = Environment.GetEnvironmentVariable("UCT_PDF_DIR");
                if (out_ is not null) File.WriteAllBytes(Path.Combine(out_, "zavierka.pdf"), pdf);
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        finally { Directory.Delete(dir, true); }
    }
}
