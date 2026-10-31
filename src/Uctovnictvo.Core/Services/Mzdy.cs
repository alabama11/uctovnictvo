using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Sadzby poistného a parametre preddavku na daň pre rok (pracovný pomer).</summary>
public record ParametreMzdy(int Rok,
    decimal Nemocenske, decimal Starobne, decimal Invalidne, decimal Nezamestnanost, decimal Zdravotne, decimal ZdravotneZp,
    decimal NemocenskeZl, decimal StarobneZl, decimal InvalidneZl, decimal NezamestnanostZl, decimal Urazove, decimal Garancne, decimal Rfs,
    decimal ZdravotneZl, decimal ZdravotneZlZp,
    decimal MaxVymeriavaciZaklad, decimal NczdMesacne, IReadOnlyList<PasmoDane> PasmaMesacne, decimal MinimalnaMzda)
{
    public decimal SpZamestnanec => Nemocenske + Starobne + Invalidne + Nezamestnanost;
    public decimal SpZamestnavatel => NemocenskeZl + StarobneZl + InvalidneZl + NezamestnanostZl + Urazove + Garancne + Rfs;
}

public record OdvodFondu(string Fond, decimal Zamestnanec, decimal Zamestnavatel);

public record VypocetMzdy(decimal Hruba, IReadOnlyList<OdvodFondu> Fondy, decimal SpZamestnanec, decimal ZpZamestnanec, decimal SpZamestnavatel, decimal ZpZamestnavatel,
    decimal ZakladPredNczd, decimal Nczd, decimal ZakladDane, decimal Preddavok, decimal DanovyBonus, decimal Cista, decimal CenaPrace);

public record RekapitulaciaMzdy(int Rok, int Mesiac, decimal Hruba, decimal SocialnaPoistovna, IReadOnlyList<(string Poistovna, decimal Suma)> Zdravotne, decimal PreddavkyNaDan, decimal Bonusy, decimal NaVyplatu, decimal CenaPrace);

/// <summary>
/// Mzdy zamestnancov v pracovnom pomere: odvody (zákon 461/2003, 580/2004) a preddavok na daň (§ 35 ZDP).
/// Sadzby 2026 podľa prehľadu odvodov od 1. 1. 2026; nezdaniteľná časť 497,23 € mesačne a pásma podľa Finančnej správy.
/// </summary>
public static class Mzdy
{
    public static readonly IReadOnlyList<ParametreMzdy> ParametreRoky = new[]
    {
        new ParametreMzdy(2026,
            Nemocenske: 1.4m, Starobne: 4m, Invalidne: 3m, Nezamestnanost: 1m, Zdravotne: 5m, ZdravotneZp: 2.5m,
            NemocenskeZl: 1.4m, StarobneZl: 14m, InvalidneZl: 3m, NezamestnanostZl: 1m, Urazove: 0.8m, Garancne: 0.25m, Rfs: 4.75m,
            ZdravotneZl: 11m, ZdravotneZlZp: 5.5m,
            MaxVymeriavaciZaklad: 16_764m, NczdMesacne: 497.23m,
            // ročné pásma 2026 (43 983,32 / 60 349,21 / 75 010,32 €) prepočítané na mesiac
            PasmaMesacne: new[] { new PasmoDane(19, 3_665.28m), new PasmoDane(25, 5_029.10m), new PasmoDane(30, 6_250.86m), new PasmoDane(35, null) },
            MinimalnaMzda: 915m),
    };

    public static ParametreMzdy? Parametre(int rok) => ParametreRoky.FirstOrDefault(p => p.Rok == rok);

    static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    public static VypocetMzdy Vypocitaj(ParametreMzdy p, Zamestnanec z, decimal hruba)
    {
        var vzSp = Math.Min(hruba, p.MaxVymeriavaciZaklad);
        decimal Pct(decimal zaklad, decimal sadzba) => R(zaklad * sadzba / 100);
        var fondy = new List<OdvodFondu>
        {
            new("Nemocenské poistenie", Pct(vzSp, p.Nemocenske), Pct(vzSp, p.NemocenskeZl)),
            new("Starobné poistenie", Pct(vzSp, p.Starobne), Pct(vzSp, p.StarobneZl)),
            new("Invalidné poistenie", Pct(vzSp, p.Invalidne), Pct(vzSp, p.InvalidneZl)),
            new("Poistenie v nezamestnanosti", Pct(vzSp, p.Nezamestnanost), Pct(vzSp, p.NezamestnanostZl)),
            new("Úrazové poistenie", 0, Pct(hruba, p.Urazove)),          // úrazové nemá horný limit
            new("Garančné poistenie", 0, Pct(vzSp, p.Garancne)),
            new("Rezervný fond solidarity", 0, Pct(vzSp, p.Rfs)),
        };
        var spZ = fondy.Sum(f => f.Zamestnanec);
        var spZl = fondy.Sum(f => f.Zamestnavatel);
        var zpZ = Pct(hruba, z.ZdravotnePostihnuty ? p.ZdravotneZp : p.Zdravotne);
        var zpZl = Pct(hruba, z.ZdravotnePostihnuty ? p.ZdravotneZlZp : p.ZdravotneZl);
        var zakladPred = Math.Max(0, hruba - spZ - zpZ);
        var nczd = z.Vyhlasenie ? Math.Min(p.NczdMesacne, zakladPred) : 0;
        var zaklad = zakladPred - nczd;
        decimal dan = 0, dolna = 0;
        foreach (var pasmo in p.PasmaMesacne)
        {
            var horna = pasmo.Do ?? decimal.MaxValue;
            if (zaklad <= dolna) break;
            dan += (Math.Min(zaklad, horna) - dolna) * pasmo.Sadzba / 100;
            dolna = horna;
        }
        var preddavok = R(dan);
        var bonus = z.Vyhlasenie ? z.DanovyBonusMesacne : 0;
        var cista = hruba - spZ - zpZ - preddavok + bonus;
        return new VypocetMzdy(hruba, fondy, spZ, zpZ, spZl, zpZl, zakladPred, nczd, zaklad, preddavok, bonus, cista, hruba + spZl + zpZl);
    }

