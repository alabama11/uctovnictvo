using System.Text.Json;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Riadok podkladu k daňovému priznaniu; Riadok = číslo riadku tlačiva (prázdne, ak ho nemáme overené).</summary>
public record RiadokDp(string Riadok, string Text, decimal? Suma, bool Zvyraznit = false);

/// <summary>Pásmo sadzby dane FO: sadzba sa uplatní na časť základu dane do hornej hranice (null = bez hranice).</summary>
public record PasmoDane(decimal Sadzba, decimal? Do);

/// <summary>
/// Parametre dane z príjmov FO pre zdaňovacie obdobie (overené na podpora.financnasprava.sk):
/// nezdaniteľná časť na daňovníka (§ 11 ods. 2), sadzby (§ 15), paušálne výdavky (§ 6 ods. 10).
/// </summary>
public record ParametreFo(int Rok, decimal Nczd, decimal NczdHranica, decimal NczdZaklad, decimal NczdDelitel,
    IReadOnlyList<PasmoDane> Pasma, decimal HranicaPrijmov15 = 100_000m, decimal PausalPercento = 60m, decimal PausalMax = 20_000m, decimal Pilier3Max = 180m);

/// <summary>Parametre dane z príjmov PO (§ 15 písm. b) a minimálna daň § 46b).</summary>
public record ParametrePo(int Rok, IReadOnlyList<(decimal Do, decimal Sadzba)> Sadzby, IReadOnlyList<(decimal Do, decimal Dan)> MinimalnaDan);

/// <summary>Vstupy, ktoré program nevie zistiť z evidencie (ukladajú sa v nastaveniach).</summary>
public class VstupyDp
{
    // FO
    public bool PausalneVydavky { get; set; }
    /// <summary>Zaplatené povinné poistné SZČO (sociálne + zdravotné); pri paušálnych výdavkoch sa pripočíta k paušálu.</summary>
    public decimal? ZaplatenePoistne { get; set; }
    public decimal Pilier3 { get; set; }
    public decimal DanovyBonus { get; set; }
    // FO aj PO
    public decimal OdpocetStraty { get; set; }
    public decimal ZaplatenePreddavky { get; set; }
    // PO
    public decimal DalsiePripocitatelne { get; set; }
    public decimal Odpocitatelne { get; set; }
    public bool PrveObdobie { get; set; }

    public static string Kluc(int jednotkaId, int rok) => $"dp.{jednotkaId}.{rok}";
    public string Zapis() => JsonSerializer.Serialize(this);
    public static VstupyDp Citaj(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<VstupyDp>(json) ?? new(); } catch (JsonException) { return new(); }
    }
}

public record VysledokDpFo(decimal Prijmy, decimal Vydavky, decimal ZakladDane, decimal Strata, decimal Nczd, decimal NczdSpolu,
    decimal ZakladPoNczd, decimal Sadzba15, decimal Dan, decimal DanPoBonuse, decimal NaUhradu, bool PovinnostPodat, List<RiadokDp> Riadky);

public record VysledokDpPo(decimal VysledokHospodarenia, decimal Pripocitatelne, decimal Odpocitatelne, decimal ZakladDane, decimal Strata,
    decimal Vynosy, decimal Sadzba, decimal Dan, decimal MinimalnaDan, decimal DanovaPovinnost, decimal NaUhradu, string Preddavky, List<RiadokDp> Riadky);

public static class DanovePriznanie
{
    public static readonly IReadOnlyList<ParametreFo> ParametreFoRoky = new[]
    {
        // 2025: ŽM 273,99 €; NČZD 21-násobok, hranica 92,8-násobok, 44,2-násobok − ¼ ZD; 19 % do 48 441,43 €, 25 % nad
        new ParametreFo(2025, 5_753.79m, 25_426.27m, 12_110.36m, 4m, new[] { new PasmoDane(19, 48_441.43m), new PasmoDane(25, null) }),
        // 2026: ŽM 284,13 €; NČZD 5 966,73 €, hranica 26 083,13 €, 14 661,11 € − ⅓ ZD; 19/25/30/35 %
        new ParametreFo(2026, 5_966.73m, 26_083.13m, 14_661.11m, 3m, new[]
        {
            new PasmoDane(19, 43_983.32m), new PasmoDane(25, 60_349.21m), new PasmoDane(30, 75_010.32m), new PasmoDane(35, null),
        }),
    };

