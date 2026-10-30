using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Jedna kategória nákladov vo vyúčtovaní bytu: záloha podľa predpisu a skutočný podiel na náklade.</summary>
public record PolozkaVyuctovania(KategoriaNakladov Kategoria, decimal NakladDomu, decimal Zaloha, decimal Skutocne)
{
    /// <summary>+ záloha bola vyššia ako náklad, − náklad bol vyšší.</summary>
    public decimal Rozdiel => Zaloha - Skutocne;
}

public record MesiacVyuctovania(int Mesiac, decimal Predpis, decimal ZaplateneZalohy, decimal ZaplatenePoistne, decimal ZaplateneMinulyRok);

public record VodaVyuctovania(
    string? CisloVodomeru,
    DateOnly? DatumZaciatok, decimal? StavZaciatok,
    DateOnly? DatumKoniec, decimal? StavKoniec,
    decimal Spotreba,
    decimal CenaZaM3,
    decimal Naklad,
    decimal Zaloha,
    IReadOnlyList<(int Rok, decimal Spotreba)> Historia)
{
    public decimal Rozdiel => Zaloha - Naklad;
}

public record VyuctovanieBytu(
    Priestor Priestor,
    int Rok,
    IReadOnlyList<PolozkaVyuctovania> Polozky,
    IReadOnlyList<MesiacVyuctovania> Mesiace,
    VodaVyuctovania? Voda,
    decimal ZostatokMinulyRok,
    decimal ZaplateneMinulyRok,
    decimal PoistnePredpis,
    decimal PoistneZaplatene)
{
    public decimal PredpisZaloh => Polozky.Sum(p => p.Zaloha);
    public decimal ZaplateneZalohy => Mesiace.Sum(m => m.ZaplateneZalohy);
    /// <summary>Zaplatené mínus predpísané zálohy (− = vlastník nezaplatil celý predpis).</summary>
    public decimal NedoplatokNaZalohach => ZaplateneZalohy - PredpisZaloh;
    /// <summary>Skutočné náklady bytu vrátane fondu opráv.</summary>
    public decimal SkutocneSpolu => Polozky.Sum(p => p.Skutocne);
    /// <summary>Predpísané zálohy mínus skutočné náklady (− = nedoplatok za skutočné náklady).</summary>
    public decimal VysledokNakladov => PredpisZaloh - SkutocneSpolu;
    /// <summary>Zostatok minulého roka po zohľadnení platieb/vrátení v tomto roku.</summary>
    public decimal RozdielMinulyRok => ZostatokMinulyRok + ZaplateneMinulyRok;
    public decimal RozdielPoistne => PoistneZaplatene - PoistnePredpis;

    /// <summary>Konečný výsledok: + preplatok vlastníka, − suma, ktorú má doplatiť.</summary>
    public decimal Vysledok => NedoplatokNaZalohach + VysledokNakladov + RozdielMinulyRok + RozdielPoistne;

    public decimal MesacnyPredpis => Math.Round(PredpisZaloh / 12m, 2, MidpointRounding.AwayFromZero);
}

public record VysledokVyuctovania(int Rok, IReadOnlyList<VyuctovanieBytu> Byty, IReadOnlyList<string> Upozornenia)
{
    public decimal SpoluVysledok => Byty.Sum(b => b.Vysledok);
}

