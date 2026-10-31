using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public record ObratUctu(string Ucet, string Nazov, decimal PsMd, decimal PsD, decimal ObratMd, decimal ObratD)
{
    public decimal Ps => PsMd - PsD;
    public decimal KsMd => PsMd + ObratMd;
    public decimal KsD => PsD + ObratD;
    /// <summary>Konečný zostatok (MD − D).</summary>
    public decimal Zostatok => KsMd - KsD;
}

public record SaldoRiadok(string Partner, string Doklad, DruhFaktury Druh, DateOnly Splatnost, decimal Suma, decimal Zostatok, int DniPoSplatnosti);

public record RiadokVykazu(string Oznacenie, string Text, int Riadok, decimal Bezne, decimal Minule, bool Sucet = false);

/// <summary>
/// Podvojné účtovníctvo podnikateľa (s.r.o.): účtový rozvrh podľa rámcovej účtovej osnovy, predkontácie dokladov,
/// hlavná kniha, obratová predvaha, otvorenie roka a účtovná závierka mikro účtovnej jednotky.
/// </summary>
public static class Podvojne
{
    /// <summary>Účtový rozvrh malej s.r.o. – vybrané účty rámcovej účtovej osnovy pre podnikateľov.</summary>
    public static List<Ucet> PredvolenyRozvrh(int jednotkaId)
    {
        var a = TypUctu.Aktivny; var p = TypUctu.Pasivny; var n = TypUctu.Nakladovy; var v = TypUctu.Vynosovy; var u = TypUctu.Uzavierkovy;
        var zoznam = new (string C, string N, TypUctu T, bool D)[]
        {
            ("013", "Softvér", a, true), ("022", "Samostatné hnuteľné veci a súbory hnuteľných vecí", a, true),
            ("042", "Obstaranie dlhodobého hmotného majetku", a, true), ("073", "Oprávky k softvéru", p, true),
            ("082", "Oprávky k samostatným hnuteľným veciam", p, true),
            ("112", "Materiál na sklade", a, true), ("132", "Tovar na sklade", a, true),
            ("211", "Pokladnica", a, true), ("221", "Bankové účty", a, true), ("261", "Peniaze na ceste", a, true),
            ("311", "Odberatelia", a, true), ("314", "Poskytnuté preddavky", a, true), ("321", "Dodávatelia", p, true),
            ("324", "Prijaté preddavky", p, true), ("331", "Zamestnanci", p, true), ("333", "Ostatné záväzky voči zamestnancom", p, true),
            ("335", "Pohľadávky voči zamestnancom", a, true), ("336", "Zúčtovanie s orgánmi sociálneho a zdravotného poistenia", p, true),
            ("341", "Daň z príjmov", p, true), ("342", "Ostatné priame dane", p, true), ("343", "Daň z pridanej hodnoty", p, true),
            ("345", "Ostatné dane a poplatky", p, true), ("353", "Pohľadávky za upísané vlastné imanie", a, true),
            ("355", "Ostatné pohľadávky voči spoločníkom", a, true), ("365", "Ostatné záväzky voči spoločníkom", p, true),
            ("378", "Iné pohľadávky", a, true), ("379", "Iné záväzky", p, true), ("381", "Náklady budúcich období", a, true),
            ("383", "Výdavky budúcich období", p, true), ("384", "Výnosy budúcich období", p, true), ("395", "Zúčtovací vzťah", a, true),
            ("411", "Základné imanie", p, true), ("417", "Zákonný rezervný fond", p, true), ("428", "Nerozdelený zisk minulých rokov", p, true),
            ("429", "Neuhradená strata minulých rokov", p, true), ("431", "Výsledok hospodárenia v schvaľovaní", p, true),
            ("451", "Rezervy zákonné", p, true), ("461", "Bankové úvery", p, true), ("479", "Ostatné dlhodobé záväzky", p, true),
            ("501", "Spotreba materiálu", n, true), ("502", "Spotreba energie", n, true), ("504", "Predaný tovar", n, true),
            ("511", "Opravy a udržiavanie", n, true), ("512", "Cestovné", n, true), ("513", "Náklady na reprezentáciu", n, false),
            ("518", "Ostatné služby", n, true), ("521", "Mzdové náklady", n, true), ("524", "Zákonné sociálne poistenie", n, true),
            ("527", "Zákonné sociálne náklady", n, true), ("531", "Daň z motorových vozidiel", n, true), ("538", "Ostatné dane a poplatky", n, true),
            ("541", "Zostatková cena predaného dlhodobého majetku", n, true), ("544", "Zmluvné pokuty, penále a úroky z omeškania", n, false),
            ("545", "Ostatné pokuty, penále a úroky z omeškania", n, false), ("548", "Ostatné náklady na hospodársku činnosť", n, true),
            ("548.900", "Ostatné náklady – nedaňové", n, false), ("551", "Odpisy dlhodobého nehmotného a hmotného majetku", n, true),
            ("562", "Úroky", n, true), ("568", "Ostatné finančné náklady", n, true), ("591", "Splatná daň z príjmov", n, false),
            ("601", "Tržby za vlastné výrobky", v, true), ("602", "Tržby z predaja služieb", v, true), ("604", "Tržby za tovar", v, true),
            ("641", "Tržby z predaja dlhodobého majetku", v, true), ("648", "Ostatné výnosy z hospodárskej činnosti", v, true),
            ("662", "Úroky", v, true), ("668", "Ostatné finančné výnosy", v, true),
            ("701", "Začiatočný účet súvahový", u, true), ("702", "Konečný účet súvahový", u, true), ("710", "Účet ziskov a strát", u, true),
        };
        return zoznam.Select(x => new Ucet { JednotkaId = jednotkaId, Cislo = x.C, Nazov = x.N, Typ = x.T, Danovy = x.D }).ToList();
    }

