using System.Globalization;
using System.Xml.Linq;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public record NavrhParovania(BankovyPohyb Pohyb, Faktura? Faktura, string Dovod);

/// <summary>Bankové pohyby firmy: import celého výpisu camt.053 (príjmy aj výdavky) a párovanie na faktúry.</summary>
public static class Banka
{
    public static List<BankovyPohyb> NacitajCamt053(string path) => NacitajCamt053(XDocument.Load(path));

    public static List<BankovyPohyb> NacitajCamt053(XDocument doc)
    {
        var vysledok = new List<BankovyPohyb>();
        foreach (var ntry in doc.Descendants().Where(e => e.Name.LocalName == "Ntry"))
        {
            if (Text(ntry, "RvslInd") == "true") continue;
            var prijem = Text(ntry, "CdtDbtInd") == "CRDT";
            var suma = decimal.Parse(Text(ntry, "Amt") ?? "0", CultureInfo.InvariantCulture);
            var datumText = Text(Prve(ntry, "BookgDt"), "Dt") ?? Text(Prve(ntry, "BookgDt"), "DtTm")
                            ?? Text(Prve(ntry, "ValDt"), "Dt") ?? "";
            var datum = DateOnly.FromDateTime(DateTime.Parse(datumText, CultureInfo.InvariantCulture));

            // pri príjme je protistranou platiteľ (Dbtr), pri výdavku príjemca (Cdtr)
            var ucet = Prve(ntry, prijem ? "DbtrAcct" : "CdtrAcct");
            var iban = Text(ucet, "IBAN");
            var meno = Text(Prve(ntry, prijem ? "Dbtr" : "Cdtr"), "Nm");
            var e2e = Text(ntry, "EndToEndId");
            var sprava = string.Join(" ", ntry.Descendants().Where(x => x.Name.LocalName == "Ustrd").Select(e => e.Value.Trim()));
            var refInf = Text(Prve(ntry, "CdtrRefInf"), "Ref");
            var referencia = Text(ntry, "AcctSvcrRef") ?? Text(ntry, "NtryRef");

            vysledok.Add(new BankovyPohyb
            {
                Datum = datum,
                Suma = prijem ? suma : -suma,
                ProtiIban = iban is null ? null : Iban.Normalizuj(iban),
                ProtiNazov = meno,
                VariabilnySymbol = ImportBanky.NajdiVs(e2e) ?? ImportBanky.NajdiVs(refInf) ?? ImportBanky.NajdiVs(sprava),
                Popis = string.IsNullOrWhiteSpace(sprava) ? null : sprava,
                Referencia = referencia ?? $"{datum:yyyyMMdd}|{(prijem ? suma : -suma).ToString(CultureInfo.InvariantCulture)}|{iban}|{e2e}",
            });
        }
        return vysledok;
    }

    /// <summary>
    /// Návrh priradenia pohybov k neuhradeným faktúram: príjem → vydaná faktúra, výdavok → prijatá faktúra.
    /// Najprv podľa VS, potom podľa IBAN partnera a presnej sumy.
    /// </summary>
    public static List<NavrhParovania> Paruj(IEnumerable<BankovyPohyb> pohyby, IEnumerable<Faktura> faktury)
    {
        var otvorene = faktury.Where(f => f.Zostava != 0).ToList();
        var pouzite = new HashSet<Faktura>();
        var vysledok = new List<NavrhParovania>();
        foreach (var p in pohyby)
        {
            if (p.Uhrady.Count > 0) continue;
            var druh = p.Suma > 0 ? DruhFaktury.Vydana : DruhFaktury.Prijata;
            var kandidati = otvorene.Where(f => f.Druh == druh && !pouzite.Contains(f)).ToList();
            var suma = Math.Abs(p.Suma);
            Faktura? zhoda = null;
            string dovod = "nenašla sa zhoda";

            if (p.VariabilnySymbol is { } vs)
            {
                var podlaVs = kandidati.Where(f => (f.VariabilnySymbol ?? "").TrimStart('0') == vs.TrimStart('0')).ToList();
                if (podlaVs.Count == 1) { zhoda = podlaVs[0]; dovod = $"VS {vs}"; }
                else if (podlaVs.Count > 1)
                {
                    var presne = podlaVs.Where(f => Math.Abs(f.Zostava) == suma).ToList();
                    if (presne.Count == 1) { zhoda = presne[0]; dovod = $"VS {vs} a suma"; }
                    else dovod = $"VS {vs} majú viaceré faktúry";
                }
            }
            if (zhoda is null && p.ProtiIban is { } iban)
            {
                var podlaUctu = kandidati.Where(f => f.PartnerIban is not null && Iban.Normalizuj(f.PartnerIban) == iban
                                                     && Math.Abs(f.Zostava) == suma).ToList();
                if (podlaUctu.Count == 1) { zhoda = podlaUctu[0]; dovod = "účet partnera a suma"; }
            }
            if (zhoda is not null) pouzite.Add(zhoda);
            vysledok.Add(new NavrhParovania(p, zhoda, dovod));
        }
        return vysledok;
    }