/// <summary>
/// Ročné vyúčtovanie spoločenstva: rozpočíta skutočné náklady domu na priestory podľa kľúča kategórie
/// a porovná ich s predpísanými a zaplatenými zálohami. Priestory musia mať načítané
/// vlastníkov, roky, predpisy a úhrady; kategórie musia mať načítané náklady.
/// </summary>
public static class Vyuctovanie
{
    public static VysledokVyuctovania Vypocitaj(int rok, IReadOnlyList<Priestor> priestory, IReadOnlyList<KategoriaNakladov> kategorie)
    {
        var upozornenia = new List<string>();
        var zoradene = priestory.OrderBy(p => p.Cislo, CisloComparer.Instance).ToList();
        var kat = kategorie.OrderBy(k => k.Poradie).ThenBy(k => k.Id).ToList();

        // podiel každého priestoru na náklade každej kategórie
        var skutocne = new Dictionary<(int Kat, int Priestor), decimal>();
        var cenaVody = 0m;
        foreach (var k in kat)
        {
            if (k.JeFondOprav)
            {
                foreach (var p in zoradene)
                    skutocne[(k.Id, p.Id)] = Predpis(p, k, rok);
                continue;
            }
            var naklad = NakladDomu(k, rok);
            if (naklad == 0) continue;

            var vybrane = VybranePriestory(k);
            var vahy = zoradene.Select(p => vybrane is null || vybrane.Contains(p.Cislo.Trim()) ? VahaPriestoru(p, k.Kluc, rok) : 0m).ToList();
            if (vybrane is not null)
                foreach (var chyba in vybrane.Where(c => zoradene.All(p => p.Cislo.Trim() != c)))
                    upozornenia.Add($"Kategória „{k.Nazov}“ uvádza priestor {chyba}, ktorý neexistuje.");
            if (vahy.Sum() <= 0)
            {
                upozornenia.Add($"Náklad „{k.Nazov}“ ({naklad:N2} €) nemožno rozpočítať: kľúč {NazovKluca(k.Kluc)} je pre všetky priestory nulový.");
                continue;
            }
            if (k.Kluc == KlucRozpoctu.SpotrebaVody)
                cenaVody = naklad / vahy.Sum();
            // Každý podiel sa zaokrúhľuje samostatne na centy (ako v doterajšej Excel tabuľke);
            // súčet podielov sa preto môže od nákladu domu líšiť o pár centov.
            var spoluVaha = vahy.Sum();
            for (int i = 0; i < zoradene.Count; i++)
                skutocne[(k.Id, zoradene[i].Id)] = Math.Round(naklad * vahy[i] / spoluVaha, 2, MidpointRounding.AwayFromZero);
        }

        var vodaKat = kat.FirstOrDefault(k => k.Kluc == KlucRozpoctu.SpotrebaVody && !k.JeFondOprav);
        var byty = new List<VyuctovanieBytu>();
        foreach (var p in zoradene)
        {
            var pr = p.Roky.FirstOrDefault(r => r.Rok == rok);
            if (!p.Predpisy.Any(x => x.Rok == rok))
                upozornenia.Add($"Priestor {p.Cislo} nemá predpis záloh na rok {rok}.");
            if (vodaKat is not null && NakladDomu(vodaKat, rok) != 0 && (pr?.StavVodomeruKoniec is null || pr.StavVodomeruZaciatok is null))
                upozornenia.Add($"Priestor {p.Cislo} nemá odpočet vody za rok {rok}.");

            var polozky = kat.Select(k => new PolozkaVyuctovania(
                k, k.JeFondOprav ? 0m : NakladDomu(k, rok), Predpis(p, k, rok),
                skutocne.TryGetValue((k.Id, p.Id), out var s) ? s : 0m)).ToList();

            var uhrady = p.Uhrady.Where(u => u.Datum.Year == rok).ToList();
            var mesacnyPredpis = Math.Round(polozky.Sum(x => x.Zaloha) / 12m, 2, MidpointRounding.AwayFromZero);
            var mesiace = Enumerable.Range(1, 12).Select(m => new MesiacVyuctovania(m, mesacnyPredpis,
                uhrady.Where(u => u.Datum.Month == m && u.Ucel == UcelUhrady.Zalohy).Sum(u => u.Suma),
                uhrady.Where(u => u.Datum.Month == m && u.Ucel == UcelUhrady.Poistne).Sum(u => u.Suma),
                uhrady.Where(u => u.Datum.Month == m && u.Ucel == UcelUhrady.MinulyRok).Sum(u => u.Suma))).ToList();

            VodaVyuctovania? voda = null;
            if (vodaKat is not null)
            {
                var historia = p.Roky.Where(r => r.Rok < rok && r.StavVodomeruKoniec is not null)
                    .OrderBy(r => r.Rok).TakeLast(4).Select(r => (r.Rok, r.SpotrebaVody)).ToList();
                var polozkaVody = polozky.First(x => x.Kategoria.Id == vodaKat.Id);
                voda = new VodaVyuctovania(p.CisloVodomeru,
                    pr?.DatumOdpoctuZaciatok, pr?.StavVodomeruZaciatok, pr?.DatumOdpoctuKoniec, pr?.StavVodomeruKoniec,
                    pr?.SpotrebaVody ?? 0m, Math.Round(cenaVody, 2, MidpointRounding.AwayFromZero),
                    polozkaVody.Skutocne, polozkaVody.Zaloha, historia);
            }

            byty.Add(new VyuctovanieBytu(p, rok, polozky, mesiace, voda,
                pr?.ZostatokMinulyRok ?? 0m,
                uhrady.Where(u => u.Ucel == UcelUhrady.MinulyRok).Sum(u => u.Suma),
                pr?.PoistnePredpis ?? 0m,
                uhrady.Where(u => u.Ucel == UcelUhrady.Poistne).Sum(u => u.Suma)));
        }

        return new VysledokVyuctovania(rok, byty, upozornenia);
    }