    /// <summary>Účet pre kategóriu peňažného denníka (ak nie je nastavený ručne).</summary>
    public static string UcetKategorie(KategoriaPohybu? k) => k?.Ucet is { Length: > 0 } u ? u : k?.Kod switch
    {
        "trzby" or "trzby-hotovost" => "602",
        "ine-prijmy" => "648",
        "material" => "501",
        "sluzby" or "najom" => "518",
        "energie" => "502",
        "auto" => "501",
        "poplatky" => "568",
        "poistne" => "336",
        "mzdy" => "331",
        "nedanove" => "548.900",
        "dan" => "341",
        "vklad" => "365",
        "prevod" => "261",
        "uver" => "461",
        "majetok" => "042",
        _ => k?.Druh == DruhKategorie.Prijem ? "648" : "548",
    };

    static string Doklad(Faktura f) => f.Druh == DruhFaktury.Prijata && !string.IsNullOrWhiteSpace(f.CisloDodavatela) ? $"{f.Cislo} ({f.CisloDodavatela})" : f.Cislo;

    /// <summary>
    /// Predkontácie dokladov roka: faktúry (311/6xx, 5xx/321; zálohová faktúra sa neúčtuje, jej úhrada ide na 324/314),
    /// bankové pohyby (221) a pokladničné doklady (211) – úhrady faktúr proti 311/321/324/314, ostatné podľa kategórie –
    /// a majetok: zaradenie (022/042), daňové odpisy (551/082) a vyradenie (082/022, 541/022).
    /// </summary>
    public static List<UctovnyZapis> Zauctuj(int jednotkaId, int rok, IEnumerable<Faktura> faktury, IEnumerable<BankovyPohyb> pohyby,
        IEnumerable<PokladnicnyDoklad> doklady, IEnumerable<Majetok> majetok)
    {
        var z = new List<UctovnyZapis>();
        void Pridaj(DateOnly d, string doklad, string popis, string md, string dal, decimal suma, ZdrojZapisu zdroj, Faktura? f = null, string? partner = null)
        {
            if (suma == 0) return;
            if (suma < 0) { (md, dal) = (dal, md); suma = -suma; }
            z.Add(new UctovnyZapis { JednotkaId = jednotkaId, Datum = d, Doklad = doklad, Popis = popis, Md = md, D = dal, Suma = suma, Zdroj = zdroj, FakturaId = f?.Id, Partner = partner ?? f?.PartnerNazov });
        }
        bool JeOdpocetZalohy(PolozkaFaktury p) => p.Popis.StartsWith("Odpočet uhradenej zálohy", StringComparison.Ordinal);

        foreach (var f in faktury.Where(f => (f.DatumDodania ?? f.DatumVystavenia).Year == rok && f.Typ != TypFaktury.Zalohova))
        {
            var d = f.DatumDodania ?? f.DatumVystavenia;
            var ucet = f.Ucet is { Length: > 0 } u ? u : f.KategoriaPohybu is { } k ? UcetKategorie(k) : f.Druh == DruhFaktury.Vydana ? "602" : "518";
            var zaloha = f.Polozky.Where(JeOdpocetZalohy).Sum(p => p.Suma);
            var zaklad = f.Suma - zaloha;
            var popis = $"{Faktury.NazovTypu(f.Typ)} {f.PartnerNazov}";
            if (f.Druh == DruhFaktury.Vydana)
            {
                Pridaj(d, f.Cislo, popis, "311", ucet, zaklad, ZdrojZapisu.Faktura, f);
                Pridaj(d, f.Cislo, "Zúčtovanie prijatého preddavku", "324", "311", -zaloha, ZdrojZapisu.Faktura, f);
            }
            else
            {
                Pridaj(d, Doklad(f), popis, ucet, "321", zaklad, ZdrojZapisu.Faktura, f);
                Pridaj(d, Doklad(f), "Zúčtovanie poskytnutého preddavku", "321", "314", -zaloha, ZdrojZapisu.Faktura, f);
            }
        }

        void Platba(DateOnly d, string doklad, string popis, string penazny, decimal suma, IEnumerable<UhradaFaktury> uhrady, KategoriaPohybu? k, ZdrojZapisu zdroj, string? partner)
        {
            var zvysok = suma;
            foreach (var u in uhrady.Where(u => u.Faktura is not null))
            {
                var f = u.Faktura!;
                var protiucet = (f.Druh, f.Typ) switch
                {
                    (DruhFaktury.Vydana, TypFaktury.Zalohova) => "324",
                    (DruhFaktury.Vydana, _) => "311",
                    (DruhFaktury.Prijata, TypFaktury.Zalohova) => "314",
                    _ => "321",
                };
                var s = f.Druh == DruhFaktury.Vydana ? u.Suma : -u.Suma;
                Pridaj(d, doklad, $"Úhrada {f.Cislo} {f.PartnerNazov}", penazny, protiucet, s, zdroj, f);
                zvysok -= s;
            }
            if (zvysok == 0) return;
            var ucet = k is null ? "395" : UcetKategorie(k);
            Pridaj(d, doklad, k is null ? $"{popis} (nezaradené – zúčtovací vzťah)" : popis, penazny, ucet, zvysok, zdroj, partner: partner);
        }

        foreach (var p in pohyby.Where(p => p.Datum.Year == rok))
            Platba(p.Datum, $"BV {p.Datum:dd.MM.}", string.Join(" – ", new[] { p.ProtiNazov, p.Popis }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } t ? t : "Bankový pohyb",
                "221", p.Suma, p.Uhrady, p.Kategoria, ZdrojZapisu.Banka, p.ProtiNazov);
        foreach (var d in doklady.Where(d => d.Datum.Year == rok))
            Platba(d.Datum, d.Cislo, d.Ucel, "211", d.Druh == DruhPokladnicnehoDokladu.Prijem ? d.Suma : -d.Suma, d.Uhrady, d.Kategoria, ZdrojZapisu.Pokladna, d.Osoba);

        foreach (var m in majetok)
        {
            // zaradenie do používania z obstarania (042); nákup účtujte na 042 (kategória „Nákup dlhodobého majetku“ alebo účet na faktúre)
            if (m.DatumZaradenia.Year == rok)
                Pridaj(m.DatumZaradenia, m.InventarneCislo, $"Zaradenie {m.Nazov}", "022", "042", m.VstupnaCena, ZdrojZapisu.Odpisy);
            var koniec = m.DatumVyradenia is { } v && v.Year == rok ? v : new DateOnly(rok, 12, 31);
            if (Odpisy.ZaRok(m, rok) is { } o)
                Pridaj(koniec, m.InventarneCislo, $"Odpis {m.Nazov}", "551", "082", o.Odpis, ZdrojZapisu.Odpisy);
            if (m.DatumVyradenia is { } vyr && vyr.Year == rok)
            {
                var zc = Odpisy.ZostatkovaCena(m, rok);
                Pridaj(vyr, m.InventarneCislo, $"Vyradenie {m.Nazov} – oprávky", "082", "022", m.VstupnaCena - zc, ZdrojZapisu.Odpisy);
                Pridaj(vyr, m.InventarneCislo, $"Vyradenie {m.Nazov} – zostatková cena", "541", "022", zc, ZdrojZapisu.Odpisy);
            }
        }

        return z.OrderBy(x => x.Datum).ThenBy(x => x.Doklad, StringComparer.Ordinal).ToList();
    }