    public static readonly IReadOnlyList<ParametrePo> ParametrePoRoky = new[]
    {
        new ParametrePo(2025, new[] { (100_000m, 10m), (5_000_000m, 21m), (decimal.MaxValue, 24m) },
            new[] { (50_000m, 340m), (250_000m, 960m), (500_000m, 1_920m), (decimal.MaxValue, 3_840m) }),
        new ParametrePo(2026, new[] { (100_000m, 10m), (5_000_000m, 21m), (decimal.MaxValue, 24m) },
            new[] { (50_000m, 340m), (250_000m, 960m), (500_000m, 1_920m), (5_000_000m, 3_840m), (decimal.MaxValue, 11_520m) }),
    };

    /// <summary>Parametre pre rok; null, ak pre rok nie sú v programe overené hodnoty.</summary>
    public static ParametreFo? Fo(int rok) => ParametreFoRoky.FirstOrDefault(p => p.Rok == rok);
    public static ParametrePo? Po(int rok) => ParametrePoRoky.FirstOrDefault(p => p.Rok == rok);

    static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    /// <summary>Nezdaniteľná časť základu dane na daňovníka (§ 11 ods. 2) pre základ dane podľa § 4 ods. 1 písm. a) a b).</summary>
    public static decimal NezdanitelnaCast(ParametreFo p, decimal zakladDane) =>
        zakladDane <= p.NczdHranica ? p.Nczd : Math.Max(0, R(p.NczdZaklad - zakladDane / p.NczdDelitel));

    /// <summary>Daň zo základu dane z podnikania: 15 % pri zdaniteľných príjmoch do hranice, inak podľa pásiem.</summary>
    public static decimal DanFo(ParametreFo p, decimal zaklad, decimal prijmy)
    {
        if (zaklad <= 0) return 0;
        if (prijmy <= p.HranicaPrijmov15) return R(zaklad * 0.15m);
        decimal dan = 0, dolna = 0;
        foreach (var pasmo in p.Pasma)
        {
            var horna = pasmo.Do ?? decimal.MaxValue;
            if (zaklad <= dolna) break;
            dan += R((Math.Min(zaklad, horna) - dolna) * pasmo.Sadzba / 100);
            dolna = horna;
        }
        return dan;
    }