    public static decimal NakladDomu(KategoriaNakladov k, int rok) => k.Naklady.Where(n => n.Rok == rok).Sum(n => n.Suma);

    static decimal Predpis(Priestor p, KategoriaNakladov k, int rok) =>
        p.Predpisy.Where(x => x.Rok == rok && x.KategoriaId == k.Id).Sum(x => x.RocnaSuma);

    public static HashSet<string>? VybranePriestory(KategoriaNakladov k)
    {
        if (string.IsNullOrWhiteSpace(k.LenPriestory)) return null;
        return k.LenPriestory.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
    }

    static decimal VahaPriestoru(Priestor p, KlucRozpoctu kluc, int rok) => kluc switch
    {
        KlucRozpoctu.Plocha => p.Plocha,
        KlucRozpoctu.SpotrebaVody => Math.Max(0m, p.Roky.FirstOrDefault(r => r.Rok == rok)?.SpotrebaVody ?? 0m),
        KlucRozpoctu.PocetOsob => p.PocetOsob,
        KlucRozpoctu.RovnakoNaPriestor => 1m,
        KlucRozpoctu.SpoluvlastnickyPodiel => p.PodielMenovatel > 0 ? (decimal)p.PodielCitatel / p.PodielMenovatel : 0m,
        _ => 0m,
    };

    /// <summary>
    /// Rozdelí sumu podľa váh na celé centy tak, aby súčet presne sedel
    /// (metóda najväčších zvyškov – zvyšné centy dostanú priestory s najväčším zaokrúhľovacím zvyškom).
    /// </summary>
    public static IReadOnlyList<decimal> RozdelNaCenty(decimal suma, IReadOnlyList<decimal> vahy)
    {
        var spoluVaha = vahy.Sum();
        var vysledok = new decimal[vahy.Count];
        if (spoluVaha <= 0) return vysledok;

        var centySpolu = (long)Math.Round(suma * 100m, MidpointRounding.AwayFromZero);
        var znamienko = Math.Sign(centySpolu);
        centySpolu = Math.Abs(centySpolu);

        var presne = vahy.Select(v => centySpolu * v / spoluVaha).ToArray();
        var cele = presne.Select(x => (long)Math.Floor(x)).ToArray();
        var zostava = centySpolu - cele.Sum();
        foreach (var i in Enumerable.Range(0, vahy.Count).OrderByDescending(i => presne[i] - cele[i]).ThenBy(i => i).Take((int)zostava))
            cele[i]++;

        for (int i = 0; i < vahy.Count; i++)
            vysledok[i] = znamienko * cele[i] / 100m;
        return vysledok;
    }

    public static string NazovKluca(KlucRozpoctu k) => k switch
    {
        KlucRozpoctu.Plocha => "podľa plochy (m²)",
        KlucRozpoctu.SpotrebaVody => "podľa vodomeru (m³)",
        KlucRozpoctu.RovnakoNaPriestor => "rovnako na byt",
        KlucRozpoctu.PocetOsob => "podľa počtu osôb",
        KlucRozpoctu.SpoluvlastnickyPodiel => "podľa spoluvlastníckeho podielu",
        _ => k.ToString(),
    };
}

/// <summary>Zoradí čísla bytov prirodzene: 2 &lt; 10, „A1“ &lt; „A2“ &lt; „A10“.</summary>
public sealed class CisloComparer : IComparer<string>
{
    public static readonly CisloComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        x ??= ""; y ??= "";
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var a = x[si..i].TrimStart('0');
                var b = y[sj..j].TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                var c = string.CompareOrdinal(a, b);
                if (c != 0) return c;
            }
            else
            {
                var c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