    static bool Pociatok(UctovnyZapis z) => z.Zdroj == ZdrojZapisu.PociatocnyStav;

    /// <summary>Obratová predvaha: počiatočné stavy (zápisy PS) a obraty roka po účtoch, vrátane syntetických súčtov.</summary>
    public static List<ObratUctu> Predvaha(IEnumerable<UctovnyZapis> zapisy, IReadOnlyList<Ucet> rozvrh, int rok)
    {
        var roka = zapisy.Where(z => z.Datum.Year == rok).ToList();
        var ucty = roka.SelectMany(z => new[] { z.Md, z.D }).Where(c => c.Length > 0 && !c.StartsWith("70")).Distinct().OrderBy(c => c, StringComparer.Ordinal);
        return ucty.Select(c => new ObratUctu(c, rozvrh.FirstOrDefault(u => u.Cislo == c)?.Nazov ?? rozvrh.FirstOrDefault(u => u.Cislo == Syn(c))?.Nazov ?? "",
            roka.Where(z => Pociatok(z) && z.Md == c).Sum(z => z.Suma), roka.Where(z => Pociatok(z) && z.D == c).Sum(z => z.Suma),
            roka.Where(z => !Pociatok(z) && z.Md == c).Sum(z => z.Suma), roka.Where(z => !Pociatok(z) && z.D == c).Sum(z => z.Suma))).ToList();
    }

