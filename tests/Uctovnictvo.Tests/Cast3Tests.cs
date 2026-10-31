using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class OdpisyTests
{
    [Fact]
    public void Rovnomerne_s_pomernou_castou_podla_financnej_spravy()
    {
        // príklad: 12 000 €, 1. skupina (4 roky), zaradené v septembri 2026
        var m = new Majetok { VstupnaCena = 12000, OdpisovaSkupina = 1, DatumZaradenia = new DateOnly(2026, 9, 15) };
        var p = Odpisy.Plan(m);
        Assert.Equal(new[] { 2026, 2027, 2028, 2029, 2030 }, p.Select(x => x.Rok));
        Assert.Equal(new[] { 1000m, 3000m, 3000m, 3000m, 2000m }, p.Select(x => x.Odpis));
        Assert.Equal(0m, p[^1].ZostatkovaCena);
    }

    [Fact]
    public void Rovnomerne_pri_zaradeni_v_januari_bez_zvysku()
    {
        var m = new Majetok { VstupnaCena = 1000, OdpisovaSkupina = 0, DatumZaradenia = new DateOnly(2026, 1, 5) };
        var p = Odpisy.Plan(m);
        Assert.Equal(new[] { 500m, 500m }, p.Select(x => x.Odpis));
    }

    [Fact]
    public void Zrychlene_2_skupina_podla_prikladu_financnej_spravy()
    {
        // FS: 50 000 €, 2. skupina, zaradené v máji 2026 → 5 555,52; 13 888,89; 11 111,11; 8 333,33; 5 555,55; 2 777,78; zvyšok 2 777,81
        var m = new Majetok { VstupnaCena = 50000, OdpisovaSkupina = 2, Metoda = MetodaOdpisovania.Zrychlena, DatumZaradenia = new DateOnly(2026, 5, 1) };
        var p = Odpisy.Plan(m);
        Assert.Equal(7, p.Count);
        Assert.Equal(5555.52m, p[0].Odpis);
        Assert.Equal(13888.89m, p[1].Odpis);
        Assert.Equal(11111.11m, p[2].Odpis);
        Assert.Equal(8333.34m, p[3].Odpis); // 8 333,335 zaokrúhlené matematicky
        Assert.Equal(5555.55m, p[4].Odpis);
        Assert.Equal(2777.78m, p[5].Odpis);
        Assert.Equal(50000m, p.Sum(x => x.Odpis));
        Assert.Equal(2777.81m, p[6].Odpis);
    }

    [Fact]
    public void Vyradenie_predajom_odpis_za_cele_mesiace()
    {
        // ročný odpis 3 000, predaj v marci 2028 → 2 mesiace = 500
        var m = new Majetok { VstupnaCena = 12000, OdpisovaSkupina = 1, DatumZaradenia = new DateOnly(2026, 1, 10), DatumVyradenia = new DateOnly(2028, 3, 20) };
        var p = Odpisy.Plan(m);
        Assert.Equal(3, p.Count);
        Assert.Equal(500m, p[2].Odpis);
        Assert.Equal(5500m, p[2].ZostatkovaCena);
    }

    [Fact]
    public void Podiel_na_podnikanie_znizuje_uplatneny_odpis()
    {
        var m = new Majetok { VstupnaCena = 20000, OdpisovaSkupina = 1, DatumZaradenia = new DateOnly(2026, 1, 1), PodielPodnikanie = 80 };
        var o = Odpisy.ZaRok(m, 2026)!;
        Assert.Equal(5000m, o.Odpis);
        Assert.Equal(4000m, o.UplatnenyOdpis);
    }
}

