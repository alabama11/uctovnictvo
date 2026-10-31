using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Časť 3: daňová evidencia (peňažný denník), majetok a odpisy, kniha jázd.</summary>
public partial class MainViewModel
{
    public ObservableCollection<KategoriaPohybu> KategoriePohybov { get; } = new();
    public ObservableCollection<DennikVm> Dennik { get; } = new();
    public ObservableCollection<MajetokVm> MajetokZoznam { get; } = new();
    public ObservableCollection<OdpisRoka> OdpisovyPlan { get; } = new();
    public ObservableCollection<Vozidlo> Vozidla { get; } = new();
    public ObservableCollection<JazdaVm> Jazdy { get; } = new();

    List<Majetok> vsetokMajetok = new();

    [ObservableProperty] private DennikVm? vybranyRiadokDennika;
    [ObservableProperty] private bool lenNezaradene;
    [ObservableProperty] private string suhrnDanovejEvidencie = "";
    [ObservableProperty] private MajetokVm? vybranyMajetok;
    [ObservableProperty] private Vozidlo? vybraneVozidlo;
    [ObservableProperty] private JazdaVm? vybranaJazda;
    [ObservableProperty] private string suhrnJazd = "";

    partial void OnLenNezaradeneChanged(bool value) => NacitajDennik();
    partial void OnVybranyMajetokChanged(MajetokVm? value) => ObnovPlan();
    partial void OnVybraneVozidloChanged(Vozidlo? value) => NacitajJazdy();

    IReadOnlyList<SadzbaKm> Sadzby() =>
        KnihaJazd.CitajSadzby(Core.Services.Nastavenia.Citaj(db, "kniha-jazd.sadzby")) ?? KnihaJazd.PredvoleneSadzby;

    void NacitajCast3()
    {
        KategoriePohybov.Clear(); Dennik.Clear(); MajetokZoznam.Clear(); OdpisovyPlan.Clear(); Vozidla.Clear(); Jazdy.Clear();
        vsetokMajetok = new();
        var j = VybranaJednotka;
        if (j is null) return;

        var kat = db.KategoriePohybov.Where(k => k.JednotkaId == j.Id).OrderBy(k => k.Poradie).ToList();
        if (kat.Count == 0 && j.Id != 0)
        {
            kat = DanovaEvidencia.PredvoleneKategorie(j.Id);
            db.KategoriePohybov.AddRange(kat);
        }
        foreach (var k in kat) KategoriePohybov.Add(k);

        vsetokMajetok = db.Majetok.Where(m => m.JednotkaId == j.Id).OrderBy(m => m.InventarneCislo).ToList();
        foreach (var m in vsetokMajetok.Where(m => m.DatumZaradenia.Year <= Rok && (m.DatumVyradenia is null || m.DatumVyradenia.Value.Year >= Rok)))
            MajetokZoznam.Add(new MajetokVm(m) { Rok = Rok });
        VybranyMajetok = MajetokZoznam.FirstOrDefault();

        var vybrane = VybraneVozidlo?.Id;
        foreach (var v in db.Vozidla.Where(v => v.JednotkaId == j.Id).Include(v => v.Jazdy).ToList()) Vozidla.Add(v);
        VybraneVozidlo = Vozidla.FirstOrDefault(v => v.Id == vybrane) ?? Vozidla.FirstOrDefault();
        NacitajDennik();
        NacitajCast4();
        NacitajCast5();
    }

    // ---------- peňažný denník ----------

    public void NacitajDennik()
    {
        Dennik.Clear();
        if (VybranaJednotka is null) return;
        var riadky = DanovaEvidencia.Dennik(vsetkyPohyby, vsetkyDoklady, KategoriePohybov, Rok);
        foreach (var r in riadky.Where(r => !LenNezaradene || r.Kategoria is null)) Dennik.Add(new DennikVm(r, KategoriePohybov, PrepocitajDanovuEvidenciu));
        PrepocitajDanovuEvidenciu();
    }

    SuhrnDanovejEvidencie VypocitajSuhrn(out List<RiadokDennika> riadky)
    {
        riadky = DanovaEvidencia.Dennik(vsetkyPohyby, vsetkyDoklady, KategoriePohybov, Rok);
        return DanovaEvidencia.Suhrn(riadky, vsetokMajetok, KnihaJazd.NahradyZaRok(Vozidla, Rok, Sadzby()), vsetkyFaktury, Rok);
    }

    public void PrepocitajDanovuEvidenciu()
    {
        if (VybranaJednotka is null) return;
        var s = VypocitajSuhrn(out _);
        SuhrnDanovejEvidencie = $"Príjmy: {PdfVyuctovanie.Eur(s.DanovePrijmy)}   Výdavky: {PdfVyuctovanie.Eur(s.DanoveVydavkyPeniazne)}" +
                                $" + odpisy {PdfVyuctovanie.Eur(s.Odpisy)} + cestovné {PdfVyuctovanie.Eur(s.CestovneNahrady)}   " +
                                $"Rozdiel: {PdfVyuctovanie.Eur(s.Vysledok)}" +
                                (s.Nezaradene != 0 ? $"   ⚠ Nezaradené: {PdfVyuctovanie.Eur(s.Nezaradene)}" : "");
        if (VstupyDp is not null && JeSzco) PrepocitajDp();
    }