    static string Syn(string ucet) => ucet.Length >= 3 ? ucet[..3] : ucet;

    /// <summary>Výsledok hospodárenia roka: výnosy (trieda 6) − náklady (trieda 5).</summary>
    public static decimal VysledokHospodarenia(IReadOnlyList<ObratUctu> predvaha) =>
        -predvaha.Where(o => o.Ucet.StartsWith('6')).Sum(o => o.ObratMd - o.ObratD) - predvaha.Where(o => o.Ucet.StartsWith('5')).Sum(o => o.ObratMd - o.ObratD);

    /// <summary>
    /// Počiatočné stavy nového roka z konečných zostatkov súvahových účtov (triedy 0–4). Výsledok hospodárenia
    /// uplynulého roka sa prenesie na 431 (výsledok hospodárenia v schvaľovaní), odkiaľ ho po schválení rozdelíte ručne.
    /// </summary>
    public static List<UctovnyZapis> PociatocneStavy(int jednotkaId, IReadOnlyList<ObratUctu> predvahaMinuleho, int novyRok)
    {
        var d = new DateOnly(novyRok, 1, 1);
        var z = new List<UctovnyZapis>();
        foreach (var o in predvahaMinuleho.Where(o => o.Ucet[0] is >= '0' and <= '4' && o.Zostatok != 0))
        {
            var (md, dal) = o.Zostatok > 0 ? (o.Ucet, "701") : ("701", o.Ucet);
            z.Add(new UctovnyZapis { JednotkaId = jednotkaId, Datum = d, Doklad = "PS", Popis = "Počiatočný stav", Md = md, D = dal, Suma = Math.Abs(o.Zostatok), Zdroj = ZdrojZapisu.PociatocnyStav });
        }
        var vh = VysledokHospodarenia(predvahaMinuleho);
        if (vh != 0)
            z.Add(new UctovnyZapis { JednotkaId = jednotkaId, Datum = d, Doklad = "PS", Popis = $"Výsledok hospodárenia {novyRok - 1} v schvaľovaní",
                Md = vh > 0 ? "701" : "431", D = vh > 0 ? "431" : "701", Suma = Math.Abs(vh), Zdroj = ZdrojZapisu.PociatocnyStav });
        return z;
    }

    // ---------- účtovná závierka mikro účtovnej jednotky ----------