    /// <summary>
    /// Podklad k DP FO typ B pre SZČO s príjmami len podľa § 6 ods. 1 a 2 (živnosť). Čísla riadkov sú podľa tlačiva DPFOBv25.
    /// </summary>
    public static VysledokDpFo VypocitajFo(ParametreFo p, decimal prijmy, decimal skutocneVydavky, decimal poistneZDennika, VstupyDp v)
    {
        var poistne = v.ZaplatenePoistne ?? poistneZDennika;
        var pausal = Math.Min(R(prijmy * p.PausalPercento / 100), p.PausalMax);
        var vydavky = v.PausalneVydavky ? pausal + poistne : skutocneVydavky;
        var rozdiel = prijmy - vydavky;
        var strata = rozdiel < 0 ? -rozdiel : 0;
        var zd = Math.Max(0, rozdiel);
        var poStrate = Math.Max(0, zd - Math.Min(v.OdpocetStraty, zd));
        var nczd = NezdanitelnaCast(p, poStrate);
        var pilier = Math.Min(Math.Max(0, v.Pilier3), p.Pilier3Max);
        var nczdSpolu = Math.Min(nczd + pilier, poStrate);
        var zakladPoNczd = poStrate - nczdSpolu;
        var dan = DanFo(p, zakladPoNczd, prijmy);
        var poBonuse = dan - v.DanovyBonus;
        var naUhradu = poBonuse - v.ZaplatenePreddavky;
        var povinnost = prijmy > R(p.Nczd / 2) || strata > 0;

        var r = new List<RiadokDp>
        {
            new("tab. 1 r. 2 stĺ. 1", "Príjmy zo živnosti (§ 6 ods. 1 písm. b)", prijmy),
            new("tab. 1 r. 2 stĺ. 2", v.PausalneVydavky ? $"Výdavky: paušál {p.PausalPercento:0} % (max. {PdfVyuctovanie.Eur(p.PausalMax)}) {PdfVyuctovanie.Eur(pausal)} + zaplatené poistné {PdfVyuctovanie.Eur(poistne)}" : "Výdavky preukázateľné (daňová evidencia)", vydavky),
            new("r. 39", "Príjmy z tabuľky č. 1", prijmy),
            new("r. 40", "Výdavky z tabuľky č. 1", vydavky),
            new("r. 41", "Základ dane (r. 39 − r. 40)", zd),
        };
        if (strata > 0) r.Add(new("", "Daňová strata (výdavky prevyšujú príjmy) – odpočítate ju v ďalších rokoch", strata, true));
        r.Add(new("r. 45", "Základ dane z príjmov podľa § 6 ods. 1 a 2", zd));
        if (v.OdpocetStraty > 0) r.Add(new("r. 51–54a", "Odpočet daňovej straty z minulých rokov", Math.Min(v.OdpocetStraty, zd)));
        r.Add(new("r. 55", "Základ dane po odpočte daňovej straty", poStrate));
        r.Add(new("r. 57", "Základ dane z § 6 ods. 1 a 2 (bez odpočtu výskumu a vývoja)", poStrate));
        r.Add(new("r. 72", "Základ dane podľa § 4 ods. 1 písm. a) a b) (tu len z podnikania)", poStrate));
        r.Add(new("r. 73", poStrate <= p.NczdHranica ? "Nezdaniteľná časť na daňovníka (plná suma)" : $"Nezdaniteľná časť na daňovníka ({PdfVyuctovanie.Eur(p.NczdZaklad)} − 1/{p.NczdDelitel:0} základu dane)", nczd));
        if (pilier > 0) r.Add(new("r. 75", "Príspevky na doplnkové dôchodkové sporenie (III. pilier)", pilier));
        r.Add(new("r. 77", "Nezdaniteľné časti spolu (najviac do výšky r. 72)", nczdSpolu));
        r.Add(new("r. 94", "Základ dane z podnikania znížený o nezdaniteľné časti", zakladPoNczd, true));
        r.Add(new("r. 95", "Zdaniteľné príjmy z podnikania (pre sadzbu 15 %)", prijmy));
        r.Add(new("r. 96", prijmy <= p.HranicaPrijmov15 ? "Daň 15 % (zdaniteľné príjmy do 100 000 €)" : "Daň podľa pásiem " + string.Join(" / ", p.Pasma.Select(x => $"{x.Sadzba:0} %")), dan));
        r.Add(new("r. 116", "Daň spolu", dan, true));
        if (v.DanovyBonus > 0)
        {
            r.Add(new("r. 117", "Daňový bonus na deti", v.DanovyBonus));
            r.Add(new("r. 118", "Daň znížená o daňový bonus", poBonuse));
        }
        if (v.ZaplatenePreddavky > 0) r.Add(new("", "Zaplatené preddavky na daň", v.ZaplatenePreddavky));
        r.Add(new("", naUhradu >= 0 ? "Daň na úhradu" : "Daňový preplatok (alebo bonus na vyplatenie)", Math.Abs(naUhradu), true));
        return new VysledokDpFo(prijmy, vydavky, zd, strata, nczd, nczdSpolu, zakladPoNczd, prijmy <= p.HranicaPrijmov15 ? 15 : 0, dan, poBonuse, naUhradu, povinnost, r);
    }

    /// <summary>Nedaňové náklady (účty s Danovy = false, okrem splatnej dane 591/595) a daň z príjmov zaúčtovaná na 591/595.</summary>
    public static (decimal Nedanove, decimal Dan) NedanoveNaklady(IReadOnlyList<ObratUctu> predvaha, IReadOnlyList<Ucet> rozvrh)
    {
        decimal nedanove = 0, dan = 0;
        foreach (var o in predvaha.Where(o => o.Ucet.StartsWith('5')))
        {
            var suma = o.ObratMd - o.ObratD;
            if (o.Ucet.StartsWith("591") || o.Ucet.StartsWith("595")) { dan += suma; continue; }
            var u = rozvrh.FirstOrDefault(x => x.Cislo == o.Ucet) ?? rozvrh.FirstOrDefault(x => x.Cislo == o.Ucet[..Math.Min(3, o.Ucet.Length)]);
            if (u is { Danovy: false }) nedanove += suma;
        }
        return (nedanove, dan);
    }