    [RelayCommand]
    async Task ZaradAutomaticky()
    {
        var n = DanovaEvidencia.Zarad(vsetkyPohyby.Where(p => p.Datum.Year == Rok), vsetkyDoklady.Where(d => d.Datum.Year == Rok), KategoriePohybov);
        NacitajDennik();
        if (Dialogy is not null) await Dialogy.Sprava("Zaradiť automaticky", $"Zaradených pohybov: {n}. Ostatným vyberte kategóriu v stĺpci „Kategória“.");
    }

    [RelayCommand]
    async Task DanovaEvidenciaPdf()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Daňová evidencia (PDF)", $"danova-evidencia-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        var suhrn = VypocitajSuhrn(out var riadky);
        await File.WriteAllBytesAsync(s, PdfDanovaEvidencia.Vytvor(VybranaJednotka, suhrn, riadky, vsetokMajetok));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task DanovaEvidenciaExcel()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Daňová evidencia (Excel)", $"danova-evidencia-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        var suhrn = VypocitajSuhrn(out var riadky);
        ExportExcel.DanovaEvidencia(VybranaJednotka, suhrn, riadky, vsetokMajetok, s);
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task PridajKategoriuPohybu()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var nazov = await Dialogy.VstupnyText("Nová kategória", "Názov kategórie (začnite „+“ pre príjem, napr. „+ Provízie“):", "");
        if (string.IsNullOrWhiteSpace(nazov)) return;
        var prijem = nazov.TrimStart().StartsWith('+');
        var k = new KategoriaPohybu
        {
            JednotkaId = VybranaJednotka.Id, Nazov = nazov.Trim().TrimStart('+').Trim(),
            Druh = prijem ? DruhKategorie.Prijem : DruhKategorie.Vydavok, Danovy = true,
            Poradie = KategoriePohybov.Count == 0 ? 1 : KategoriePohybov.Max(x => x.Poradie) + 1,
        };
        db.KategoriePohybov.Add(k);
        KategoriePohybov.Add(k);
        Uloz();
    }

    // ---------- majetok ----------

    void ObnovPlan()
    {
        OdpisovyPlan.Clear();
        if (VybranyMajetok is null) return;
        foreach (var o in VybranyMajetok.Plan) OdpisovyPlan.Add(o);
    }

    public void MajetokZmeneny()
    {
        ObnovPlan();
        PrepocitajDanovuEvidenciu();
    }

    [RelayCommand]
    async Task PridajMajetok()
    {
        if (VybranaJednotka is null || Dialogy is null) return;
        var datum = DnesVRoku();
        var m = new Majetok
        {
            JednotkaId = VybranaJednotka.Id, Nazov = "Nový majetok", DatumZaradenia = datum, DatumObstarania = datum,
            InventarneCislo = Odpisy.DalsieInventarneCislo(datum.Year, vsetokMajetok.Select(x => x.InventarneCislo)), OdpisovaSkupina = 1,
        };
        db.Majetok.Add(m);
        vsetokMajetok.Add(m);
        var vm = new MajetokVm(m) { Rok = Rok };
        MajetokZoznam.Add(vm);
        VybranyMajetok = vm;
        await Task.CompletedTask;
    }

    [RelayCommand]
    async Task ZmazMajetok()
    {
        var m = VybranyMajetok;
        if (m is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať majetok", $"Zmazať „{m.Nazov}“? Pri predaji alebo likvidácii radšej vyplňte dátum vyradenia.")) return;
        db.Majetok.Remove(m.M);
        vsetokMajetok.Remove(m.M);
        MajetokZoznam.Remove(m);
        VybranyMajetok = MajetokZoznam.FirstOrDefault();
        PrepocitajDanovuEvidenciu();
    }

    // ---------- kniha jázd ----------

    void NacitajJazdy()
    {
        Jazdy.Clear();
        var v = VybraneVozidlo;
        if (v is not null)
            foreach (var j in v.Jazdy.Where(j => j.Datum.Year == Rok).OrderBy(j => j.Datum).ThenBy(j => j.Id))
                Jazdy.Add(new JazdaVm(j, v, Sadzby, PrepocitajJazdy));
        PrepocitajJazdy();
    }

    public void PrepocitajJazdy()
    {
        var v = VybraneVozidlo;
        if (v is null) { SuhrnJazd = ""; return; }
        var sadzby = Sadzby();
        var m = KnihaJazd.Prehlad(v, Rok, sadzby);
        var chyba = v.Jazdy.Any(j => j.Datum.Year == Rok && KnihaJazd.Nahrada(j, v, sadzby).ChybaSadzba);
        SuhrnJazd = $"Služobné km: {m.Sum(x => x.KmSluzobne):0.#}   Súkromné km: {m.Sum(x => x.KmSukromne):0.#}   " +
                    (v.VObchodnomMajetku ? "Vozidlo v obchodnom majetku – náhrady sa neuplatňujú." :
                        $"Základná náhrada: {PdfVyuctovanie.Eur(m.Sum(x => x.ZakladnaNahrada))}   PHM: {PdfVyuctovanie.Eur(m.Sum(x => x.NahradaPhm))}   " +
                        $"Spolu do výdavkov: {PdfVyuctovanie.Eur(m.Sum(x => x.Spolu))}") +
                    (chyba ? "   ⚠ Pre niektoré dátumy chýba sadzba za km – doplňte ju tlačidlom „Sadzby za km“." : "");
        PrepocitajDanovuEvidenciu();
    }

    [RelayCommand]
    void PridajVozidlo()
    {
        if (VybranaJednotka is null) return;
        var v = new Vozidlo { JednotkaId = VybranaJednotka.Id, Nazov = "Nové vozidlo", Palivo = "benzín" };
        db.Vozidla.Add(v);
        Vozidla.Add(v);
        VybraneVozidlo = v;
    }

    [RelayCommand]
    async Task ZmazVozidlo()
    {
        var v = VybraneVozidlo;
        if (v is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať vozidlo", $"Zmazať vozidlo „{v.Nazov}“ aj s {v.Jazdy.Count} jazdami?")) return;
        db.Vozidla.Remove(v);
        Vozidla.Remove(v);
        VybraneVozidlo = Vozidla.FirstOrDefault();
    }

    [RelayCommand]
    void PridajJazdu()
    {
        var v = VybraneVozidlo;
        if (v is null) return;
        var posledna = v.Jazdy.OrderBy(j => j.Datum).ThenBy(j => j.Id).LastOrDefault();
        var j = new Jazda
        {
            Vozidlo = v, Datum = DnesVRoku(), Odkial = posledna?.Kam ?? VybranaJednotka?.Obec ?? "", Kam = "",
            Vodic = posledna?.Vodic ?? VybranaJednotka?.Statutar,
        };
        v.Jazdy.Add(j);
        var vm = new JazdaVm(j, v, Sadzby, PrepocitajJazdy);
        Jazdy.Add(vm);
        VybranaJazda = vm;
    }

    [RelayCommand]
    void SpiatocnaJazda()
    {
        var v = VybraneVozidlo;
        var z = VybranaJazda?.J;
        if (v is null || z is null) return;
        var j = new Jazda { Vozidlo = v, Datum = z.Datum, Odkial = z.Kam, Kam = z.Odkial, Ucel = z.Ucel, Km = z.Km, Vodic = z.Vodic, Sukromna = z.Sukromna, CenaPaliva = z.CenaPaliva };
        v.Jazdy.Add(j);
        var vm = new JazdaVm(j, v, Sadzby, PrepocitajJazdy);
        Jazdy.Insert(Jazdy.IndexOf(VybranaJazda!) + 1, vm);
        VybranaJazda = vm;
        PrepocitajJazdy();
    }

    [RelayCommand]
    void ZmazJazdu()
    {
        var v = VybraneVozidlo;
        var j = VybranaJazda;
        if (v is null || j is null) return;
        v.Jazdy.Remove(j.J);
        if (j.J.Id != 0) db.Jazdy.Remove(j.J);
        Jazdy.Remove(j);
        PrepocitajJazdy();
    }

    [RelayCommand]
    async Task KnihaJazdPdf()
    {
        var v = VybraneVozidlo;
        if (v is null || Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Kniha jázd (PDF)", $"kniha-jazd-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        await File.WriteAllBytesAsync(s, PdfDanovaEvidencia.KnihaJazdPdf(VybranaJednotka, v, Rok, Sadzby()));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task SadzbyZaKm()
    {
        if (Dialogy is null) return;
        var text = await Dialogy.VstupnyText("Sadzby základnej náhrady za km",
            "Sadzby pre osobné vozidlá vo formáte „dátum od=sadzba“, oddelené bodkočiarkou. Zverejňuje ich MPSVR SR opatrením.",
            KnihaJazd.ZapisSadzby(Sadzby()));
        if (text is null) return;
        var s = KnihaJazd.CitajSadzby(text);
        if (s is null) { await Dialogy.Sprava("Sadzby", "Nesprávny formát. Príklad: 2025-06-01=0,296; 2026-01-01=0,313"); return; }
        Core.Services.Nastavenia.Zapis(db, "kniha-jazd.sadzby", KnihaJazd.ZapisSadzby(s));
        Uloz();
        NacitajJazdy();
    }
}
