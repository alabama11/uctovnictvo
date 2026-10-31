using System.Text;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Číselné rady, nová faktúra, kópia, dobropis, úhrady.</summary>
public static class Faktury
{
    /// <summary>Predpona číselného radu podľa druhu a typu dokladu (vydaná faktúra nemá predponu).</summary>
    public static string Predpona(DruhFaktury druh, TypFaktury typ) => (druh, typ) switch
    {
        (DruhFaktury.Prijata, _) => "PF",
        (_, TypFaktury.Zalohova) => "ZF",
        (_, TypFaktury.Dobropis) => "DB",
        _ => "",
    };

    /// <summary>
    /// Ďalšie číslo v rade. Formát: RRRR = rok, RR = rok dvojmiestne, # = poradové číslo (počet # = počet číslic),
    /// ostatné znaky sa zachovajú. Napr. „RRRR####“ → 20260001, „FA-RR/###“ → FA-26/001.
    /// </summary>
    public static string DalsieCislo(string format, int rok, IEnumerable<string> existujuce, string predpona = "")
    {
        if (string.IsNullOrWhiteSpace(format)) format = "RRRR####";
        if (!format.Contains('#')) format += "###";
        var maska = predpona + format.Replace("RRRR", rok.ToString("0000")).Replace("RR", (rok % 100).ToString("00"));
        var zaciatok = maska.IndexOf('#');
        var dlzka = maska.Count(c => c == '#');
        var pred = maska[..zaciatok];
        var za = maska[(zaciatok + dlzka)..];
        var max = 0;
        foreach (var c in existujuce)
        {
            if (c.Length < pred.Length + za.Length || !c.StartsWith(pred) || !c.EndsWith(za)) continue;
            var stred = c[pred.Length..^za.Length];
            if (stred.Length > 0 && stred.All(char.IsDigit) && int.TryParse(stred, out var n)) max = Math.Max(max, n);
        }
        return pred + (max + 1).ToString(new string('0', dlzka)) + za;
    }

    /// <summary>Variabilný symbol z čísla faktúry: len číslice, najviac 10.</summary>
    public static string VsZCisla(string cislo)
    {
        var cislice = new string(cislo.Where(char.IsDigit).ToArray());
        return cislice.Length > 10 ? cislice[^10..] : cislice;
    }

    public static Faktura Nova(UctovnaJednotka j, DruhFaktury druh, TypFaktury typ, DateOnly datum, IEnumerable<string> existujuceCisla)
    {
        var cislo = DalsieCislo(j.FormatCislaFaktury, datum.Year, existujuceCisla, Predpona(druh, typ));
        return new Faktura
        {
            JednotkaId = j.Id,
            Jednotka = j,
            Druh = druh,
            Typ = typ,
            Cislo = cislo,
            VariabilnySymbol = druh == DruhFaktury.Vydana ? VsZCisla(cislo) : null,
            DatumVystavenia = datum,
            DatumDodania = typ == TypFaktury.Zalohova ? null : datum,
            DatumSplatnosti = datum.AddDays(j.SplatnostDni <= 0 ? 14 : j.SplatnostDni),
        };
    }

    public static void NastavPartnera(Faktura f, Partner? p)
    {
        f.Partner = p;
        f.PartnerId = p?.Id;
        if (p is null) return;
        f.PartnerNazov = p.Nazov;
        f.PartnerAdresa = p.Adresa;
        f.PartnerIco = p.Ico;
        f.PartnerDic = p.Dic;
        f.PartnerIcDph = p.IcDph;
        f.PartnerIban = p.Iban;
    }

    static List<PolozkaFaktury> KopiaPoloziek(Faktura f, decimal znamienko) =>
        f.Polozky.OrderBy(p => p.Poradie).Select(p => new PolozkaFaktury
        {
            Poradie = p.Poradie,
            Popis = p.Popis,
            Mnozstvo = p.Mnozstvo,
            Jednotka = p.Jednotka,
            CenaZaJednotku = p.CenaZaJednotku * znamienko,
        }).ToList();

    /// <summary>Nová faktúra s rovnakým partnerom a položkami, dnešným dátumom a ďalším číslom.</summary>
    public static Faktura Kopia(Faktura zdroj, UctovnaJednotka j, DateOnly datum, IEnumerable<string> existujuceCisla)
    {
        var f = Nova(j, zdroj.Druh, zdroj.Typ == TypFaktury.Dobropis ? TypFaktury.Faktura : zdroj.Typ, datum, existujuceCisla);
        KopirujPartnera(zdroj, f);
        f.FormaUhrady = zdroj.FormaUhrady;
        f.KonstantnySymbol = zdroj.KonstantnySymbol;
        f.TextNad = zdroj.TextNad;
        f.TextPod = zdroj.TextPod;
        f.KategoriaNakladovId = zdroj.KategoriaNakladovId;
        f.Polozky = KopiaPoloziek(zdroj, 1);
        return f;
    }

