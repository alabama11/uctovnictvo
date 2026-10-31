using System.Globalization;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Riadok peňažného denníka: bankový pohyb alebo pokladničný doklad.</summary>
public record RiadokDennika(DateOnly Datum, string Doklad, string Popis, decimal Suma, KategoriaPohybu? Kategoria, string Zdroj, object Polozka)
{
    public decimal Prijem => Suma > 0 ? Suma : 0;
    public decimal Vydavok => Suma < 0 ? -Suma : 0;
}

public record SucetKategorie(string Nazov, DruhKategorie Druh, bool Danovy, decimal Suma);

public record SuhrnDanovejEvidencie(
    int Rok,
    IReadOnlyList<SucetKategorie> Kategorie,
    decimal DanovePrijmy,
    decimal DanoveVydavkyPeniazne,
    decimal Odpisy,
    decimal CestovneNahrady,
    decimal NedanoveVydavky,
    decimal Nezaradene,
    IReadOnlyList<Faktura> Pohladavky,
    IReadOnlyList<Faktura> Zavazky)
{
    public decimal DanoveVydavky => DanoveVydavkyPeniazne + Odpisy + CestovneNahrady;
    public decimal Vysledok => DanovePrijmy - DanoveVydavky;
}

/// <summary>
/// Daňová evidencia podľa § 6 ods. 11 zákona o dani z príjmov: príjmy a výdavky (peňažný denník z banky a pokladne),
/// majetok a odpisy, pohľadávky a záväzky. Príjem a výdavok sa evidujú pri zaplatení.
/// </summary>
public static class DanovaEvidencia
{
    public static List<KategoriaPohybu> PredvoleneKategorie(int jednotkaId)
    {
        var zoznam = new (string Nazov, DruhKategorie Druh, bool Danovy, string Kod)[]
        {
            ("Tržby z faktúr", DruhKategorie.Prijem, true, "trzby"),
            ("Tržby v hotovosti", DruhKategorie.Prijem, true, "trzby-hotovost"),
            ("Iné príjmy z podnikania", DruhKategorie.Prijem, true, "ine-prijmy"),
            ("Materiál a tovar", DruhKategorie.Vydavok, true, "material"),
            ("Služby", DruhKategorie.Vydavok, true, "sluzby"),
            ("Nájomné", DruhKategorie.Vydavok, true, "najom"),
            ("Energie, telefón, internet", DruhKategorie.Vydavok, true, "energie"),
            ("Pohonné hmoty a auto", DruhKategorie.Vydavok, true, "auto"),
            ("Bankové poplatky", DruhKategorie.Vydavok, true, "poplatky"),
            ("Poistné na sociálne a zdravotné poistenie", DruhKategorie.Vydavok, true, "poistne"),
            ("Mzdy a odvody zamestnancov", DruhKategorie.Vydavok, true, "mzdy"),
            ("Ostatné daňové výdavky", DruhKategorie.Vydavok, true, "ostatne"),
            ("Nedaňové výdavky (súkromné, pokuty…)", DruhKategorie.Vydavok, false, "nedanove"),
            ("Daň z príjmov", DruhKategorie.Vydavok, false, "dan"),
            ("Nákup dlhodobého majetku (odpisuje sa)", DruhKategorie.Neutralny, false, "majetok"),
            ("Vklad / výber podnikateľa", DruhKategorie.Neutralny, false, "vklad"),
            ("Prevod banka – pokladňa", DruhKategorie.Neutralny, false, "prevod"),
            ("Úver, pôžička", DruhKategorie.Neutralny, false, "uver"),
        };
        return zoznam.Select((x, i) => new KategoriaPohybu
        {
            JednotkaId = jednotkaId, Nazov = x.Nazov, Druh = x.Druh, Danovy = x.Danovy, Kod = x.Kod, Poradie = i + 1,
        }).ToList();
    }

    /// <summary>
    /// Navrhne kategóriu nezaradeného pohybu: podľa uhradenej faktúry (jej kategória, inak tržby / materiál),
    /// bankový poplatok podľa popisu. Ak nič nesedí, vráti null.
    /// </summary>
    public static KategoriaPohybu? Navrhni(decimal suma, IEnumerable<UhradaFaktury> uhrady, string? popis, IReadOnlyList<KategoriaPohybu> kategorie)
    {
        KategoriaPohybu? Kod(string k) => kategorie.FirstOrDefault(x => x.Kod == k);
        var f = uhrady.Select(u => u.Faktura).FirstOrDefault(x => x is not null);
        if (f is not null)
        {
            if (f.KategoriaPohybu is { } k) return k;
            if (f.KategoriaPohybuId is { } id && kategorie.FirstOrDefault(x => x.Id == id) is { } k2) return k2;
            return f.Druh == DruhFaktury.Vydana ? Kod("trzby") : Kod("material");
        }
        var p = (popis ?? "").ToLowerInvariant();
        if (suma < 0 && (p.Contains("poplatok") || p.Contains("vedenie úč") || p.Contains("vedenie uc"))) return Kod("poplatky");
        if (suma < 0 && (p.Contains("socialna poist") || p.Contains("sociálna poist") || p.Contains("zdravotn") || p.Contains("vszp") || p.Contains("dôvera") || p.Contains("dovera") || p.Contains("union")))
            return Kod("poistne");
        return null;
    }

