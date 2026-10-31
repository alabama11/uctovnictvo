using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Návrh nových ročných záloh podľa skutočných nákladov uplynulého roka.</summary>
public static class NavrhZaloh
{
    /// <summary>
    /// Pre každý priestor a kategóriu: skutočný náklad roka × (1 + navýšenie), zaokrúhlený nahor na násobok 0,12 €,
    /// aby mesačná záloha vyšla na celé centy. Fond opráv a kategórie bez skutočného nákladu ponechávajú doterajší predpis.
    /// </summary>
    public static Dictionary<(int PriestorId, int KategoriaId), decimal> Vypocitaj(
        VysledokVyuctovania vyuctovanie, decimal navyseniePercent)
    {
        var navrh = new Dictionary<(int, int), decimal>();
        foreach (var b in vyuctovanie.Byty.GroupBy(b => b.Priestor.Id))
        {
            var polozky = b.SelectMany(x => x.Polozky).GroupBy(x => x.Kategoria);
            foreach (var g in polozky)
            {
                var k = g.Key;
                var skutocne = g.Sum(x => x.Skutocne);
                var zaloha = g.Sum(x => x.Zaloha);
                decimal suma;
                if (k.JeFondOprav || skutocne == 0)
                    suma = zaloha;
                else
                    suma = Math.Ceiling(skutocne * (1 + navyseniePercent / 100m) / 0.12m) * 0.12m;
                navrh[(b.Key, k.Id)] = suma;
            }
        }
        return navrh;
    }
}

public record StavFondu(int Rok, decimal Zaciatok, decimal Tvorba, decimal InePrijmy, decimal Cerpanie, IReadOnlyList<PohybFondu> Pohyby)
{
    public decimal Koniec => Zaciatok + Tvorba + InePrijmy - Cerpanie;
}

/// <summary>Fond prevádzky, údržby a opráv: počiatočný stav + predpísaná tvorba + iné príjmy − čerpanie.</summary>
public static class FondOprav
{
    public static decimal Tvorba(IEnumerable<Priestor> priestory, IEnumerable<KategoriaNakladov> kategorie, int rok)
    {
        var fo = kategorie.Where(k => k.JeFondOprav).Select(k => k.Id).ToHashSet();
        return priestory.SelectMany(p => p.Predpisy).Where(x => x.Rok == rok && fo.Contains(x.KategoriaId)).Sum(x => x.RocnaSuma);
    }

    /// <summary>
    /// Stav za rok. Počiatočný stav sa zadáva ako pohyb typu „počiatočný stav“ k 1. 1. niektorého roka;
    /// od toho roka sa ďalej pripočítava tvorba, príjmy a čerpanie.
    /// </summary>
    public static StavFondu Vypocitaj(IReadOnlyList<Priestor> priestory, IReadOnlyList<KategoriaNakladov> kategorie, IReadOnlyList<PohybFondu> pohyby, int rok)
    {
        var ps = pohyby.Where(x => x.Typ == TypPohybuFondu.PociatocnyStav).OrderBy(x => x.Datum).ToList();
        var prvyRok = ps.Count > 0 ? ps[0].Datum.Year : rok;
        decimal zaciatok = 0;
        for (int y = prvyRok; y < rok; y++)
            zaciatok += ZaRok(y).Zmena;
        zaciatok += ps.Where(x => x.Datum.Year <= rok).Sum(x => x.Suma);

        var r = ZaRok(rok);
        return new StavFondu(rok, zaciatok, r.Tvorba, r.Prijmy, r.Cerpanie,
            pohyby.Where(x => x.Datum.Year == rok && x.Typ != TypPohybuFondu.PociatocnyStav).OrderBy(x => x.Datum).ToList());

        (decimal Tvorba, decimal Prijmy, decimal Cerpanie, decimal Zmena) ZaRok(int y)
        {
            var t = rok >= prvyRok ? Tvorba(priestory, kategorie, y) : 0m;
            var pr = pohyby.Where(x => x.Datum.Year == y && x.Typ == TypPohybuFondu.InyPrijem).Sum(x => x.Suma);
            var c = pohyby.Where(x => x.Datum.Year == y && x.Typ == TypPohybuFondu.Cerpanie).Sum(x => x.Suma);
            return (t, pr, c, t + pr - c);
        }
    }
}