    /// <summary>Riadky súvahy (aktíva 01–23, pasíva 24–45) podľa vzoru Úč MÚJ 1-01 (eFormulár UZMUJ Finančnej správy).</summary>
    static readonly (int R, string Oz, string Text)[] RiadkySuvahy =
    {
        (1, "", "SPOLU MAJETOK r. 02 + r. 14"),
        (2, "A.", "Neobežný majetok r. 03 + r. 04 + r. 09"),
        (3, "A.I.", "Dlhodobý nehmotný majetok"),
        (4, "A.II.", "Dlhodobý hmotný majetok r. 05 až r. 08"),
        (5, "1.", "Pozemky a stavby"),
        (6, "2.", "Samostatné hnuteľné veci a súbory hnuteľných vecí"),
        (7, "3.", "Ostatný dlhodobý hmotný majetok"),
        (8, "4.", "Opravná položka k nadobudnutému majetku"),
        (9, "A.III.", "Dlhodobý finančný majetok r. 10 až r. 13"),
        (10, "1.", "Podielové cenné papiere a podiely"),
        (11, "2.", "Ostatný dlhodobý finančný majetok"),
        (12, "3.", "Účty v bankách s dobou viazanosti dlhšou ako jeden rok"),
        (13, "4.", "Ostatný finančný majetok so zostatkovou dobou splatnosti do jedného roka"),
        (14, "B.", "Obežný majetok r. 15 + r. 16 + r. 17 + r. 21"),
        (15, "B.I.", "Zásoby"),
        (16, "B.II.", "Dlhodobé pohľadávky"),
        (17, "B.III.", "Krátkodobé pohľadávky r. 18 až r. 20"),
        (18, "1.", "Pohľadávky z obchodného styku"),
        (19, "2.", "Pohľadávky zo sociálneho poistenia, daňové pohľadávky a dotácie"),
        (20, "3.", "Iné pohľadávky"),
        (21, "B.IV.", "Finančné účty r. 22 + r. 23"),
        (22, "1.", "Peniaze a účty v bankách"),
        (23, "2.", "Ostatné finančné účty"),
        (24, "", "SPOLU VLASTNÉ IMANIE A ZÁVÄZKY r. 25 + r. 34"),
        (25, "A.", "Vlastné imanie r. 26 + r. 29 + r. 30 + r. 31 + r. 32 + r. 33"),
        (26, "A.I.", "Základné imanie r. 27 + r. 28"),
        (27, "1.", "Základné imanie a zmena základného imania"),
        (28, "2.", "Pohľadávky za upísané vlastné imanie"),
        (29, "A.II.", "Kapitálové fondy"),
        (30, "A.III.", "Fondy zo zisku"),
        (31, "A.IV.", "Oceňovacie rozdiely z precenenia"),
        (32, "A.V.", "Výsledok hospodárenia minulých rokov"),
        (33, "A.VI.", "Výsledok hospodárenia za účtovné obdobie po zdanení"),
        (34, "B.", "Záväzky r. 35 + r. 36 + r. 37 + r. 38 + r. 43 + r. 44 + r. 45"),
        (35, "B.I.", "Dlhodobé záväzky okrem rezerv, úverov a výpomocí"),
        (36, "B.II.", "Dlhodobé rezervy"),
        (37, "B.III.", "Dlhodobé bankové úvery"),
        (38, "B.IV.", "Krátkodobé záväzky okrem rezerv, úverov a výpomocí r. 39 až r. 42"),
        (39, "1.", "Záväzky z obchodného styku"),
        (40, "2.", "Záväzky voči zamestnancom a zo sociálneho poistenia"),
        (41, "3.", "Daňové záväzky a dotácie"),
        (42, "4.", "Ostatné záväzky"),
        (43, "B.V.", "Krátkodobé rezervy"),
        (44, "B.VI.", "Bežné bankové úvery"),
        (45, "B.VII.", "Krátkodobé finančné výpomoci"),
    };