    /// <summary>Doplní kategórie všetkým nezaradeným pohybom a dokladom, kde sa dá; vráti počet zaradených.</summary>
    public static int Zarad(IEnumerable<BankovyPohyb> pohyby, IEnumerable<PokladnicnyDoklad> doklady, IReadOnlyList<KategoriaPohybu> kategorie)
    {
        var n = 0;
        foreach (var p in pohyby.Where(p => p.Kategoria is null && p.KategoriaId is null))
            if (Navrhni(p.Suma, p.Uhrady, p.Popis, kategorie) is { } k) { p.Kategoria = k; p.KategoriaId = k.Id == 0 ? null : k.Id; n++; }
        foreach (var d in doklady.Where(d => d.Kategoria is null && d.KategoriaId is null))
        {
            var suma = d.Druh == DruhPokladnicnehoDokladu.Prijem ? d.Suma : -d.Suma;
            if (Navrhni(suma, d.Uhrady, d.Ucel, kategorie) is { } k) { d.Kategoria = k; d.KategoriaId = k.Id == 0 ? null : k.Id; n++; }
        }
        return n;
    }

    static KategoriaPohybu? Kat(KategoriaPohybu? k, int? id, IReadOnlyList<KategoriaPohybu> kategorie) =>
        k ?? (id is { } i ? kategorie.FirstOrDefault(x => x.Id == i) : null);

    public static List<RiadokDennika> Dennik(IEnumerable<BankovyPohyb> pohyby, IEnumerable<PokladnicnyDoklad> doklady, IReadOnlyList<KategoriaPohybu> kategorie, int rok)
    {
        var riadky = new List<RiadokDennika>();
        foreach (var p in pohyby.Where(p => p.Datum.Year == rok))
        {
            var popis = string.Join(" – ", new[] { p.ProtiNazov, p.Popis, p.Uhrady.Count > 0 ? "fa " + string.Join(", ", p.Uhrady.Select(u => u.Faktura?.Cislo)) : null }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            riadky.Add(new RiadokDennika(p.Datum, "banka", popis, p.Suma, Kat(p.Kategoria, p.KategoriaId, kategorie), "Banka", p));
        }
        foreach (var d in doklady.Where(d => d.Datum.Year == rok))
        {
            var popis = string.IsNullOrWhiteSpace(d.Osoba) ? d.Ucel : $"{d.Ucel} – {d.Osoba}";
            riadky.Add(new RiadokDennika(d.Datum, d.Cislo, popis, d.Druh == DruhPokladnicnehoDokladu.Prijem ? d.Suma : -d.Suma,
                Kat(d.Kategoria, d.KategoriaId, kategorie), "Pokladňa", d));
        }
        return riadky.OrderBy(r => r.Datum).ThenBy(r => r.Doklad, StringComparer.Ordinal).ToList();
    }

    public static SuhrnDanovejEvidencie Suhrn(IReadOnlyList<RiadokDennika> dennik, IEnumerable<Majetok> majetok, decimal cestovneNahrady,
        IEnumerable<Faktura> faktury, int rok)
    {
        var sucty = dennik.Where(r => r.Kategoria is not null)
            .GroupBy(r => r.Kategoria!)
            .OrderBy(g => g.Key.Druh).ThenBy(g => g.Key.Poradie)
            .Select(g => new SucetKategorie(g.Key.Nazov, g.Key.Druh, g.Key.Danovy,
                g.Key.Druh == DruhKategorie.Vydavok ? -g.Sum(r => r.Suma) : g.Sum(r => r.Suma)))
            .ToList();
        // príjem zaradený do výdavkovej kategórie (napr. vrátený preplatok) znižuje výdavky, a naopak
        var danovePrijmy = dennik.Where(r => r.Kategoria is { Druh: DruhKategorie.Prijem, Danovy: true }).Sum(r => r.Suma);
        var danoveVydavky = -dennik.Where(r => r.Kategoria is { Druh: DruhKategorie.Vydavok, Danovy: true }).Sum(r => r.Suma);
        var nedanove = -dennik.Where(r => r.Kategoria is { Druh: DruhKategorie.Vydavok, Danovy: false }).Sum(r => r.Suma);
        var nezaradene = dennik.Where(r => r.Kategoria is null).Sum(r => Math.Abs(r.Suma));
        var odpisy = majetok.Select(m => Odpisy.ZaRok(m, rok)?.UplatnenyOdpis ?? 0).Sum();

        var koniec = new DateOnly(rok, 12, 31);
        bool Otvorena(Faktura f) => f.DatumVystavenia <= koniec && f.Suma - f.Uhrady.Where(u => u.Datum <= koniec).Sum(u => u.Suma) != 0;
        var pohladavky = faktury.Where(f => f.Druh == DruhFaktury.Vydana && Otvorena(f)).OrderBy(f => f.DatumVystavenia).ToList();
        var zavazky = faktury.Where(f => f.Druh == DruhFaktury.Prijata && Otvorena(f)).OrderBy(f => f.DatumVystavenia).ToList();
        return new SuhrnDanovejEvidencie(rok, sucty, danovePrijmy, danoveVydavky, odpisy, cestovneNahrady, nedanove, nezaradene, pohladavky, zavazky);
    }

    /// <summary>Zostatok faktúry k dátumu (na zostavu pohľadávok a záväzkov ku koncu roka).</summary>
    public static decimal ZostatokK(Faktura f, DateOnly datum) => f.Suma - f.Uhrady.Where(u => u.Datum <= datum).Sum(u => u.Suma);
}

public record SadzbaKm(DateOnly Od, decimal Sadzba);

public record MesiacJazd(int Mesiac, decimal KmSluzobne, decimal KmSukromne, decimal ZakladnaNahrada, decimal NahradaPhm)
{
    public decimal Spolu => ZakladnaNahrada + NahradaPhm;
}

/// <summary>
/// Kniha jázd a náhrady pri použití vlastného vozidla na podnikanie (§ 7 zákona o cestovných náhradách):
/// základná náhrada za km podľa oznámenia MPSVR + náhrada za pohonné látky (km × spotreba z TP / 100 × cena paliva).
/// Pri vozidle v obchodnom majetku sa náhrady neuplatňujú (výdavky sú skutočné), kniha slúži ako evidencia.
/// </summary>
public static class KnihaJazd
{
    /// <summary>Overené sadzby základnej náhrady pre osobné vozidlá: 0,296 €/km od 1. 6. 2025, 0,313 €/km od 1. 1. 2026.</summary>
    public static readonly IReadOnlyList<SadzbaKm> PredvoleneSadzby = new[]
    {
        new SadzbaKm(new DateOnly(2025, 6, 1), 0.296m),
        new SadzbaKm(new DateOnly(2026, 1, 1), 0.313m),
    };