public class DanovaEvidenciaTests
{
    [Fact]
    public void Dennik_a_suhrn_z_banky_pokladne_majetku_a_jazd()
    {
        var kat = DanovaEvidencia.PredvoleneKategorie(1);
        for (int i = 0; i < kat.Count; i++) kat[i].Id = i + 1;
        KategoriaPohybu K(string kod) => kat.First(k => k.Kod == kod);

        var vydana = new Faktura { Druh = DruhFaktury.Vydana, Cislo = "20260001", DatumVystavenia = new DateOnly(2026, 2, 1), DatumSplatnosti = new DateOnly(2026, 2, 15) };
        vydana.Polozky.Add(new PolozkaFaktury { CenaZaJednotku = 1000 });
        var neuhradena = new Faktura { Druh = DruhFaktury.Vydana, Cislo = "20260002", DatumVystavenia = new DateOnly(2026, 12, 20), DatumSplatnosti = new DateOnly(2027, 1, 3) };
        neuhradena.Polozky.Add(new PolozkaFaktury { CenaZaJednotku = 300 });

        var pohyby = new List<BankovyPohyb>
        {
            new() { Datum = new DateOnly(2026, 2, 10), Suma = 1000 },
            new() { Datum = new DateOnly(2026, 3, 31), Suma = -4.5m, Popis = "Poplatok za vedenie účtu" },
            new() { Datum = new DateOnly(2026, 4, 8), Suma = -250m, Popis = "Odvod", Kategoria = K("poistne") },
            new() { Datum = new DateOnly(2026, 5, 2), Suma = -500m, Popis = "výber" },
        };
        Banka.Uhrad(vydana, pohyby[0]);
        var doklady = new List<PokladnicnyDoklad>
        {
            new() { Druh = DruhPokladnicnehoDokladu.Vydavok, Cislo = "VPD2026001", Datum = new DateOnly(2026, 6, 1), Suma = 40, Ucel = "Papier", Kategoria = K("material") },
            new() { Druh = DruhPokladnicnehoDokladu.Vydavok, Cislo = "VPD2026002", Datum = new DateOnly(2026, 6, 2), Suma = 15, Ucel = "Pokuta", Kategoria = K("nedanove") },
        };

        Assert.Equal(2, DanovaEvidencia.Zarad(pohyby, doklady, kat));
        Assert.Equal("trzby", pohyby[0].Kategoria!.Kod);
        Assert.Equal("poplatky", pohyby[1].Kategoria!.Kod);
        Assert.Null(pohyby[3].Kategoria);

        var dennik = DanovaEvidencia.Dennik(pohyby, doklady, kat, 2026);
        Assert.Equal(6, dennik.Count);
        var majetok = new[] { new Majetok { VstupnaCena = 2400, OdpisovaSkupina = 1, DatumZaradenia = new DateOnly(2026, 1, 1) } };
        var s = DanovaEvidencia.Suhrn(dennik, majetok, 100m, new[] { vydana, neuhradena }, 2026);
        Assert.Equal(1000m, s.DanovePrijmy);
        Assert.Equal(4.5m + 250m + 40m, s.DanoveVydavkyPeniazne);
        Assert.Equal(600m, s.Odpisy);
        Assert.Equal(15m, s.NedanoveVydavky);
        Assert.Equal(500m, s.Nezaradene);
        Assert.Equal(1000m - (294.5m + 600m + 100m), s.Vysledok);
        Assert.Single(s.Pohladavky);
        Assert.Same(neuhradena, s.Pohladavky[0]);

        var j = new UctovnaJednotka { Nazov = "SZČO Test", Ico = "12345678" };
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(PdfDanovaEvidencia.Vytvor(j, s, dennik, majetok), 0, 4));
        var xlsx = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        ExportExcel.DanovaEvidencia(j, s, dennik, majetok, xlsx);
        Assert.True(new FileInfo(xlsx).Length > 0);
        File.Delete(xlsx);
    }
}

public class KnihaJazdTests
{
    [Fact]
    public void Sadzby_podla_datumu()
    {
        var s = KnihaJazd.PredvoleneSadzby;
        Assert.Null(KnihaJazd.Sadzba(new DateOnly(2025, 5, 31), s));
        Assert.Equal(0.296m, KnihaJazd.Sadzba(new DateOnly(2025, 12, 31), s));
        Assert.Equal(0.313m, KnihaJazd.Sadzba(new DateOnly(2026, 3, 1), s));
        var text = KnihaJazd.ZapisSadzby(s);
        Assert.Equal(s, KnihaJazd.CitajSadzby(text));
        Assert.Null(KnihaJazd.CitajSadzby("nezmysel"));
    }

