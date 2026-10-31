using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class KontoTests
{
    [Fact]
    public void Konto_bytu_3_k_decembru_zodpoveda_vzoru()
    {
        var (byty, _) = Vzor.Dom();
        var k = KontoVlastnika.Vypocitaj(byty[2], Vzor.Rok, 12);
        // predpis 771,26 + poistné 27,02 + nedoplatok 2024 334,40; zaplatené 752,98 + 27,02 + 334,40
        Assert.Equal(771.26m + 27.02m, k.PredpisDoteraz);
        Assert.Equal(-18.28m, k.Zostatok);
        Assert.Equal(18.28m, k.Dlh);
    }

    [Fact]
    public void Dlh_v_priebehu_roka()
    {
        var p = new Priestor { Cislo = "1" };
        p.Predpisy.Add(new PredpisPolozky { Rok = 2025, RocnaSuma = 1200m });
        p.Uhrady.Add(new Uhrada { Datum = new DateOnly(2025, 1, 10), Suma = 100m });
        var k = KontoVlastnika.Vypocitaj(p, 2025, 4);
        Assert.Equal(-300m, k.Zostatok);
        Assert.Equal(3, k.MesiacovDlhu);
    }
}

public class ZmenaVlastnikaTests
{
    [Fact]
    public void Predaj_bytu_rozdeli_vyuctovanie_podla_dni()
    {
        var (byty, kat) = Vzor.Dom();
        var b3 = byty[2];
        b3.Vlastnici[0].VlastnikDo = new DateOnly(2025, 6, 30);
        b3.Vlastnici.Add(new Vlastnik { Meno = "Nový Vlastník", VlastnikOd = new DateOnly(2025, 7, 1) });

        var v = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat);
        var casti = v.Byty.Where(b => b.Priestor == b3).ToList();

        Assert.Equal(2, casti.Count);
        Assert.Equal("PaedDr. Patrik Voštinár PhD.", casti[0].Vlastnik);
        Assert.Equal("Nový Vlastník", casti[1].Vlastnik);
        Assert.Equal(new DateOnly(2025, 6, 30), casti[0].ObdobieDo);
        // 181 + 184 dní: súčet častí sa od celoročného líši najviac o zaokrúhlenie
        Assert.InRange(casti.Sum(c => c.SkutocneSpolu), 975.25m - 0.10m, 975.25m + 0.10m);
        // úhrady sa delia podľa dátumu, poistné a minulý rok patria prvému vlastníkovi
        Assert.Equal(6 * 65m - 27.02m, casti[0].ZaplateneZalohy);
        Assert.Equal(6 * 65m, casti[1].ZaplateneZalohy);
        Assert.Equal(27.02m, casti[0].PoistnePredpis);
        Assert.Equal(0m, casti[1].PoistnePredpis);
        Assert.Equal(-334.40m, casti[0].ZostatokMinulyRok);
        Assert.Contains(v.Upozornenia, u => u.Contains("zmenil"));
        Assert.Equal("Nový Vlastník", b3.MenaVlastnikov);
    }

    [Fact]
    public void Vymeneny_vodomer_sa_pripocita_k_spotrebe()
    {
        var r = new PriestorRok { StavVodomeruZaciatok = 0, StavVodomeruKoniec = 10, SpotrebaVymenenyVodomer = 4.5m };
        Assert.Equal(14.5m, r.SpotrebaVody);
    }
}

public class PayBySquareTests
{
    // Reťazce overené dekódovaním referenčnou knižnicou bysquare 2.13.3 (npm), ktorá vrátila presne zadané údaje.
    [Fact]
    public void Zakoduje_platbu_rovnako_ako_overeny_vzor()
    {
        Assert.Equal("0003Q00094PTIBIG92PRPD5AS3FVMTMK7DTPSB5ODFRCH79KLIBHL06M2KS5HGB7CNEU8AKKTIV8NB45BORG49G6FG66000",
            PayBySquare.Zakoduj(new PlatbaQr("SK3112000000198742637541", 64.27m, "12")));
        Assert.Equal("00072000AI4H68EUABQCD6KLN0R3IG3V6AEFA959TQCLII3VBCTEICPN023J611A0U1LU7I6AOKHU0UCMJDKEOV9FDDNQ5S3ET0Q4P9EOVPQ3QE4LCS7MFATSQ0R70GC83KN0P3V1VUPTTA2KJBUI1A67UQGUKRQI8L0PRBQG0",
            PayBySquare.Zakoduj(new PlatbaQr("SK59 1100 0000 0026 2878 2959", 222.27m, "3", null, null, "Vyúčtovanie 2025 byt 3", new DateOnly(2026, 6, 9), "Spoločenstvo Spiežovec")));
    }

    [Fact]
    public void Vytvori_png()
    {
        var png = PayBySquare.QrPng(new PlatbaQr("SK3112000000198742637541", 10m, "1"));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4));
    }
}