    static readonly (int R, string Oz, string Text)[] RiadkyVzas =
    {
        (1, "", "Výnosy z hospodárskej činnosti spolu r. 02 až r. 07"),
        (2, "I.", "Tržby z predaja tovaru"),
        (3, "II.", "Tržby z predaja vlastných výrobkov a služieb"),
        (4, "III.", "Zmeny stavu vnútroorganizačných zásob (+/-)"),
        (5, "IV.", "Aktivácia"),
        (6, "V.", "Tržby z predaja dlhodobého nehmotného majetku, dlhodobého hmotného majetku a materiálu"),
        (7, "VI.", "Ostatné výnosy z hospodárskej činnosti"),
        (8, "", "Náklady na hospodársku činnosť spolu r. 09 až r. 17"),
        (9, "A.", "Náklady vynaložené na obstaranie predaného tovaru"),
        (10, "B.", "Spotreba materiálu, energie a ostatných neskladovateľných dodávok"),
        (11, "C.", "Služby"),
        (12, "D.", "Osobné náklady"),
        (13, "E.", "Dane a poplatky"),
        (14, "F.", "Odpisy a opravné položky k dlhodobému nehmotnému majetku a dlhodobému hmotnému majetku"),
        (15, "G.", "Zostatková cena predaného dlhodobého majetku a predaného materiálu"),
        (16, "H.", "Opravné položky k pohľadávkam"),
        (17, "I.", "Ostatné náklady na hospodársku činnosť"),
        (18, "", "Výsledok hospodárenia z hospodárskej činnosti (+/-) r. 01 – r. 08"),
        (19, "", "Pridaná hodnota (r. 02 + r. 03 + r. 04 + r. 05) – (r. 09 + r. 10 + r. 11)"),
        (20, "", "Výnosy z finančnej činnosti spolu r. 21 až r. 26"),
        (21, "VII.", "Tržby z predaja cenných papierov a podielov"),
        (22, "VIII.", "Výnosy z dlhodobého finančného majetku"),
        (23, "IX.", "Výnosy z krátkodobého finančného majetku"),
        (24, "X.", "Výnosové úroky"),
        (25, "XI.", "Kurzové zisky"),
        (26, "XII.", "Ostatné výnosy z finančnej činnosti"),
        (27, "", "Náklady na finančnú činnosť spolu r. 28 až r. 33"),
        (28, "J.", "Predané cenné papiere a podiely"),
        (29, "K.", "Náklady na krátkodobý finančný majetok"),
        (30, "L.", "Opravné položky k finančnému majetku (+/-)"),
        (31, "M.", "Nákladové úroky"),
        (32, "N.", "Kurzové straty"),
        (33, "O.", "Ostatné náklady na finančnú činnosť"),
        (34, "", "Výsledok hospodárenia z finančnej činnosti (+/-) r. 20 – r. 27"),
        (35, "", "Výsledok hospodárenia za účtovné obdobie pred zdanením (+/-) r. 18 + r. 34"),
        (36, "P.", "Daň z príjmov"),
        (37, "Q.", "Prevod podielov na výsledku hospodárenia spoločníkom (+/-)"),
        (38, "", "Výsledok hospodárenia za účtovné obdobie po zdanení (+/-) r. 35 – r. 36 – r. 37"),
    };

    static bool Je(string ucet, params string[] predpony) => predpony.Any(p => ucet.StartsWith(p, StringComparison.Ordinal));

    /// <summary>Riadok súvahy pre účet podľa znamienka zostatku (pre účty, ktoré môžu byť aktívom aj pasívom); 0 = nezaradiť.</summary>
    public static int RiadokSuvahy(string ucet, decimal zostatok)
    {
        var s = Syn(ucet);
        var aktivum = zostatok > 0;
        if (s[0] == '0')
        {
            if (Je(s, "01", "041", "051", "07", "091", "093")) return 3;
            if (Je(s, "021", "031", "081")) return 5;
            if (Je(s, "022", "023", "024", "027", "028", "082", "083", "084", "087", "088")) return 6;
            if (Je(s, "025", "026", "029", "032", "042", "052", "085", "086", "089", "092", "094", "095")) return 7;
            if (Je(s, "097", "098")) return 8;
            if (Je(s, "061", "062", "063")) return 10;
            return 11; // 043, 053, 064–069, 096
        }
        if (s[0] == '1') return 15;
        if (s[0] == '2')
        {
            if (Je(s, "211", "213", "21", "261")) return 22;
            if (s == "221") return aktivum ? 22 : 44;
            if (Je(s, "23")) return 44;
            if (Je(s, "24")) return 45;
            if (s == "255") return aktivum ? 23 : 45;
            return 23; // 251–259, 291
        }
        if (s[0] == '3')
        {
            if (s == "323") return 43;
            if (s == "353") return 28;
            if (s == "391") return 18;
            if (Je(s, "31")) return aktivum ? 18 : 39;
            if (Je(s, "32")) return aktivum ? 18 : 39;
            if (s is "331" or "333") return aktivum ? 20 : 40;
            if (s == "336") return aktivum ? 19 : 40;
            if (Je(s, "34")) return aktivum ? 19 : 41;
            if (s is "395" or "398") return aktivum ? 20 : 42;
            return aktivum ? 20 : 42;
        }
        if (s[0] == '4')
        {
            if (s is "411" or "419" or "491") return 27;
            if (s is "412" or "413" or "417" or "418") return 29;
            if (Je(s, "42") && s is not "428" and not "429") return 30;
            if (s is "414" or "415" or "416") return 31;
            if (s is "428" or "429") return 32;
            if (s == "431") return 33;
            if (Je(s, "45")) return 36;
            if (Je(s, "46")) return 37;
            if (Je(s, "47")) return 35;
            return 42;
        }
        return 0;
    }

