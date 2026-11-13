using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Odpočty vody podľa tlačiva: hlavička (dátumy, ceny), import a export z Excelu.</summary>
public partial class MainViewModel
{
    VodaRoka? vodaRoku;

    /// <summary>Hlavička tlačiva vody pre vybraný rok (vytvorí sa pri prvom použití).</summary>
    public VodaRoka? VodaRoku => vodaRoku;

    void NacitajVoduRoka()
    {
        var j = VybranaJednotka;
        vodaRoku = j is null || j.Typ != TypJednotky.Spolocenstvo ? null
            : db.VodaRoky.Local.FirstOrDefault(v => v.JednotkaId == j.Id && v.Rok == Rok)
              ?? db.VodaRoky.FirstOrDefault(v => v.JednotkaId == j.Id && v.Rok == Rok);
        foreach (var n in new[] { nameof(VodaDatum1), nameof(VodaDatum2), nameof(VodaDatum3), nameof(VodaDatum4), nameof(VodaCena1), nameof(VodaCena2) })
            OnPropertyChanged(n);
    }

    VodaRoka ZabezpecVoduRoka()
    {
        if (vodaRoku is not null) return vodaRoku;
        vodaRoku = new VodaRoka { JednotkaId = VybranaJednotka!.Id, Rok = Rok };
        db.VodaRoky.Add(vodaRoku);
        return vodaRoku;
    }

    void ZmenaVody(Action<VodaRoka> zmena, string meno)
    {
        if (VybranaJednotka is null || !JeSpolocenstvo) return;
        zmena(ZabezpecVoduRoka());
        OnPropertyChanged(meno);
        foreach (var r in RokRiadky) r.Obnov();
    }

    public DateOnly? VodaDatum1 { get => vodaRoku?.Datum1; set => ZmenaVody(v => v.Datum1 = value, nameof(VodaDatum1)); }
    public DateOnly? VodaDatum2 { get => vodaRoku?.Datum2; set => ZmenaVody(v => v.Datum2 = value, nameof(VodaDatum2)); }
    public DateOnly? VodaDatum3 { get => vodaRoku?.Datum3; set => ZmenaVody(v => v.Datum3 = value, nameof(VodaDatum3)); }
    public DateOnly? VodaDatum4 { get => vodaRoku?.Datum4; set => ZmenaVody(v => v.Datum4 = value, nameof(VodaDatum4)); }
    public decimal VodaCena1 { get => vodaRoku?.Cena1 ?? 0m; set => ZmenaVody(v => v.Cena1 = value, nameof(VodaCena1)); }
    public decimal VodaCena2 { get => vodaRoku?.Cena2 ?? 0m; set => ZmenaVody(v => v.Cena2 = value, nameof(VodaCena2)); }

    RokRiadok NovyRokRiadok(Priestor p) =>
        new(p, ZabezpecRok(p, Rok), () => vodaRoku, () => Vyuctovanie.KategoriaVody(Kategorie));

    /// <summary>Prepočíta stĺpce odpočtov (napr. po zmene predpisu záloh).</summary>
    public void ObnovOdpocty()
    {
        foreach (var r in RokRiadky) r.Obnov();
    }

    [RelayCommand]
    async Task ImportOdpoctovVody()
    {
        if (Dialogy is null || VybranaJednotka is null || !JeSpolocenstvo) return;
        var s = await Dialogy.OtvorSubor("Import odpočtov vody z Excelu (tlačivo)", "xlsx;xlsm", "Excel");
        if (s is null) return;
        TlacivoVody t;
        try
        {
            t = OdpoctyVody.Nacitaj(s);
        }
        catch (Exception ex)
        {
            await Dialogy.Sprava("Tlačivo sa nepodarilo načítať", ex.Message);
            return;
        }

        static string Kluc(string c) => c.Trim().TrimStart('0').ToUpperInvariant();
        var podlaCisla = Priestory.GroupBy(p => Kluc(p.Cislo)).ToDictionary(g => g.Key, g => g.First());
        var nenajdene = new List<string>();
        int pocet = 0;
        foreach (var r in t.Riadky)
        {
            if (!podlaCisla.TryGetValue(Kluc(r.Byt), out var p))
            {
                nenajdene.Add(r.Byt);
                continue;
            }
            var pr = ZabezpecRok(p, Rok);
            pr.StavVodomeruZaciatok = r.Stav2;
            pr.StavVodomeruKoniec = r.Stav3;
            pr.StavVodomeru2Zaciatok = r.Stav7;
            pr.StavVodomeru2Koniec = r.Stav8;
            if (r.Stav2 is not null || r.Stav3 is not null || r.Stav7 is not null) pr.SpotrebaVymenenyVodomer = 0;
            if (!string.IsNullOrWhiteSpace(r.Vodomer1)) p.CisloVodomeru = r.Vodomer1;
            if (!string.IsNullOrWhiteSpace(r.Vodomer2)) p.CisloVodomeru2 = r.Vodomer2;
            pocet++;
        }
        var v = ZabezpecVoduRoka();
        if (t.Datum1 is not null) v.Datum1 = t.Datum1;
        if (t.Datum2 is not null) v.Datum2 = t.Datum2;
        if (t.Datum3 is not null) v.Datum3 = t.Datum3;
        if (t.Datum4 is not null) v.Datum4 = t.Datum4;
        if (t.Cena1 is not null) v.Cena1 = t.Cena1.Value;
        if (t.Cena2 is not null) v.Cena2 = t.Cena2.Value;
        Uloz();
        NacitajVoduRoka();
        ObnovOdpocty();
        var sprava = $"Načítané odpočty pre {pocet} bytov za rok {Rok}.";
        if (nenajdene.Count > 0) sprava += $"\n\nTieto byty v evidencii nie sú: {string.Join(", ", nenajdene)}.";
        Stav = $"Načítané odpočty pre {pocet} bytov.";
        await Dialogy.Sprava("Import odpočtov vody", sprava);
    }

    [RelayCommand]
    async Task ExportOdpoctovVody()
    {
        if (Dialogy is null || VybranaJednotka is null || !JeSpolocenstvo) return;
        var s = await Dialogy.UlozSubor("Tlačivo odpočtov vody (Excel)", $"odpocty-vody-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        Uloz();
        OdpoctyVody.Export(VybranaJednotka, Priestory.ToList(), vodaRoku, Vyuctovanie.KategoriaVody(Kategorie), Rok, s);
        Stav = $"Uložené: {s}";
    }
}
