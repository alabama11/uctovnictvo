using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public record MesiacKonta(int Mesiac, decimal Predpis, decimal Zaplatene, decimal Zostatok);

/// <summary>
/// Priebežný stav platieb priestoru v roku: čo mal zaplatiť (mesačné zálohy, poistné, zostatok minulého roka)
/// a čo zaplatil. Zostatok: + preplatok, − dlh.
/// </summary>
public record KontoPriestoru(
    Priestor Priestor,
    int Rok,
    int KuMesiacu,
    decimal ZostatokMinulyRok,
    decimal PoistnePredpis,
    IReadOnlyList<MesiacKonta> Mesiace)
{
    public decimal PredpisDoteraz => Mesiace.Where(m => m.Mesiac <= KuMesiacu).Sum(m => m.Predpis);
    public decimal ZaplateneDoteraz => Mesiace.Where(m => m.Mesiac <= KuMesiacu).Sum(m => m.Zaplatene);
    public decimal Zostatok => Mesiace.FirstOrDefault(m => m.Mesiac == KuMesiacu)?.Zostatok ?? ZostatokMinulyRok;
    public decimal Dlh => Math.Max(0m, -Zostatok);
    /// <summary>Počet celých mesačných predpisov, ktoré dlží.</summary>
    public int MesiacovDlhu
    {
        get
        {
            var mesacne = Mesiace.FirstOrDefault()?.Predpis ?? 0m;
            return mesacne > 0 ? (int)Math.Floor(Dlh / mesacne) : 0;
        }
    }
}

public static class KontoVlastnika
{
    /// <summary>
    /// Mesačný predpis = ročné zálohy / 12; poistné je splatné v januári a zostatok minulého roka hneď na začiatku roka.
    /// </summary>
    public static KontoPriestoru Vypocitaj(Priestor p, int rok, int kuMesiacu)
    {
        var pr = p.Roky.FirstOrDefault(r => r.Rok == rok);
        var zostatokMinuly = pr?.ZostatokMinulyRok ?? 0m;
        var poistne = pr?.PoistnePredpis ?? 0m;
        var rocne = p.Predpisy.Where(x => x.Rok == rok).Sum(x => x.RocnaSuma);
        var mesacne = Math.Round(rocne / 12m, 2, MidpointRounding.AwayFromZero);

        var mesiace = new List<MesiacKonta>();
        var zostatok = zostatokMinuly;
        for (int m = 1; m <= 12; m++)
        {
            // posledný mesiac dorovná zaokrúhlenie, aby súčet sedel na ročný predpis
            var predpis = (m == 12 ? rocne - mesacne * 11 : mesacne) + (m == 1 ? poistne : 0m);
            var zaplatene = p.Uhrady.Where(u => u.Datum.Year == rok && u.Datum.Month == m).Sum(u => u.Suma);
            zostatok += zaplatene - predpis;
            mesiace.Add(new MesiacKonta(m, predpis, zaplatene, zostatok));
        }
        return new KontoPriestoru(p, rok, Math.Clamp(kuMesiacu, 1, 12), zostatokMinuly, poistne, mesiace);
    }

    /// <summary>Predvolený mesiac: aktuálny, ak je rok bežný; inak december.</summary>
    public static int PredvolenyMesiac(int rok)
    {
        var dnes = DateTime.Today;
        return rok == dnes.Year ? dnes.Month : rok < dnes.Year ? 12 : 1;
    }
}