    public static int RiadokVzas(string ucet)
    {
        var s = Syn(ucet);
        if (s is "604" or "607") return 2;
        if (s is "601" or "602" or "606") return 3;
        if (Je(s, "61")) return 4;
        if (Je(s, "62")) return 5;
        if (s is "641" or "642") return 6;
        if (Je(s, "64", "65")) return 7;
        if (s == "661") return 21;
        if (s == "665") return 22;
        if (s == "666") return 23;
        if (s == "662") return 24;
        if (s == "663") return 25;
        if (Je(s, "66", "67", "68", "69")) return 26;
        if (Je(s, "60", "63")) return 3;
        if (s is "504" or "505" or "507") return 9;
        if (Je(s, "50")) return 10;
        if (Je(s, "51")) return 11;
        if (Je(s, "52")) return 12;
        if (Je(s, "53")) return 13;
        if (s is "551" or "553") return 14;
        if (s is "541" or "542") return 15;
        if (s == "547") return 16;
        if (Je(s, "54", "55")) return 17;
        if (s == "561") return 28;
        if (s == "566") return 29;
        if (s == "565") return 30;
        if (s == "562") return 31;
        if (s == "563") return 32;
        if (Je(s, "56", "57", "58")) return 33;
        if (s is "591" or "595" or "592" or "594") return 36;
        if (s == "596") return 37;
        return 0;
    }

    static Dictionary<int, decimal> HodnotySuvahy(IReadOnlyList<ObratUctu> predvaha)
    {
        var h = new Dictionary<int, decimal>();
        void Add(int r, decimal v) => h[r] = h.GetValueOrDefault(r) + v;
        foreach (var o in predvaha.Where(o => o.Ucet[0] is >= '0' and <= '4'))
        {
            var r = RiadokSuvahy(o.Ucet, o.Zostatok);
            if (r == 0) continue;
            Add(r, r <= 23 ? o.Zostatok : -o.Zostatok);
        }
        Add(33, VysledokHospodarenia(predvaha));
        void Sum(int r, params int[] z) => h[r] = z.Sum(x => h.GetValueOrDefault(x));
        Sum(4, 5, 6, 7, 8); Sum(9, 10, 11, 12, 13); Sum(2, 3, 4, 9);
        Sum(17, 18, 19, 20); Sum(21, 22, 23); Sum(14, 15, 16, 17, 21); Sum(1, 2, 14);
        Sum(26, 27, 28); Sum(25, 26, 29, 30, 31, 32, 33);
        Sum(38, 39, 40, 41, 42); Sum(34, 35, 36, 37, 38, 43, 44, 45); Sum(24, 25, 34);
        return h;
    }

    static Dictionary<int, decimal> HodnotyVzas(IReadOnlyList<ObratUctu> predvaha)
    {
        var h = new Dictionary<int, decimal>();
        foreach (var o in predvaha.Where(o => o.Ucet[0] is '5' or '6'))
        {
            var r = RiadokVzas(o.Ucet);
            if (r == 0) continue;
            var v = o.Ucet[0] == '6' ? o.ObratD - o.ObratMd : o.ObratMd - o.ObratD;
            h[r] = h.GetValueOrDefault(r) + v;
        }
        decimal G(int r) => h.GetValueOrDefault(r);
        h[1] = Enumerable.Range(2, 6).Sum(G);
        h[8] = Enumerable.Range(9, 9).Sum(G);
        h[18] = G(1) - G(8);
        h[19] = G(2) + G(3) + G(4) + G(5) - (G(9) + G(10) + G(11));
        h[20] = Enumerable.Range(21, 6).Sum(G);
        h[27] = Enumerable.Range(28, 6).Sum(G);
        h[34] = G(20) - G(27);
        h[35] = G(18) + G(34);
        h[38] = G(35) - G(36) - G(37);
        return h;
    }