public class SpolocenstvoTests
{
    [Fact]
    public void Navrh_zaloh_podla_skutocnosti()
    {
        var (byty, kat) = Vzor.Dom();
        var v = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat);
        var navrh = NavrhZaloh.Vypocitaj(v, 0m);
        var sprava = kat.Single(k => k.Nazov == "správcovská činnosť");
        var fo = kat.Single(k => k.JeFondOprav);
        var odmeny = kat.Single(k => k.Nazov.StartsWith("odmeny"));
        Assert.Equal(319.92m, navrh[(3, sprava.Id)]);   // 319,85 nahor na násobok 0,12
        Assert.Equal(200.98m, navrh[(3, fo.Id)]);       // fond opráv ostáva
        Assert.Equal(23.88m, navrh[(3, odmeny.Id)]);    // bez skutočného nákladu ostáva predpis
        Assert.All(navrh.Where(x => x.Key.Item2 != fo.Id && x.Value != 0), x => Assert.Equal(0m, x.Value * 100 % 12));
    }

    [Fact]
    public void Fond_oprav_prenasa_stav_do_dalsieho_roka()
    {
        var (byty, kat) = Vzor.Dom();
        var pohyby = new List<PohybFondu>
        {
            new() { Datum = new DateOnly(2025, 1, 1), Typ = TypPohybuFondu.PociatocnyStav, Suma = 10000m },
            new() { Datum = new DateOnly(2025, 5, 3), Typ = TypPohybuFondu.Cerpanie, Suma = 2500m, Popis = "oprava strechy" },
        };
        var s2025 = FondOprav.Vypocitaj(byty, kat, pohyby, 2025);
        Assert.Equal(10000m, s2025.Zaciatok);
        Assert.Equal(60 * 200.98m, s2025.Tvorba);
        Assert.Equal(10000m + 60 * 200.98m - 2500m, s2025.Koniec);
        var s2026 = FondOprav.Vypocitaj(byty, kat, pohyby, 2026);
        Assert.Equal(s2025.Koniec, s2026.Zaciatok);
    }

    [Fact]
    public void Tlacivo_pdf_sa_vytvoria()
    {
        var (byty, kat) = Vzor.Dom();
        var j = new UctovnaJednotka { Nazov = "SVB Test", Iban = "SK5911000000002628782959", Statutar = "Predseda" };
        var v = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat);
        var dir = Directory.CreateTempSubdirectory("pdf").FullName;
        var kont = byty.Select(p => KontoVlastnika.Vypocitaj(p, Vzor.Rok, 12)).ToList();
        PdfDokumenty.Upomienky(j, kont.Where(k => k.Dlh > 0), "Donovaly", new DateOnly(2026, 1, 20), 14, Path.Combine(dir, "u.pdf"));
        PdfDokumenty.EvidencneListy(j, byty.Take(3).Select(p => PdfDokumenty.Predpis(p, kat, Vzor.Rok, new DateOnly(2025, 1, 1))), "Donovaly", new DateOnly(2025, 1, 2), Path.Combine(dir, "e.pdf"));
        PdfDokumenty.PrezencnaListina(j, byty, new DateOnly(2026, 5, 26), Path.Combine(dir, "p.pdf"));
        PdfDokumenty.RocnaSprava(j, v, FondOprav.Vypocitaj(byty, kat, Array.Empty<PohybFondu>(), Vzor.Rok), kont, "Donovaly", new DateOnly(2026, 5, 26), Path.Combine(dir, "s.pdf"));
        PdfVyuctovanie.Uloz(j, v.Byty.Take(2), new NastaveniaVyuctovania(new DateOnly(2026, 5, 26), "Donovaly"), Path.Combine(dir, "v.pdf"));
        foreach (var f in Directory.GetFiles(dir)) Assert.True(new FileInfo(f).Length > 1000, f);
        var out_ = Environment.GetEnvironmentVariable("UCT_PDF_DIR");
        if (out_ is not null) foreach (var f in Directory.GetFiles(dir)) File.Copy(f, Path.Combine(out_, Path.GetFileName(f)), true);
        Directory.Delete(dir, true);
    }
}