    /// <summary>Spracuje (vytvorí alebo prepočíta) mzdu zamestnanca za mesiac.</summary>
    public static MzdaMesiac Spracuj(ParametreMzdy p, Zamestnanec z, int rok, int mesiac, decimal hruba, MzdaMesiac? existujuca = null)
    {
        var v = Vypocitaj(p, z, hruba);
        var m = existujuca ?? new MzdaMesiac { ZamestnanecId = z.Id, Zamestnanec = z, Rok = rok, Mesiac = mesiac };
        m.HrubaMzda = hruba;
        m.SpZamestnanec = v.SpZamestnanec;
        m.ZpZamestnanec = v.ZpZamestnanec;
        m.Nczd = v.Nczd;
        m.ZakladDane = v.ZakladDane;
        m.Preddavok = v.Preddavok;
        m.DanovyBonus = v.DanovyBonus;
        m.SpZamestnavatel = v.SpZamestnavatel;
        m.ZpZamestnavatel = v.ZpZamestnavatel;
        return m;
    }

    public static bool PracujeVMesiaci(Zamestnanec z, int rok, int mesiac)
    {
        var zac = new DateOnly(rok, mesiac, 1);
        var kon = zac.AddMonths(1).AddDays(-1);
        return z.DatumNastupu <= kon && (z.DatumUkoncenia is null || z.DatumUkoncenia >= zac);
    }

    public static RekapitulaciaMzdy Rekapitulacia(int rok, int mesiac, IEnumerable<MzdaMesiac> mzdy)
    {
        var m = mzdy.Where(x => x.Rok == rok && x.Mesiac == mesiac).ToList();
        var zp = m.GroupBy(x => string.IsNullOrWhiteSpace(x.Zamestnanec?.ZdravotnaPoistovna) ? "zdravotná poisťovňa" : x.Zamestnanec!.ZdravotnaPoistovna!)
            .Select(g => (g.Key, g.Sum(x => x.ZpZamestnanec + x.ZpZamestnavatel))).ToList();
        return new RekapitulaciaMzdy(rok, mesiac, m.Sum(x => x.HrubaMzda), m.Sum(x => x.SpZamestnanec + x.SpZamestnavatel), zp,
            m.Sum(x => x.Preddavok - x.DanovyBonus), m.Sum(x => x.DanovyBonus), m.Sum(x => x.CistaMzda), m.Sum(x => x.CenaPrace));
    }

    /// <summary>
    /// Zaúčtovanie miezd k poslednému dňu mesiaca: 521/331 hrubá mzda, 331/336 poistné zamestnanca, 331/342 preddavok na daň,
    /// 342/331 daňový bonus, 524/336 poistné zamestnávateľa.
    /// </summary>
    public static List<UctovnyZapis> Zauctuj(int jednotkaId, int rok, IEnumerable<MzdaMesiac> mzdy)
    {
        var z = new List<UctovnyZapis>();
        foreach (var g in mzdy.Where(m => m.Rok == rok).GroupBy(m => m.Mesiac).OrderBy(g => g.Key))
        {
            var d = new DateOnly(rok, g.Key, 1).AddMonths(1).AddDays(-1);
            var doklad = $"MZDY {g.Key:00}/{rok}";
            void P(string popis, string md, string dal, decimal suma)
            {
                if (suma != 0) z.Add(new UctovnyZapis { JednotkaId = jednotkaId, Datum = d, Doklad = doklad, Popis = popis, Md = md, D = dal, Suma = suma, Zdroj = ZdrojZapisu.Mzdy });
            }
            P("Hrubé mzdy", "521", "331", g.Sum(m => m.HrubaMzda));
            P("Poistné zamestnancov", "331", "336", g.Sum(m => m.SpZamestnanec + m.ZpZamestnanec));
            P("Preddavky na daň zo mzdy", "331", "342", g.Sum(m => m.Preddavok));
            P("Daňový bonus", "342", "331", g.Sum(m => m.DanovyBonus));
            P("Poistné zamestnávateľa", "524", "336", g.Sum(m => m.SpZamestnavatel + m.ZpZamestnavatel));
        }
        return z;
    }
}
