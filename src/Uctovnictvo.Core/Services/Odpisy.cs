using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public record OdpisRoka(int Rok, decimal Odpis, decimal UplatnenyOdpis, decimal ZostatkovaCena, string? Poznamka);

/// <summary>
/// Daňové odpisy hmotného majetku podľa zákona č. 595/2003 Z. z. (§ 26–28, § 22 ods. 12), stav k roku 2026.
/// Overené na stránkach Finančnej správy „Výpočet rovnomerných odpisov hmotného majetku“ a „Výpočet zrýchlených odpisov“.
/// </summary>
public static class Odpisy
{
    /// <summary>Doba odpisovania v rokoch pre odpisové skupiny 0 až 6.</summary>
    public static readonly int[] DobaOdpisovania = { 2, 4, 6, 8, 12, 20, 40 };

    /// <summary>Hnuteľný hmotný majetok sa odpisuje, ak vstupná cena presahuje 1 700 €, nehmotný 2 400 € (2026).</summary>
    public const decimal HranicaHmotny = 1700m;
    public const decimal HranicaNehmotny = 2400m;

    /// <summary>Zrýchlene sa dá odpisovať len 2. a 3. odpisová skupina; koeficienty (prvý rok, ďalšie roky).</summary>
    public static (int Prvy, int Dalsie)? Koeficienty(int skupina) => skupina switch
    {
        2 => (6, 7),
        3 => (8, 9),
        _ => null,
    };

    static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Odpisový plán. Rok zaradenia: pomerná časť ročného odpisu za mesiace od mesiaca zaradenia (vrátane);
    /// neuplatnená časť sa uplatní v roku po uplynutí doby odpisovania. Pri vyradení sa v roku vyradenia uplatní
    /// odpis za celé mesiace pred mesiacom vyradenia (§ 22 ods. 12), najviac do zostatkovej ceny.
    /// Uplatnený odpis = odpis × podiel použitia na podnikanie.
    /// </summary>
    public static List<OdpisRoka> Plan(Majetok m)
    {
        var vysledok = new List<OdpisRoka>();
        if (m.VstupnaCena <= 0 || m.OdpisovaSkupina < 0 || m.OdpisovaSkupina > 6) return vysledok;
        var doba = DobaOdpisovania[m.OdpisovaSkupina];
        var vc = m.VstupnaCena;
        var rok0 = m.DatumZaradenia.Year;
        var mesiace0 = 13 - m.DatumZaradenia.Month;
        var podiel = Math.Clamp(m.PodielPodnikanie, 0, 100) / 100m;
        var koef = m.Metoda == MetodaOdpisovania.Zrychlena ? Koeficienty(m.OdpisovaSkupina) : null;

        // ročné odpisy bez vyradenia
        var odpisy = new List<decimal>();
        if (koef is { } k)
        {
            var rocny1 = vc / k.Prvy;
            var prvy = mesiace0 == 12 ? R(rocny1) : R(R(rocny1 / 12m) * mesiace0);
            odpisy.Add(prvy);
            var zc = vc - R(rocny1);
            for (int n = 1; n < doba; n++)
            {
                var o = k.Dalsie - n <= 2 ? zc : R(2 * zc / (k.Dalsie - n));
                odpisy.Add(o);
                zc -= o;
            }
        }
        else
        {
            var rocny = R(vc / doba);
            odpisy.Add(mesiace0 == 12 ? rocny : R(R(rocny / 12m) * mesiace0));
            for (int n = 1; n < doba; n++) odpisy.Add(rocny);
        }
        var zvysok = vc - odpisy.Sum();
        if (zvysok > 0) odpisy.Add(zvysok);
        else if (zvysok < 0) odpisy[^1] += zvysok;

        var zostatok = vc;
        for (int i = 0; i < odpisy.Count; i++)
        {
            var rok = rok0 + i;
            var o = odpisy[i];
            string? poz = i == 0 && mesiace0 < 12 ? $"pomerná časť za {mesiace0} mes." : i == odpisy.Count - 1 && zvysok > 0 && mesiace0 < 12 ? "zvyšok z roka zaradenia" : null;
            if (m.DatumVyradenia is { } dv && dv.Year <= rok)
            {
                if (dv.Year == rok)
                {
                    // celé mesiace v evidencii pred mesiacom vyradenia
                    var mesiace = i == 0 ? Math.Max(0, dv.Month - m.DatumZaradenia.Month) : dv.Month - 1;
                    var rocny = koef is { } kk ? (i == 0 ? R(vc / kk.Prvy) : o) : R(vc / doba);
                    o = Math.Min(zostatok, R(R(rocny / 12m) * mesiace));
                    zostatok -= o;
                    vysledok.Add(new OdpisRoka(rok, o, R(o * podiel), zostatok, $"vyradené {dv:d.M.yyyy}, zostatková cena {PdfVyuctovanie.Eur(zostatok)}"));
                }
                break;
            }
            zostatok -= o;
            vysledok.Add(new OdpisRoka(rok, o, R(o * podiel), zostatok, poz));
        }
        return vysledok;
    }

    public static OdpisRoka? ZaRok(Majetok m, int rok) => Plan(m).FirstOrDefault(x => x.Rok == rok);

    /// <summary>Zostatková cena na konci roka (vstupná cena, ak sa ešte neodpisoval).</summary>
    public static decimal ZostatkovaCena(Majetok m, int rok)
    {
        var plan = Plan(m).Where(x => x.Rok <= rok).ToList();
        return plan.Count == 0 ? (m.DatumZaradenia.Year > rok ? 0 : m.VstupnaCena) : plan[^1].ZostatkovaCena;
    }

    public static string DalsieInventarneCislo(int rok, IEnumerable<string> existujuce) =>
        Faktury.DalsieCislo("RRRR###", rok, existujuce, "M");
}
