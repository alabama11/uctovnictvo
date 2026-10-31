using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Časť 6: mzdy zamestnancov v pracovnom pomere.</summary>
public partial class MainViewModel
{
    public ObservableCollection<Zamestnanec> Zamestnanci { get; } = new();
    public ObservableCollection<MzdaVm> MzdyRoka { get; } = new();
    public int[] Mesiace { get; } = Enumerable.Range(1, 12).ToArray();

    List<MzdaMesiac> vsetkyMzdy = new();

    [ObservableProperty] private Zamestnanec? vybranyZamestnanec;
    [ObservableProperty] private MzdaVm? vybranaMzda;
    [ObservableProperty] private int mesiacMzdy = DateTime.Today.Month;
    [ObservableProperty] private string rekapitulaciaMzdy = "";

    partial void OnVybranyZamestnanecChanged(Zamestnanec? value) => NacitajMzdyZamestnanca();
    partial void OnMesiacMzdyChanged(int value) => ObnovRekapitulaciu();

    void NacitajCast6()
    {
        Zamestnanci.Clear(); MzdyRoka.Clear(); vsetkyMzdy = new(); RekapitulaciaMzdy = "";
        var j = VybranaJednotka;
        if (j is null || j.Typ == TypJednotky.Spolocenstvo) return;
        var vybrany = VybranyZamestnanec?.Id;
        foreach (var z in db.Zamestnanci.Where(z => z.JednotkaId == j.Id).Include(z => z.Mzdy).OrderBy(z => z.Meno).ToList()) Zamestnanci.Add(z);
        vsetkyMzdy = Zamestnanci.SelectMany(z => z.Mzdy).ToList();
        VybranyZamestnanec = Zamestnanci.FirstOrDefault(z => z.Id == vybrany) ?? Zamestnanci.FirstOrDefault();
        ObnovRekapitulaciu();
    }

    void NacitajMzdyZamestnanca()
    {
        MzdyRoka.Clear();
        var z = VybranyZamestnanec;
        if (z is null) return;
        foreach (var m in z.Mzdy.Where(m => m.Rok == Rok).OrderBy(m => m.Mesiac)) MzdyRoka.Add(new MzdaVm(m, this));
    }

    public void ObnovRekapitulaciu()
    {
        if (VybranaJednotka is null) return;
        if (Mzdy.Parametre(Rok) is null)
        {
            RekapitulaciaMzdy = $"Pre rok {Rok} program nemá overené sadzby odvodov. Dostupné roky: {string.Join(", ", Mzdy.ParametreRoky.Select(p => p.Rok))}.";
            return;
        }
        var r = Mzdy.Rekapitulacia(Rok, MesiacMzdy, vsetkyMzdy);
        RekapitulaciaMzdy = r.Hruba == 0 ? $"Za {MesiacMzdy}/{Rok} nie sú spracované mzdy." :
            $"{MesiacMzdy}/{Rok}: hrubé mzdy {E(r.Hruba)}, na výplatu {E(r.NaVyplatu)}, Sociálna poisťovňa {E(r.SocialnaPoistovna)}, " +
            string.Join(", ", r.Zdravotne.Select(x => $"{x.Poistovna} {E(x.Suma)}")) + $", daňový úrad {E(r.PreddavkyNaDan)}, cena práce {E(r.CenaPrace)}";
    }

    static string E(decimal d) => PdfVyuctovanie.Eur(d);

    [RelayCommand]
    void PridajZamestnanca()
    {
        var j = VybranaJednotka;
        if (j is null) return;
        var p = Mzdy.Parametre(Rok);
        var z = new Zamestnanec { JednotkaId = j.Id, Meno = "Nový zamestnanec", DatumNastupu = new DateOnly(Rok, 1, 1), MesacnaMzda = p?.MinimalnaMzda ?? 0 };
        db.Zamestnanci.Add(z);
        Zamestnanci.Add(z);
        VybranyZamestnanec = z;
    }

