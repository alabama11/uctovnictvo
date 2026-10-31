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

    /// <summary>Vymyslená s.r.o. s partnermi, faktúrami, bankou a pokladňou na vyskúšanie časti pre firmy.</summary>
    public static UctovnaJednotka VytvorFirmu(AppDbContext db, int rok)
    {
        var j = new UctovnaJednotka
        {
            Typ = TypJednotky.Sro, Nazov = "Ukážková firma s.r.o.", Ico = "12345678", Dic = "2020123456",
            Ulica = "Hlavná 5", Obec = "Žilina", Psc = "010 01", Iban = "SK3112000000198742637541",
            Statutar = "Eva Ukážková", Email = "firma@example.sk",
            ZapisVRegistri = "Zapísaná v Obchodnom registri Okresného súdu Žilina, oddiel Sro, vložka č. 99999/L",
            PokladnaPociatocnyStav = 150m,
        };
        db.Jednotky.Add(j);
        var odberatel = new Partner { Jednotka = j, Nazov = "Stavby Sever s.r.o.", Ico = "87654321", Ulica = "Priemyselná 12", Psc = "036 01", Obec = "Martin", Email = "uctaren@example.sk" };
        var dodavatel = new Partner { Jednotka = j, Nazov = "Kancel servis a.s.", Ico = "11223344", Ulica = "Obchodná 3", Psc = "811 06", Obec = "Bratislava", Iban = UkazkovyIban(77) };
        db.Partneri.AddRange(odberatel, dodavatel);

        Faktura Vydana(string cislo, int mesiac, Partner p, params (string Popis, decimal Mn, string Mj, decimal Cena)[] polozky)
        {
            var f = new Faktura
            {
                Jednotka = j, Druh = DruhFaktury.Vydana, Cislo = cislo, VariabilnySymbol = cislo,
                DatumVystavenia = new DateOnly(rok, mesiac, 3), DatumDodania = new DateOnly(rok, mesiac, 3), DatumSplatnosti = new DateOnly(rok, mesiac, 17),
            };
            Faktury.NastavPartnera(f, p);
            var i = 1;
            foreach (var x in polozky) f.Polozky.Add(new PolozkaFaktury { Poradie = i++, Popis = x.Popis, Mnozstvo = x.Mn, Jednotka = x.Mj, CenaZaJednotku = x.Cena });
            db.Faktury.Add(f);
            return f;
        }
        var f1 = Vydana($"{rok}0001", 2, odberatel, ("Projektová dokumentácia – rodinný dom", 1, "ks", 1200m), ("Konzultácie", 6, "hod", 35m));
        Vydana($"{rok}0002", 9, odberatel, ("Stavebný dozor", 12, "hod", 40m));
        var p1 = new Faktura
        {
            Jednotka = j, Druh = DruhFaktury.Prijata, Cislo = $"PF{rok}0001", CisloDodavatela = "2024-118", VariabilnySymbol = "2024118",
            DatumVystavenia = new DateOnly(rok, 3, 10), DatumDodania = new DateOnly(rok, 3, 10), DatumSplatnosti = new DateOnly(rok, 3, 24),
        };
        Faktury.NastavPartnera(p1, dodavatel);
        p1.Polozky.Add(new PolozkaFaktury { Poradie = 1, Popis = "Kancelárske potreby", Mnozstvo = 1, CenaZaJednotku = 86.10m });
        db.Faktury.Add(p1);

        var b1 = new BankovyPohyb { Jednotka = j, Datum = new DateOnly(rok, 2, 15), Suma = f1.Suma, ProtiNazov = odberatel.Nazov, VariabilnySymbol = f1.Cislo, Referencia = "ukazka-1", Popis = "Uhrada faktury" };
        var b2 = new BankovyPohyb { Jednotka = j, Datum = new DateOnly(rok, 3, 31), Suma = -5.90m, Popis = "Poplatok za vedenie účtu", Referencia = "ukazka-2" };
        db.BankovePohyby.AddRange(b1, b2);
        Banka.Uhrad(f1, b1);
        var d = Pokladna.ZFaktury(p1, new DateOnly(rok, 3, 12), Array.Empty<string>());
        d.Jednotka = j;
        db.PokladnicneDoklady.Add(d);
        db.PokladnicneDoklady.Add(new PokladnicnyDoklad { Jednotka = j, Druh = DruhPokladnicnehoDokladu.Vydavok, Cislo = $"VPD{rok}002", Datum = new DateOnly(rok, 4, 2), Suma = 12.50m, Ucel = "Poštovné", Osoba = "Slovenská pošta" });
        var p2 = new Faktura
        {
            Jednotka = j, Druh = DruhFaktury.Prijata, Cislo = $"PF{rok}0002", CisloDodavatela = "FA-3391", VariabilnySymbol = "3391", Ucet = "042",
            DatumVystavenia = new DateOnly(rok, 3, 5), DatumDodania = new DateOnly(rok, 3, 5), DatumSplatnosti = new DateOnly(rok, 3, 19),
        };
        Faktury.NastavPartnera(p2, dodavatel);
        p2.Polozky.Add(new PolozkaFaktury { Poradie = 1, Popis = "Notebook a monitor", Mnozstvo = 1, CenaZaJednotku = 2150m });
        db.Faktury.Add(p2);
        var b3 = new BankovyPohyb { Jednotka = j, Datum = new DateOnly(rok, 3, 8), Suma = -2150m, ProtiNazov = dodavatel.Nazov, ProtiIban = dodavatel.Iban, VariabilnySymbol = "3391", Referencia = "ukazka-3", Popis = "FA-3391" };
        db.BankovePohyby.Add(b3);
        Banka.Uhrad(p2, b3);
        db.Majetok.Add(new Majetok { Jednotka = j, InventarneCislo = $"M{rok}001", Nazov = "Notebook a monitor", DatumObstarania = new DateOnly(rok, 3, 5), DatumZaradenia = new DateOnly(rok, 3, 5), VstupnaCena = 2150m, OdpisovaSkupina = 1 });
        var auto = new Vozidlo { Jednotka = j, Nazov = "Škoda Octavia", Ecv = "ZA123AB", Spotreba = 5.6m, Palivo = "nafta", CenaPaliva = 1.52m, StavTachometraZaciatok = 84210 };
        auto.Jazdy.Add(new Jazda { Datum = new DateOnly(rok, 9, 4), Odkial = "Žilina", Kam = "Martin", Ucel = "Stavebný dozor – Stavby Sever", Km = 34, Vodic = "Eva Ukážková" });
        auto.Jazdy.Add(new Jazda { Datum = new DateOnly(rok, 9, 4), Odkial = "Martin", Kam = "Žilina", Ucel = "Stavebný dozor – Stavby Sever", Km = 34, Vodic = "Eva Ukážková" });
        auto.Jazdy.Add(new Jazda { Datum = new DateOnly(rok, 9, 6), Odkial = "Žilina", Kam = "Terchová", Ucel = "", Km = 26, Sukromna = true, Vodic = "Eva Ukážková" });
        db.Vozidla.Add(auto);
        // počiatočné stavy podvojného účtovníctva: banka 5 000 €, pokladnica 150 €, základné imanie 5 000 €, nerozdelený zisk 150 €
        var ps = new DateOnly(rok, 1, 1);
        foreach (var (md, dal, suma) in new[] { ("221", "701", 5000m), ("211", "701", 150m), ("701", "411", 5000m), ("701", "428", 150m) })
            db.UctovneZapisy.Add(new UctovnyZapis { Jednotka = j, Datum = ps, Doklad = "PS", Popis = "Počiatočný stav", Md = md, D = dal, Suma = suma, Zdroj = ZdrojZapisu.PociatocnyStav });
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