public class ImportExcelTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("imp").FullName;
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(dir, true); }

    [Fact]
    public void Sablona_s_udajmi_sa_da_naimportovat_do_inej_jednotky()
    {
        var db = Path.Combine(dir, "a.db");
        Databaza.Priprav(db);
        using var c = AppDbContext.Open(db);
        var zdroj = Ukazka.Vytvor(c, 2025);
        var priestory = c.Priestory.Where(p => p.JednotkaId == zdroj.Id).Include(p => p.Vlastnici).ThenInclude(v => v.Ucty)
            .Include(p => p.Roky).Include(p => p.Predpisy).ToList();
        var kat = c.Kategorie.Where(k => k.JednotkaId == zdroj.Id).OrderBy(k => k.Poradie).ToList();
        var xlsx = Path.Combine(dir, "sablona.xlsx");
        ImportExcel.Sablona(zdroj, priestory, kat, 2025, xlsx);

        var ciel = new UctovnaJednotka { Nazov = "Cieľ", Typ = TypJednotky.Spolocenstvo };
        foreach (var k in kat) ciel.Kategorie.Add(new KategoriaNakladov { Nazov = k.Nazov, Kluc = k.Kluc, JeFondOprav = k.JeFondOprav, Poradie = k.Poradie });
        c.Jednotky.Add(ciel);
        c.SaveChanges();

        var r = ImportExcel.Importuj(c, ciel, 2025, xlsx);
        Assert.Equal(12, r.Pridane);
        Assert.Empty(r.Upozornenia);
        var b1 = c.Priestory.Where(p => p.JednotkaId == ciel.Id && p.Cislo == "1").Include(p => p.Vlastnici).ThenInclude(v => v.Ucty)
            .Include(p => p.Predpisy).Include(p => p.Roky).Single();
        Assert.Equal(44.53m, b1.Plocha);
        Assert.Equal("Mária Kováčová", b1.Vlastnici.Single().Meno);
        Assert.Single(b1.Vlastnici[0].Ucty);
        Assert.Equal(priestory.Single(p => p.Cislo == "1").Predpisy.Sum(x => x.RocnaSuma), b1.Predpisy.Sum(x => x.RocnaSuma));
        Assert.Equal(26.72m, b1.Roky.Single().PoistnePredpis);

        // druhý import ten istý súbor len aktualizuje
        var r2 = ImportExcel.Importuj(c, ciel, 2025, xlsx);
        Assert.Equal(0, r2.Pridane);
        Assert.Equal(12, r2.Aktualizovane);
        Assert.Equal(12, c.Priestory.Count(p => p.JednotkaId == ciel.Id));
    }
}

public class EmailAZalohaTests
{
    [Fact]
    public async Task Odosle_email_s_prilohou_cez_smtp()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var prijate = new StringBuilder();
        var server = Task.Run(async () =>
        {
            using var klient = await listener.AcceptTcpClientAsync();
            using var s = klient.GetStream();
            var r = new StreamReader(s);
            var w = new StreamWriter(s) { AutoFlush = true, NewLine = "\r\n" };
            await w.WriteLineAsync("220 test");
            bool data = false;
            while (await r.ReadLineAsync() is { } l)
            {
                if (data) { if (l == ".") { data = false; await w.WriteLineAsync("250 ok"); } else prijate.AppendLine(l); continue; }
                if (l.StartsWith("EHLO")) await w.WriteLineAsync("250 test");
                else if (l.StartsWith("DATA")) { data = true; await w.WriteLineAsync("354 go"); }
                else if (l.StartsWith("QUIT")) { await w.WriteLineAsync("221 bye"); break; }
                else await w.WriteLineAsync("250 ok");
            }
        });

        var n = new EmailNastavenia("127.0.0.1", port, false, "", "", "svb@example.sk", "SVB Test");
        var chyby = await Email.Odosli(n, new[] { new EmailSprava("vlastnik@example.sk", "Ján", "Vyúčtovanie 2025", "Dobrý deň", new[] { ("v.pdf", new byte[] { 1, 2, 3 }) }) });
        await server;
        listener.Stop();
        Assert.Empty(chyby);
        Assert.Contains("vlastnik@example.sk", prijate.ToString());
        Assert.Contains("v.pdf", prijate.ToString());
        Assert.True(Email.JePlatnaAdresa("a@b.sk; c@d.sk"));
        Assert.False(Email.JePlatnaAdresa("bez zavináča"));
    }

    [Fact]
    public void Automaticka_zaloha_drzi_len_poslednych_n()
    {
        var dir = Directory.CreateTempSubdirectory("az").FullName;
        var db = Path.Combine(dir, "a.db");
        Databaza.Priprav(db);
        var n = new ZalohaNastavenia(true, Path.Combine(dir, "z"), 3);
        for (int i = 0; i < 5; i++) AutomatickaZaloha.Vytvor(db, n, new DateTime(2026, 1, 1, 10, i, 0));
        var subory = Directory.GetFiles(n.Priecinok).Select(Path.GetFileName).OrderBy(x => x).ToList();
        Assert.Equal(3, subory.Count);
        Assert.EndsWith("1004.zip", subory[^1]);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Nastavenia_emailu_sa_ulozia_aj_s_heslom()
    {
        var dir = Directory.CreateTempSubdirectory("ns").FullName;
        var db = Path.Combine(dir, "a.db");
        Databaza.Priprav(db);
        using (var c = AppDbContext.Open(db))
            Nastavenia.UlozEmail(c, new EmailNastavenia("smtp.gmail.com", 465, true, "u", "tajné heslo", "a@b.sk", "SVB"));
        using (var c = AppDbContext.Open(db))
        {
            var e = Nastavenia.Email(c);
            Assert.Equal("tajné heslo", e.Heslo);
            Assert.True(e.Ssl);
            Assert.DoesNotContain("tajné", c.Nastavenia.Single(x => x.Kluc == "email.heslo").Hodnota);
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(dir, true);
    }
}