    static readonly HashSet<int> SuctoveSuvaha = new() { 1, 2, 4, 9, 14, 17, 21, 24, 25, 26, 34, 38 };
    static readonly HashSet<int> SuctoveVzas = new() { 1, 8, 18, 19, 20, 27, 34, 35, 38 };

    public static List<RiadokVykazu> Suvaha(IReadOnlyList<ObratUctu> bezne, IReadOnlyList<ObratUctu>? minule)
    {
        var b = HodnotySuvahy(bezne);
        var m = minule is null ? new() : HodnotySuvahy(minule);
        return RiadkySuvahy.Select(r => new RiadokVykazu(r.Oz, r.Text, r.R, R(b.GetValueOrDefault(r.R)), R(m.GetValueOrDefault(r.R)), SuctoveSuvaha.Contains(r.R))).ToList();
    }

    public static List<RiadokVykazu> VykazZiskovAStrat(IReadOnlyList<ObratUctu> bezne, IReadOnlyList<ObratUctu>? minule)
    {
        var b = HodnotyVzas(bezne);
        var m = minule is null ? new() : HodnotyVzas(minule);
        return RiadkyVzas.Select(r => new RiadokVykazu(r.Oz, r.Text, r.R, R(b.GetValueOrDefault(r.R)), R(m.GetValueOrDefault(r.R)), SuctoveVzas.Contains(r.R))).ToList();
    }

    static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    /// <summary>Kontroly pred závierkou: rovnosť aktív a pasív, nezaradené pohyby (395), záporná pokladnica.</summary>
    public static List<string> Kontroly(IReadOnlyList<RiadokVykazu> suvaha, IReadOnlyList<ObratUctu> predvaha)
    {
        var k = new List<string>();
        var aktiva = suvaha.First(r => r.Riadok == 1).Bezne;
        var pasiva = suvaha.First(r => r.Riadok == 24).Bezne;
        if (aktiva != pasiva) k.Add($"Aktíva ({PdfVyuctovanie.Eur(aktiva)}) sa nerovnajú pasívam ({PdfVyuctovanie.Eur(pasiva)}) – chýbajú počiatočné stavy alebo je nesprávny ručný zápis.");
        if (predvaha.FirstOrDefault(o => o.Ucet == "395") is { Zostatok: not 0 } z)
            k.Add($"Na účte 395 (nezaradené pohyby) zostáva {PdfVyuctovanie.Eur(z.Zostatok)} – priraďte bankovým pohybom a pokladničným dokladom kategóriu.");
        if (predvaha.FirstOrDefault(o => o.Ucet == "211") is { Zostatok: < 0 } p)
            k.Add($"Pokladnica je v mínuse ({PdfVyuctovanie.Eur(p.Zostatok)}).");
        return k;
    }

    /// <summary>Saldokonto k dátumu: neuhradené (ostávajúce) faktúry po partneroch; zálohové faktúry sa nezahŕňajú.</summary>
    public static List<SaldoRiadok> Saldokonto(IEnumerable<Faktura> faktury, DateOnly k) =>
        faktury.Where(f => f.Typ != TypFaktury.Zalohova && (f.DatumDodania ?? f.DatumVystavenia) <= k)
            .Select(f => (f, z: DanovaEvidencia.ZostatokK(f, k)))
            .Where(x => x.z != 0)
            .Select(x => new SaldoRiadok(x.f.PartnerNazov, Doklad(x.f), x.f.Druh, x.f.DatumSplatnosti, x.f.Suma, x.z,
                Math.Max(0, k.DayNumber - x.f.DatumSplatnosti.DayNumber)))
            .OrderBy(r => r.Partner, StringComparer.CurrentCulture).ThenBy(r => r.Splatnost).ToList();

    /// <summary>Kľúč nastavenia pre uzamknutý rok.</summary>
    public static string KlucUzamknutia(int jednotkaId, int rok) => $"uzamknute.{jednotkaId}.{rok}";
}