    /// <summary>Dobropis k faktúre: rovnaké položky so zápornou cenou, odkaz na pôvodnú faktúru.</summary>
    public static Faktura Dobropis(Faktura zdroj, UctovnaJednotka j, DateOnly datum, IEnumerable<string> existujuceCisla)
    {
        var f = Nova(j, zdroj.Druh, TypFaktury.Dobropis, datum, existujuceCisla);
        KopirujPartnera(zdroj, f);
        f.PovodnaFaktura = zdroj.Cislo;
        f.FormaUhrady = zdroj.FormaUhrady;
        f.TextNad = $"Opravný doklad (dobropis) k faktúre č. {zdroj.Cislo} zo dňa {zdroj.DatumVystavenia:d.M.yyyy}.";
        f.Polozky = KopiaPoloziek(zdroj, -1);
        return f;
    }

    /// <summary>
    /// Vyúčtovacia faktúra k zaplatenej zálohovej faktúre: rovnaké položky a riadok „Odpočet uhradenej zálohy“.
    /// </summary>
    public static Faktura ZoZalohovej(Faktura zalohova, UctovnaJednotka j, DateOnly datum, IEnumerable<string> existujuceCisla)
    {
        var f = Nova(j, zalohova.Druh, TypFaktury.Faktura, datum, existujuceCisla);
        KopirujPartnera(zalohova, f);
        f.FormaUhrady = zalohova.FormaUhrady;
        f.Polozky = KopiaPoloziek(zalohova, 1);
        if (zalohova.Uhradene != 0)
            f.Polozky.Add(new PolozkaFaktury
            {
                Poradie = f.Polozky.Count + 1,
                Popis = $"Odpočet uhradenej zálohy (zálohová faktúra č. {zalohova.Cislo})",
                Mnozstvo = 1,
                CenaZaJednotku = -zalohova.Uhradene,
            });
        return f;
    }

    static void KopirujPartnera(Faktura z, Faktura f)
    {
        f.PartnerId = z.PartnerId;
        f.Partner = z.Partner;
        f.PartnerNazov = z.PartnerNazov;
        f.PartnerAdresa = z.PartnerAdresa;
        f.PartnerIco = z.PartnerIco;
        f.PartnerDic = z.PartnerDic;
        f.PartnerIcDph = z.PartnerIcDph;
        f.PartnerIban = z.PartnerIban;
    }

    public static string Stav(Faktura f, DateOnly dnes)
    {
        if (f.Suma == 0) return "nulová";
        if (f.Zostava == 0) return "uhradená";
        if (f.Suma > 0 && f.Zostava < 0 || f.Suma < 0 && f.Zostava > 0) return "preplatená";
        if (f.Uhradene != 0) return dnes > f.DatumSplatnosti ? "čiastočne, po splatnosti" : "čiastočne uhradená";
        return dnes > f.DatumSplatnosti ? $"po splatnosti {dnes.DayNumber - f.DatumSplatnosti.DayNumber} dní" : "neuhradená";
    }

    public static string NazovTypu(TypFaktury t) => t switch
    {
        TypFaktury.Zalohova => "Zálohová faktúra",
        TypFaktury.Dobropis => "Dobropis",
        _ => "Faktúra",
    };

    public static string NazovFormy(FormaUhrady f) => f switch
    {
        FormaUhrady.Hotovost => "hotovosť",
        FormaUhrady.Karta => "platobná karta",
        FormaUhrady.Dobierka => "dobierka",
        _ => "prevodný príkaz",
    };

    /// <summary>Súčet prijatých faktúr spoločenstva podľa kategórie nákladov a dátumu dodania (alebo vystavenia) v roku.</summary>
    public static Dictionary<int, decimal> NakladyZFaktur(IEnumerable<Faktura> faktury, int rok) =>
        faktury.Where(f => f.Druh == DruhFaktury.Prijata && f.KategoriaNakladovId is not null
                           && (f.DatumDodania ?? f.DatumVystavenia).Year == rok)
            .GroupBy(f => f.KategoriaNakladovId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.Suma));

    /// <summary>Text e-mailu k faktúre so zástupnými znakmi {cislo}, {suma}, {splatnost}, {vs}.</summary>
    public static string NahradZnaky(string text, Faktura f) => new StringBuilder(text)
        .Replace("{cislo}", f.Cislo)
        .Replace("{suma}", PdfVyuctovanie.Eur(f.Zostava != 0 ? f.Zostava : f.Suma))
        .Replace("{splatnost}", f.DatumSplatnosti.ToString("d.M.yyyy"))
        .Replace("{vs}", f.VariabilnySymbol ?? "")
        .ToString();
}
