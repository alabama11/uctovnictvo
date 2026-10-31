using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Časť 5: podklady k daňovému priznaniu (DP FO typ B pre SZČO, DP PO pre s.r.o.).</summary>
public partial class MainViewModel
{
    public ObservableCollection<RiadokDp> RiadkyDp { get; } = new();
    [ObservableProperty] private VstupyDpVm? vstupyDp;
    [ObservableProperty] private string infoDp = "";
    public bool JeSzco => VybranaJednotka?.Typ == TypJednotky.Szco;

    void NacitajCast5()
    {
        OnPropertyChanged(nameof(JeSzco));
        RiadkyDp.Clear();
        var j = VybranaJednotka;
        if (j is null || j.Typ == TypJednotky.Spolocenstvo) { VstupyDp = null; InfoDp = ""; return; }
        var v = Core.Services.VstupyDp.Citaj(Core.Services.Nastavenia.Citaj(db, Core.Services.VstupyDp.Kluc(j.Id, Rok)));
        VstupyDp = new VstupyDpVm(v, () =>
        {
            if (VybranaJednotka is { } jj) Core.Services.Nastavenia.Zapis(db, Core.Services.VstupyDp.Kluc(jj.Id, Rok), v.Zapis());
            PrepocitajDp();
        });
        PrepocitajDp();
    }

    decimal PoistneZDennika() =>
        -DanovaEvidencia.Dennik(vsetkyPohyby, vsetkyDoklady, KategoriePohybov, Rok).Where(r => r.Kategoria?.Kod == "poistne").Sum(r => r.Suma);

    public void PrepocitajDp()
    {
        RiadkyDp.Clear();
        var j = VybranaJednotka;
        if (j is null || VstupyDp is null) return;
        var v = VstupyDp.V;
        if (j.Typ == TypJednotky.Szco)
        {
            var p = DanovePriznanie.Fo(Rok);
            if (p is null) { InfoDp = NemameParametre(); return; }
            var s = VypocitajSuhrn(out _);
            var poistne = PoistneZDennika();
            VstupyDp.PoistneZDennika = poistne;
            var r = DanovePriznanie.VypocitajFo(p, s.DanovePrijmy, s.DanoveVydavky, poistne, v);
            foreach (var x in r.Riadky) RiadkyDp.Add(x);
            InfoDp = (r.PovinnostPodat ? $"Daňové priznanie typ B za {Rok} treba podať do 31. 3. {Rok + 1}." : $"Príjmy neprevyšujú {PdfVyuctovanie.Eur(Math.Round(p.Nczd / 2, 2, MidpointRounding.AwayFromZero))} – priznanie podávať nemusíte (ak nevykazujete stratu).") +
                     (s.Nezaradene != 0 ? $"  ⚠ V daňovej evidencii sú nezaradené pohyby ({PdfVyuctovanie.Eur(s.Nezaradene)}), výpočet nie je úplný." : "") +
                     (Rok == 2026 ? "  Odvody SZČO 2026: minimálne sociálne poistenie 303,11 € (vymeriavací základ 914,40 €), minimálny preddavok na zdravotné 121,92 € mesačne." : "");
        }
        else if (j.Typ == TypJednotky.Sro)
        {
            var p = DanovePriznanie.Po(Rok);
            if (p is null) { InfoDp = NemameParametre(); return; }
            var r = DanovePriznanie.VypocitajPo(p, Predvaha.ToList(), UctovyRozvrh.ToList(), v);
            foreach (var x in r.Riadky) RiadkyDp.Add(x);
            InfoDp = $"Daňové priznanie PO za {Rok} treba podať do 31. 3. {Rok + 1}. Preddavky na daň v ďalšom roku: {r.Preddavky}. Výpočet vychádza zo záložky Účtovníctvo – najprv zaúčtujte doklady." +
                     (zapisyRoka.Count == 0 ? "  ⚠ V roku nie sú žiadne účtovné zápisy." : "");
        }
    }