    /// <summary>Zapíše úhradu faktúry z bankového pohybu (najviac do výšky zostatku faktúry).</summary>
    public static UhradaFaktury Uhrad(Faktura f, BankovyPohyb p)
    {
        var suma = f.Druh == DruhFaktury.Vydana ? p.Suma : -p.Suma;
        var u = new UhradaFaktury { Faktura = f, FakturaId = f.Id, Datum = p.Datum, Suma = suma, BankovyPohyb = p, BankovyPohybId = p.Id == 0 ? null : p.Id };
        f.Uhrady.Add(u);
        p.Uhrady.Add(u);
        return u;
    }

    static XElement? Prve(XElement? e, string name) => e?.Descendants().FirstOrDefault(x => x.Name.LocalName == name);
    static string? Text(XElement? e, string name) => Prve(e, name)?.Value.Trim();
}

public record RiadokPokladne(PokladnicnyDoklad Doklad, decimal Prijem, decimal Vydavok, decimal Zostatok);

/// <summary>Pokladňa: číslovanie dokladov a pokladničná kniha so zostatkom.</summary>
public static class Pokladna
{
    public static string DalsieCislo(DruhPokladnicnehoDokladu druh, int rok, IEnumerable<string> existujuce) =>
        Faktury.DalsieCislo("RRRR###", rok, existujuce, druh == DruhPokladnicnehoDokladu.Prijem ? "PPD" : "VPD");

    /// <summary>Pokladničná kniha za rok. Zostatok na začiatku = počiatočný stav + všetky doklady pred rokom.</summary>
    public static (decimal Zaciatok, List<RiadokPokladne> Riadky) Kniha(IEnumerable<PokladnicnyDoklad> doklady, decimal pociatocnyStav, int rok)
    {
        var vsetky = doklady.OrderBy(d => d.Datum).ThenBy(d => d.Druh).ThenBy(d => d.Cislo).ToList();
        var zaciatok = pociatocnyStav + vsetky.Where(d => d.Datum.Year < rok).Sum(Zmena);
        var stav = zaciatok;
        var riadky = new List<RiadokPokladne>();
        foreach (var d in vsetky.Where(d => d.Datum.Year == rok))
        {
            stav += Zmena(d);
            riadky.Add(new RiadokPokladne(d, d.Druh == DruhPokladnicnehoDokladu.Prijem ? d.Suma : 0,
                d.Druh == DruhPokladnicnehoDokladu.Vydavok ? d.Suma : 0, stav));
        }
        return (zaciatok, riadky);
    }

    /// <summary>Prvý doklad, po ktorom by bola pokladňa v mínuse (to sa nesmie stať).</summary>
    public static RiadokPokladne? PrvyZapornyZostatok(IEnumerable<PokladnicnyDoklad> doklady, decimal pociatocnyStav, int rok) =>
        Kniha(doklady, pociatocnyStav, rok).Riadky.FirstOrDefault(r => r.Zostatok < 0);

    static decimal Zmena(PokladnicnyDoklad d) => d.Druh == DruhPokladnicnehoDokladu.Prijem ? d.Suma : -d.Suma;

    /// <summary>Pokladničný doklad k hotovostnej úhrade faktúry.</summary>
    public static PokladnicnyDoklad ZFaktury(Faktura f, DateOnly datum, IEnumerable<string> existujuce)
    {
        var druh = (f.Druh == DruhFaktury.Vydana) == (f.Zostava > 0) ? DruhPokladnicnehoDokladu.Prijem : DruhPokladnicnehoDokladu.Vydavok;
        var d = new PokladnicnyDoklad
        {
            JednotkaId = f.JednotkaId,
            Druh = druh,
            Cislo = DalsieCislo(druh, datum.Year, existujuce),
            Datum = datum,
            Suma = Math.Abs(f.Zostava),
            Ucel = $"Úhrada faktúry č. {(f.Druh == DruhFaktury.Prijata && !string.IsNullOrWhiteSpace(f.CisloDodavatela) ? f.CisloDodavatela : f.Cislo)}",
            Osoba = f.PartnerNazov,
        };
        var u = new UhradaFaktury { Faktura = f, FakturaId = f.Id, Datum = datum, Suma = f.Zostava, PokladnicnyDoklad = d };
        f.Uhrady.Add(u);
        d.Uhrady.Add(u);
        return d;
    }
}