    [RelayCommand]
    async Task ZmazZamestnanca()
    {
        var z = VybranyZamestnanec;
        if (z is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať zamestnanca", $"Zmazať {z.Meno} aj so všetkými spracovanými mzdami? Pri skončení pomeru radšej vyplňte dátum ukončenia.")) return;
        db.Zamestnanci.Remove(z);
        Zamestnanci.Remove(z);
        vsetkyMzdy.RemoveAll(m => m.Zamestnanec == z);
        VybranyZamestnanec = Zamestnanci.FirstOrDefault();
        Uloz();
        ObnovRekapitulaciu();
    }

    /// <summary>Spracuje mzdy všetkých zamestnancov za zvolený mesiac (chýbajúce vytvorí z dohodnutej mzdy, existujúce prepočíta).</summary>
    [RelayCommand]
    async Task SpracujMzdy()
    {
        var p = Mzdy.Parametre(Rok);
        if (VybranaJednotka is null || Dialogy is null) return;
        if (p is null) { await Dialogy.Sprava("Mzdy", RekapitulaciaMzdy); return; }
        if (RokUzamknuty) { OverOdomknute(); return; }
        var n = 0;
        foreach (var z in Zamestnanci.Where(z => Mzdy.PracujeVMesiaci(z, Rok, MesiacMzdy)))
        {
            var m = z.Mzdy.FirstOrDefault(x => x.Rok == Rok && x.Mesiac == MesiacMzdy);
            if (m is null)
            {
                m = Mzdy.Spracuj(p, z, Rok, MesiacMzdy, z.MesacnaMzda);
                z.Mzdy.Add(m);
                vsetkyMzdy.Add(m);
            }
            else Mzdy.Spracuj(p, z, Rok, MesiacMzdy, m.HrubaMzda, m);
            n++;
        }
        Uloz();
        NacitajMzdyZamestnanca();
        ObnovRekapitulaciu();
        Stav = n == 0 ? $"V mesiaci {MesiacMzdy}/{Rok} nepracuje žiadny zamestnanec." : $"Spracované mzdy za {MesiacMzdy}/{Rok}: {n}. Do účtovníctva ich prenesie „Zaúčtovať doklady“.";
    }

    public void MzdaZmenena(MzdaVm vm)
    {
        if (Mzdy.Parametre(Rok) is { } p && vm.M.Zamestnanec is { } z) Mzdy.Spracuj(p, z, Rok, vm.M.Mesiac, vm.M.HrubaMzda, vm.M);
        ObnovRekapitulaciu();
    }

    [RelayCommand]
    async Task ZmazMzdu()
    {
        var m = VybranaMzda;
        if (m is null || Dialogy is null || !OverOdomknute()) return;
        if (!await Dialogy.Potvrd("Zmazať mzdu", $"Zmazať mzdu za {m.M.Mesiac}/{m.M.Rok}?")) return;
        db.Mzdy.Remove(m.M);
        m.M.Zamestnanec?.Mzdy.Remove(m.M);
        vsetkyMzdy.Remove(m.M);
        MzdyRoka.Remove(m);
        ObnovRekapitulaciu();
    }

    [RelayCommand]
    async Task VyplatnePaskyPdf()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null) return;
        var mzdy = vsetkyMzdy.Where(m => m.Rok == Rok && m.Mesiac == MesiacMzdy).OrderBy(m => m.Zamestnanec?.Meno).ToList();
        if (mzdy.Count == 0) { await Dialogy.Sprava("Výplatné pásky", $"Za {MesiacMzdy}/{Rok} nie sú spracované mzdy. Kliknite „Spracovať mzdy“."); return; }
        var s = await Dialogy.UlozSubor("Výplatné pásky (PDF)", $"vyplatne-pasky-{Rok}-{MesiacMzdy:00}.pdf", "pdf", "PDF");
        if (s is null) return;
        await File.WriteAllBytesAsync(s, PdfMzdy.VyplatnePasky(j, Mzdy.Parametre(Rok)!, mzdy, Mzdy.Rekapitulacia(Rok, MesiacMzdy, vsetkyMzdy)));
        Stav = $"Uložené: {s}";
    }
}

/// <summary>Riadok mzdy v tabuľke; meniť sa dá hrubá mzda a poznámka, ostatné sa prepočíta.</summary>
public partial class MzdaVm : ObservableObject
{
    public MzdaMesiac M { get; }
    readonly MainViewModel vm;
    public MzdaVm(MzdaMesiac m, MainViewModel vm) { M = m; this.vm = vm; }

    public int Mesiac => M.Mesiac;
    public decimal HrubaMzda { get => M.HrubaMzda; set { if (vm.RokUzamknuty) { OnPropertyChanged(); return; } M.HrubaMzda = value; vm.MzdaZmenena(this); OnPropertyChanged(string.Empty); } }
    public decimal Odvody => M.SpZamestnanec + M.ZpZamestnanec;
    public decimal Preddavok => M.Preddavok;
    public decimal DanovyBonus => M.DanovyBonus;
    public decimal CistaMzda => M.CistaMzda;
    public decimal OdvodyZamestnavatela => M.SpZamestnavatel + M.ZpZamestnavatel;
    public decimal CenaPrace => M.CenaPrace;
    public string? Poznamka { get => M.Poznamka; set { M.Poznamka = value; OnPropertyChanged(); } }
}