    [Fact]
    public void Nahrada_za_jazdu_vlastnym_autom()
    {
        var v = new Vozidlo { Nazov = "Škoda", Spotreba = 5.6m, CenaPaliva = 1.559m };
        var j = new Jazda { Datum = new DateOnly(2026, 3, 2), Km = 249.4m, Odkial = "Žilina", Kam = "Bratislava" };
        v.Jazdy.Add(j);
        v.Jazdy.Add(new Jazda { Datum = new DateOnly(2026, 3, 5), Km = 30, Sukromna = true });
        var (z, p, chyba) = KnihaJazd.Nahrada(j, v, KnihaJazd.PredvoleneSadzby);
        Assert.False(chyba);
        Assert.Equal(78.25m, z);   // 250 km × 0,313 = 78,25
        Assert.Equal(21.83m, p);   // 250 × 5,6 / 100 × 1,559 = 21,826 → nahor 21,83
        var m = KnihaJazd.Prehlad(v, 2026, KnihaJazd.PredvoleneSadzby)[2];
        Assert.Equal(249.4m, m.KmSluzobne);
        Assert.Equal(30m, m.KmSukromne);
        Assert.Equal(100.08m, m.Spolu);
        v.VObchodnomMajetku = true;
        Assert.Equal(0m, KnihaJazd.NahradyZaRok(new[] { v }, 2026, KnihaJazd.PredvoleneSadzby));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(PdfDanovaEvidencia.KnihaJazdPdf(new UctovnaJednotka { Nazov = "X" }, v, 2026, KnihaJazd.PredvoleneSadzby), 0, 4));
    }
}

public class Cast3DatabazaTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "c3-" + Guid.NewGuid().ToString("N"));
    public Cast3DatabazaTests() => Directory.CreateDirectory(dir);
    public void Dispose() { try { Directory.Delete(dir, true); } catch { } }

    [Fact]
    public void Ulozi_kategorie_majetok_a_jazdy()
    {
        var db = Path.Combine(dir, "t.db");
        Databaza.Priprav(db);
        using (var c = AppDbContext.Open(db))
        {
            var j = new UctovnaJednotka { Typ = TypJednotky.Szco, Nazov = "SZČO" };
            c.Jednotky.Add(j);
            c.SaveChanges();
            var kat = DanovaEvidencia.PredvoleneKategorie(j.Id);
            c.KategoriePohybov.AddRange(kat);
            c.BankovePohyby.Add(new BankovyPohyb { JednotkaId = j.Id, Datum = new DateOnly(2026, 1, 1), Suma = -10, Kategoria = kat[8] });
            c.Majetok.Add(new Majetok { JednotkaId = j.Id, Nazov = "Notebook", InventarneCislo = "M2026001", VstupnaCena = 1800, DatumZaradenia = new DateOnly(2026, 2, 1) });
            var v = new Vozidlo { JednotkaId = j.Id, Nazov = "Auto" };
            v.Jazdy.Add(new Jazda { Datum = new DateOnly(2026, 2, 2), Km = 12, Odkial = "A", Kam = "B" });
            c.Vozidla.Add(v);
            c.SaveChanges();
        }
        using (var c = AppDbContext.Open(db))
        {
            Assert.Equal(17, c.KategoriePohybov.Count());
            Assert.Equal("poplatky", c.BankovePohyby.Include(p => p.Kategoria).Single().Kategoria!.Kod);
            Assert.Single(c.Vozidla.Include(v => v.Jazdy).Single().Jazdy);
            Assert.Equal(1800m, c.Majetok.Single().VstupnaCena);
            // zmazanie kategórie nezmaže pohyb
            c.KategoriePohybov.Remove(c.KategoriePohybov.Single(k => k.Kod == "poplatky"));
            c.SaveChanges();
            Assert.Null(c.BankovePohyby.Single().KategoriaId);
        }
    }
}
