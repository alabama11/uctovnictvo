using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Vymyslené spoločenstvo s 12 bytmi na vyskúšanie programu.</summary>
public static class Ukazka
{
    public static UctovnaJednotka Vytvor(AppDbContext db, int rok)
    {
        var j = new UctovnaJednotka
        {
            Typ = TypJednotky.Spolocenstvo,
            Nazov = "Ukážkové spoločenstvo Lipová 12",
            Ulica = "Lipová 12", Obec = "Banská Bystrica", Psc = "974 01",
            Iban = "SK3112000000198742637541", IbanFondOprav = "SK8975000000000012345671",
            Statutar = "Ján Ukážkový",
        };
        var kat = new (string Nazov, KlucRozpoctu Kluc, bool Fo, string? Len, decimal Naklad, decimal Zaloha)[]
        {
            ("správcovská činnosť", KlucRozpoctu.RovnakoNaPriestor, false, null, 3840.00m, 300m),
            ("upratovanie spol. častí domu, kosenie, sneh", KlucRozpoctu.Plocha, false, null, 1650.40m, 2.10m),
            ("el. energia spol. priestorov", KlucRozpoctu.Plocha, false, null, 312.75m, 0.45m),
            ("vodné a stočné", KlucRozpoctu.SpotrebaVody, false, null, 2480.30m, 180m),
            ("čistenie a revízia komínov", KlucRozpoctu.RovnakoNaPriestor, false, "7, 8", 96.00m, 0m),
            ("iné náklady", KlucRozpoctu.RovnakoNaPriestor, false, null, 214.80m, 12m),
            ("fond opráv", KlucRozpoctu.Plocha, true, null, 0m, 4.20m),
        };
        int poradie = 1;
        foreach (var k in kat)
        {
            var e = new KategoriaNakladov { Nazov = k.Nazov, Kluc = k.Kluc, JeFondOprav = k.Fo, LenPriestory = k.Len, Poradie = poradie++ };
            if (!k.Fo) e.Naklady.Add(new NakladRoka { Rok = rok, Suma = k.Naklad });
            j.Kategorie.Add(e);
        }
        db.Jednotky.Add(j);
        db.SaveChanges();

        string[] mena = { "Mária Kováčová", "Peter Horváth", "Jana Nagyová", "Martin Baláž", "Eva Szabóová", "Tomáš Molnár",
                          "Zuzana Varga", "Michal Tóth", "Lucia Lukáčová", "Juraj Polák", "Katarína Kráľová", "Andrej Novák" };
        decimal[] plochy = { 44.53m, 62.10m, 71.85m, 44.53m, 62.10m, 71.85m, 38.20m, 55.00m, 68.40m, 38.20m, 55.00m, 68.40m };
        decimal[] spotreby = { 39.285m, 52.1m, 61.4m, 22.7m, 48.9m, 70.2m, 18.3m, 41.0m, 55.6m, 25.4m, 33.8m, 66.1m };
        var nahoda = new Random(7);
        var spoluPlocha = plochy.Sum(p => (int)Math.Round(p * 100));
        for (int i = 0; i < 12; i++)
        {
            var p = new Priestor
            {
                JednotkaId = j.Id, Cislo = (i + 1).ToString(), VariabilnySymbol = (i + 1).ToString(),
                Poschodie = $"{i / 3 + 1}.", Plocha = plochy[i], PlochaBalkona = i % 3 == 0 ? 0 : 6.5m,
                PocetOsob = 1 + i % 4, PodielCitatel = (int)Math.Round(plochy[i] * 100), PodielMenovatel = spoluPlocha,
                CisloVodomeru = (69241600 + i * 7).ToString(),
            };
            var v = new Vlastnik { Meno = mena[i], Ulica = "Lipová 12", Obec = "Banská Bystrica", Psc = "974 01" };
            v.Ucty.Add(new BankovyUcet { Iban = UkazkovyIban(i) });
            p.Vlastnici.Add(v);
            var start = 100m + i * 13.7m;
            p.Roky.Add(new PriestorRok
            {
                Rok = rok, DatumOdpoctuZaciatok = new DateOnly(rok, 1, 10), StavVodomeruZaciatok = start,
                DatumOdpoctuKoniec = new DateOnly(rok + 1, 1, 10), StavVodomeruKoniec = start + spotreby[i],
                PoistnePredpis = Math.Round(plochy[i] * 0.6m, 2),
            });

            foreach (var (k, def) in j.Kategorie.Zip(kat))
            {
                var suma = k.Kluc switch
                {
                    _ when def.Zaloha == 0 => 0m,
                    KlucRozpoctu.Plocha => Math.Round(def.Zaloha * plochy[i], 2),
                    _ => def.Zaloha,
                };
                if (suma != 0) p.Predpisy.Add(new PredpisPolozky { KategoriaId = k.Id, Rok = rok, RocnaSuma = suma });
            }
            var mesacne = Math.Ceiling(p.Predpisy.Sum(x => x.RocnaSuma) / 12m);
            for (int m = 1; m <= 12; m++)
            {
                if (i == 4 && m > 10) continue; // byt 5 nezaplatil november a december
                p.Uhrady.Add(new Uhrada
                {
                    Datum = new DateOnly(rok, m, 10 + nahoda.Next(10)), Suma = mesacne,
                    IbanPlatitela = UkazkovyIban(i), VariabilnySymbol = p.VariabilnySymbol,
                });
            }
            p.Uhrady.Add(new Uhrada { Datum = new DateOnly(rok, 2, 20), Suma = p.Roky[0].PoistnePredpis, Ucel = UcelUhrady.Poistne, IbanPlatitela = UkazkovyIban(i) });
            db.Priestory.Add(p);
        }
        db.SaveChanges();
        return j;
    }

    /// <summary>Vymyslený, ale formálne platný slovenský IBAN.</summary>
    static string UkazkovyIban(int i)
    {
        var bban = "0900" + "000000" + (5100000000L + i * 1234567L).ToString("D10");
        for (int kontrola = 2; kontrola < 99; kontrola++)
        {
            var iban = $"SK{kontrola:D2}{bban}";
            if (Iban.JePlatny(iban)) return iban;
        }
        return "SK00" + bban;
    }
}