    public static decimal MinimalnaDan(ParametrePo p, decimal vynosy) => p.MinimalnaDan.First(x => vynosy <= x.Do).Dan;

    /// <summary>Podklad k DP PO pre s.r.o. z podvojného účtovníctva. Čísla riadkov podľa tlačiva DPPOv25.</summary>
    public static VysledokDpPo VypocitajPo(ParametrePo p, IReadOnlyList<ObratUctu> predvaha, IReadOnlyList<Ucet> rozvrh, VstupyDp v)
    {
        var vh = Podvojne.VysledokHospodarenia(predvaha);
        var (nedanove, danZauctovana) = NedanoveNaklady(predvaha, rozvrh);
        var vhPred = vh + danZauctovana;
        var vynosy = predvaha.Where(o => o.Ucet.StartsWith('6')).Sum(o => o.ObratD - o.ObratMd);
        var prip = nedanove + v.DalsiePripocitatelne;
        var zaklad = vhPred + prip - v.Odpocitatelne;
        var strata = zaklad < 0 ? -zaklad : 0;
        var zd = Math.Max(0, zaklad);
        var odpocet = Math.Min(v.OdpocetStraty, zd);
        var poStrate = zd - odpocet;
        var sadzba = p.Sadzby.First(x => vynosy <= x.Do).Sadzba;
        var dan = R(poStrate * sadzba / 100);
        var min = v.PrveObdobie ? 0 : MinimalnaDan(p, vynosy);
        var povinnost = Math.Max(dan, min);
        var naUhradu = povinnost - v.ZaplatenePreddavky;
        var preddavky = dan <= 5_000 ? "neplatia sa (daň do 5 000 €)" : dan <= 16_600 ? $"štvrťročne po {PdfVyuctovanie.Eur(R(dan / 4))}" : $"mesačne po {PdfVyuctovanie.Eur(R(dan / 12))}";

        var r = new List<RiadokDp>
        {
            new("r. 100", "Výsledok hospodárenia pred zdanením" + (danZauctovana != 0 ? $" (bez zaúčtovanej dane {PdfVyuctovanie.Eur(danZauctovana)})" : ""), vhPred),
            new("r. 110–200", v.DalsiePripocitatelne != 0 ? "Pripočítateľné položky (nedaňové náklady + ďalšie)" : "Pripočítateľné položky (nedaňové náklady)", prip),
            new("r. 210–300", "Odpočítateľné položky", v.Odpocitatelne),
            new("", strata > 0 ? "Daňová strata" : "Základ dane", strata > 0 ? strata : zd, true),
        };
        if (odpocet > 0) r.Add(new("r. 410", "Odpočet daňovej straty z minulých rokov", odpocet));
        r.Add(new("", "Základ dane po odpočte straty", poStrate));
        r.Add(new("", "Zdaniteľné výnosy (pre sadzbu a minimálnu daň)", vynosy));
        r.Add(new("r. 550", "Sadzba dane", sadzba));
        r.Add(new("", "Daň zo základu dane", dan, true));
        r.Add(new("r. 810", v.PrveObdobie ? "Minimálna daň – neplatí sa v prvom zdaňovacom období" : "Minimálna daň (daňová licencia) podľa výnosov", min));
        r.Add(new("r. 800", "Daňová povinnosť (vyššia z dane a minimálnej dane)", povinnost, true));
        if (v.ZaplatenePreddavky > 0) r.Add(new("r. 1010", "Zaplatené preddavky na daň", v.ZaplatenePreddavky));
        r.Add(new(naUhradu >= 0 ? "r. 1100" : "r. 1101", naUhradu >= 0 ? "Daň na úhradu" : "Daňový preplatok", Math.Abs(naUhradu), true));
        r.Add(new("r. 1110", "Daň pre určenie preddavkov na ďalšie obdobie", dan));
        return new VysledokDpPo(vhPred, prip, v.Odpocitatelne, zd, strata, vynosy, sadzba, dan, min, povinnost, naUhradu, preddavky, r);
    }
}