    public static decimal? Sadzba(DateOnly datum, IReadOnlyList<SadzbaKm> sadzby) =>
        sadzby.Where(s => s.Od <= datum).OrderByDescending(s => s.Od).Select(s => (decimal?)s.Sadzba).FirstOrDefault();

    /// <summary>Formát pre nastavenia: „2025-06-01=0,296; 2026-01-01=0,313“.</summary>
    public static string ZapisSadzby(IEnumerable<SadzbaKm> s) =>
        string.Join("; ", s.OrderBy(x => x.Od).Select(x => $"{x.Od:yyyy-MM-dd}={x.Sadzba.ToString(CultureInfo.GetCultureInfo("sk-SK"))}"));

    public static List<SadzbaKm>? CitajSadzby(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var vysledok = new List<SadzbaKm>();
        foreach (var cast in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = cast.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2 || !DateOnly.TryParse(kv[0], CultureInfo.InvariantCulture, out var od)
                || !decimal.TryParse(kv[1].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var sadzba))
                return null;
            vysledok.Add(new SadzbaKm(od, sadzba));
        }
        return vysledok;
    }

    static decimal HoreNaCenty(decimal d) => Math.Ceiling(d * 100m) / 100m;

    public static (decimal Zakladna, decimal Phm, bool ChybaSadzba) Nahrada(Jazda j, Vozidlo v, IReadOnlyList<SadzbaKm> sadzby)
    {
        if (j.Sukromna || v.VObchodnomMajetku) return (0, 0, false);
        var km = Math.Ceiling(j.Km); // každý, aj začatý kilometer
        var s = Sadzba(j.Datum, sadzby);
        var zakladna = s is { } x ? HoreNaCenty(km * x) : 0;
        var cena = j.CenaPaliva ?? v.CenaPaliva;
        var phm = HoreNaCenty(km * v.Spotreba / 100m * cena);
        return (zakladna, phm, s is null);
    }

    public static List<MesiacJazd> Prehlad(Vozidlo v, int rok, IReadOnlyList<SadzbaKm> sadzby) =>
        Enumerable.Range(1, 12).Select(m =>
        {
            var jazdy = v.Jazdy.Where(j => j.Datum.Year == rok && j.Datum.Month == m).ToList();
            var n = jazdy.Select(j => Nahrada(j, v, sadzby)).ToList();
            return new MesiacJazd(m, jazdy.Where(j => !j.Sukromna).Sum(j => j.Km), jazdy.Where(j => j.Sukromna).Sum(j => j.Km),
                n.Sum(x => x.Zakladna), n.Sum(x => x.Phm));
        }).ToList();

    /// <summary>Cestovné náhrady za rok zo všetkých vozidiel mimo obchodného majetku (daňový výdavok).</summary>
    public static decimal NahradyZaRok(IEnumerable<Vozidlo> vozidla, int rok, IReadOnlyList<SadzbaKm> sadzby) =>
        vozidla.Where(v => !v.VObchodnomMajetku).Sum(v => Prehlad(v, rok, sadzby).Sum(m => m.Spolu));
}