    string NemameParametre() =>
        $"Pre rok {Rok} program ešte nemá overené daňové parametre (sadzby, nezdaniteľnú časť). Dostupné roky: {string.Join(", ", DanovePriznanie.ParametreFoRoky.Select(x => x.Rok))}.";

    [RelayCommand]
    async Task DanovePriznaniePdf()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null || RiadkyDp.Count == 0) return;
        var s = await Dialogy.UlozSubor("Podklad k daňovému priznaniu (PDF)", $"podklad-dp-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        var titulok = j.Typ == TypJednotky.Szco ? $"Podklad k DP FO typ B za {Rok}" : $"Podklad k DP PO za {Rok}";
        await File.WriteAllBytesAsync(s, PdfDanovePriznanie.Vytvor(j, titulok, RiadkyDp.ToList(), InfoDp));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task ZauctujDanPo()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null || j.Typ != TypJednotky.Sro || !OverOdomknute()) return;
        var p = DanovePriznanie.Po(Rok);
        if (p is null || VstupyDp is null) return;
        var bezDane = zapisyRoka.Where(z => !(z.Doklad == "DP" && z.Md == "591")).ToList();
        var pv = Podvojne.Predvaha(bezDane, UctovyRozvrh, Rok);
        var r = DanovePriznanie.VypocitajPo(p, pv, UctovyRozvrh.ToList(), VstupyDp.V);
        if (!await Dialogy.Potvrd("Zaúčtovať daň z príjmov", $"Zaúčtovať splatnú daň z príjmov {PdfVyuctovanie.Eur(r.DanovaPovinnost)} k 31. 12. {Rok} (591/341)? Predchádzajúci zápis dane sa nahradí.")) return;
        var stare = zapisyRoka.Where(z => z.Doklad == "DP" && z.Md == "591").ToList();
        db.UctovneZapisy.RemoveRange(stare);
        zapisyRoka = zapisyRoka.Except(stare).ToList();
        if (r.DanovaPovinnost > 0)
        {
            var z = new UctovnyZapis { JednotkaId = j.Id, Datum = new DateOnly(Rok, 12, 31), Doklad = "DP", Popis = $"Splatná daň z príjmov {Rok}", Md = "591", D = "341", Suma = r.DanovaPovinnost, Zdroj = ZdrojZapisu.Rucne };
            db.UctovneZapisy.Add(z);
            zapisyRoka.Add(z);
        }
        db.SaveChanges();
        NacitajZapisy();
        PrepocitajDp();
    }
}

public partial class VstupyDpVm : ObservableObject
{
    public VstupyDp V { get; }
    readonly Action zmena;
    public VstupyDpVm(VstupyDp v, Action zmena) { V = v; this.zmena = zmena; }

    [ObservableProperty] private decimal poistneZDennika;

    public bool PausalneVydavky { get => V.PausalneVydavky; set { V.PausalneVydavky = value; Z(); } }
    public decimal? ZaplatenePoistne { get => V.ZaplatenePoistne; set { V.ZaplatenePoistne = value; Z(); } }
    public decimal Pilier3 { get => V.Pilier3; set { V.Pilier3 = value; Z(); } }
    public decimal DanovyBonus { get => V.DanovyBonus; set { V.DanovyBonus = value; Z(); } }
    public decimal OdpocetStraty { get => V.OdpocetStraty; set { V.OdpocetStraty = value; Z(); } }
    public decimal ZaplatenePreddavky { get => V.ZaplatenePreddavky; set { V.ZaplatenePreddavky = value; Z(); } }
    public decimal DalsiePripocitatelne { get => V.DalsiePripocitatelne; set { V.DalsiePripocitatelne = value; Z(); } }
    public decimal Odpocitatelne { get => V.Odpocitatelne; set { V.Odpocitatelne = value; Z(); } }
    public bool PrveObdobie { get => V.PrveObdobie; set { V.PrveObdobie = value; Z(); } }

    void Z() { OnPropertyChanged(string.Empty); zmena(); }
}
