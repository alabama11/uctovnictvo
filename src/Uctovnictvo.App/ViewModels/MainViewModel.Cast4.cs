using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Časť 4: podvojné účtovníctvo s.r.o. – účtový rozvrh, denník, hlavná kniha, saldokonto, závierka.</summary>
public partial class MainViewModel
{
    public ObservableCollection<Ucet> UctovyRozvrh { get; } = new();
    public ObservableCollection<ZapisVm> Zapisy { get; } = new();
    public ObservableCollection<ObratUctu> Predvaha { get; } = new();
    public ObservableCollection<RiadokKnihy> HlavnaKniha { get; } = new();
    public ObservableCollection<SaldoRiadok> Saldokonto { get; } = new();
    public ObservableCollection<RiadokVykazu> Suvaha { get; } = new();
    public ObservableCollection<RiadokVykazu> VykazZiskovAStrat { get; } = new();

    List<UctovnyZapis> zapisyRoka = new();
    List<ObratUctu> predvahaMinuleho = new();

    [ObservableProperty] private ZapisVm? vybranyZapis;
    [ObservableProperty] private ObratUctu? vybranyObrat;
    [ObservableProperty] private Ucet? vybranyUcetRozvrhu;
    [ObservableProperty] private bool rokUzamknuty;
    [ObservableProperty] private string suhrnUctovnictva = "";
    [ObservableProperty] private string kontrolyZavierky = "";
    [ObservableProperty] private bool lenOtvoreneSaldo = true;

    public bool RokOdomknuty => !RokUzamknuty;

    partial void OnVybranyObratChanged(ObratUctu? value) => NacitajHlavnuKnihu();
    partial void OnLenOtvoreneSaldoChanged(bool value) => NacitajSaldokonto();

    partial void OnRokUzamknutyChanged(bool value)
    {
        OnPropertyChanged(nameof(RokOdomknuty));
        if (Nacitava || VybranaJednotka is null) return;
        Core.Services.Nastavenia.Zapis(db, Podvojne.KlucUzamknutia(VybranaJednotka.Id, Rok), value ? "1" : null);
        Stav = value ? $"Rok {Rok} je uzamknutý – zápisy sa nedajú meniť ani pregenerovať." : $"Rok {Rok} je odomknutý.";
    }

    void NacitajCast4()
    {
        UctovyRozvrh.Clear(); Zapisy.Clear(); Predvaha.Clear(); HlavnaKniha.Clear(); Saldokonto.Clear(); Suvaha.Clear(); VykazZiskovAStrat.Clear();
        zapisyRoka = new(); predvahaMinuleho = new();
        SuhrnUctovnictva = ""; KontrolyZavierky = "";
        var j = VybranaJednotka;
        if (j is null || j.Typ != TypJednotky.Sro) return;

        var ucty = db.Ucty.Where(u => u.JednotkaId == j.Id).ToList();
        if (ucty.Count == 0 && j.Id != 0)
        {
            ucty = Podvojne.PredvolenyRozvrh(j.Id);
            db.Ucty.AddRange(ucty);
            db.SaveChanges();
        }
        foreach (var u in ucty.OrderBy(u => u.Cislo, StringComparer.Ordinal)) UctovyRozvrh.Add(u);

        var nacitava = Nacitava;
        Nacitava = true;
        try { RokUzamknuty = Core.Services.Nastavenia.Citaj(db, Podvojne.KlucUzamknutia(j.Id, Rok)) == "1"; }
        finally { Nacitava = nacitava; }

        zapisyRoka = db.UctovneZapisy.Where(z => z.JednotkaId == j.Id && z.Datum.Year == Rok).ToList();
        var minule = db.UctovneZapisy.Where(z => z.JednotkaId == j.Id && z.Datum.Year == Rok - 1).ToList();
        predvahaMinuleho = minule.Count == 0 ? new() : Podvojne.Predvaha(minule, UctovyRozvrh, Rok - 1);
        NacitajZapisy();
    }

    void NacitajZapisy()
    {
        Zapisy.Clear();
        foreach (var z in zapisyRoka.OrderBy(z => z.Zdroj != ZdrojZapisu.PociatocnyStav).ThenBy(z => z.Datum).ThenBy(z => z.Doklad, StringComparer.Ordinal).ThenBy(z => z.Id))
            Zapisy.Add(new ZapisVm(z, () => RokUzamknuty, PrepocitajUctovnictvo));
        PrepocitajUctovnictvo();
    }

    /// <summary>Prepočíta predvahu, hlavnú knihu, saldokonto a závierku po zmene zápisov.</summary>
    public void PrepocitajUctovnictvo()
    {
        if (VybranaJednotka is null) return;
        var vybrany = VybranyObrat?.Ucet;
        Predvaha.Clear();
        var pv = Podvojne.Predvaha(zapisyRoka, UctovyRozvrh, Rok);
        foreach (var o in pv) Predvaha.Add(o);
        VybranyObrat = Predvaha.FirstOrDefault(o => o.Ucet == vybrany) ?? Predvaha.FirstOrDefault();

        Suvaha.Clear(); VykazZiskovAStrat.Clear();
        var suv = Podvojne.Suvaha(pv, predvahaMinuleho.Count == 0 ? null : predvahaMinuleho);
        foreach (var r in suv) Suvaha.Add(r);
        foreach (var r in Podvojne.VykazZiskovAStrat(pv, predvahaMinuleho.Count == 0 ? null : predvahaMinuleho)) VykazZiskovAStrat.Add(r);
        var k = KontrolyUctovnictva(suv, pv);
        KontrolyZavierky = k.Count == 0 ? "✓ Kontroly v poriadku: aktíva sa rovnajú pasívam, všetky pohyby sú zaúčtované." : string.Join(Environment.NewLine, k.Select(x => "⚠ " + x));

        var aktiva = suv.First(r => r.Riadok == 1).Bezne;
        SuhrnUctovnictva = $"Zápisov: {zapisyRoka.Count}   Obraty: {PdfVyuctovanie.Eur(zapisyRoka.Where(z => z.Zdroj != ZdrojZapisu.PociatocnyStav).Sum(z => z.Suma))}   " +
                           $"Výsledok hospodárenia: {PdfVyuctovanie.Eur(Podvojne.VysledokHospodarenia(pv))}   Aktíva: {PdfVyuctovanie.Eur(aktiva)}" +
                           (k.Count > 0 ? $"   ⚠ {k.Count} upozornenie(a) – pozri Závierku" : "") + (RokUzamknuty ? "   🔒 uzamknutý" : "");
        NacitajSaldokonto();
        if (VstupyDp is not null && VybranaJednotka?.Typ == TypJednotky.Sro) PrepocitajDp();
    }

    List<string> KontrolyUctovnictva(IReadOnlyList<RiadokVykazu> suv, IReadOnlyList<ObratUctu> pv)
    {
        var k = Podvojne.Kontroly(suv, pv);
        var cisla = UctovyRozvrh.Select(u => u.Cislo).ToHashSet();
        var nezname = zapisyRoka.SelectMany(z => new[] { z.Md, z.D }).Where(c => c.Length > 0 && !cisla.Contains(c)).Distinct().OrderBy(c => c).ToList();
        if (nezname.Count > 0) k.Add($"Účty mimo účtového rozvrhu: {string.Join(", ", nezname)} – doplňte ich v záložke Účtový rozvrh.");
        if (zapisyRoka.Any(z => z.Md.Length == 0 || z.D.Length == 0 || z.Suma <= 0)) k.Add("Niektoré ručné zápisy nemajú vyplnený účet MD, D alebo kladnú sumu.");
        if (zapisyRoka.Count > 0 && !zapisyRoka.Any(z => z.Zdroj == ZdrojZapisu.PociatocnyStav) && db.UctovneZapisy.Any(z => z.JednotkaId == VybranaJednotka!.Id && z.Datum.Year == Rok - 1))
            k.Add($"Chýbajú počiatočné stavy – použite „Otvoriť rok z {Rok - 1}“.");
        return k;
    }

    void NacitajHlavnuKnihu()
    {
        HlavnaKniha.Clear();
        var c = VybranyObrat?.Ucet;
        if (c is null) return;
        var zostatok = 0m;
        foreach (var z in zapisyRoka.Where(z => z.Md == c || z.D == c).OrderBy(z => z.Zdroj != ZdrojZapisu.PociatocnyStav).ThenBy(z => z.Datum).ThenBy(z => z.Id))
        {
            var md = z.Md == c ? z.Suma : 0;
            var d = z.D == c ? z.Suma : 0;
            zostatok += md - d;
            HlavnaKniha.Add(new RiadokKnihy(z.Datum, z.Doklad, z.Popis, z.Md == c ? z.D : z.Md, md, d, zostatok));
        }
    }

    void NacitajSaldokonto()
    {
        Saldokonto.Clear();
        if (VybranaJednotka is null) return;
        var k = new DateOnly(Rok, 12, 31);
        var riadky = Podvojne.Saldokonto(vsetkyFaktury, k);
        foreach (var r in riadky.Where(r => !LenOtvoreneSaldo || r.Zostatok != 0)) Saldokonto.Add(r);
    }

    bool OverOdomknute()
    {
        if (!RokUzamknuty) return true;
        Dialogy?.Sprava("Rok je uzamknutý", $"Rok {Rok} je uzamknutý. Ak potrebujete opravu, najprv zrušte zaškrtnutie „Uzamknúť rok“.");
        return false;
    }

    [RelayCommand]
    async Task ZauctujDoklady()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null || !OverOdomknute()) return;
        var automaticke = zapisyRoka.Where(z => z.Zdroj is not (ZdrojZapisu.Rucne or ZdrojZapisu.PociatocnyStav)).ToList();
        if (automaticke.Count > 0 && !await Dialogy.Potvrd("Zaúčtovať doklady",
                $"Automatické zápisy roka {Rok} ({automaticke.Count}) sa vytvoria nanovo z faktúr, banky, pokladne, majetku a miezd. Ručné zápisy a počiatočné stavy zostanú. Pokračovať?"))
            return;
        Uloz();
        db.UctovneZapisy.RemoveRange(automaticke);
        var nove = Podvojne.Zauctuj(j.Id, Rok, vsetkyFaktury, vsetkyPohyby, vsetkyDoklady, vsetokMajetok, vsetkyMzdy);
        db.UctovneZapisy.AddRange(nove);
        db.SaveChanges();
        zapisyRoka = zapisyRoka.Except(automaticke).Concat(nove).ToList();
        NacitajZapisy();
        Stav = $"Zaúčtovaných {nove.Count} zápisov z dokladov roka {Rok}.";
        var nezaradene = nove.Where(z => z.D == "395" || z.Md == "395").ToList();
        if (nezaradene.Count > 0)
            await Dialogy.Sprava("Zaúčtovať doklady", $"Hotovo, {nove.Count} zápisov. {nezaradene.Count} pohybov nemá kategóriu, preto sú zatiaľ na účte 395 (zúčtovací vzťah). " +
                                                     "Priraďte im kategóriu v záložke Daňová evidencia a zaúčtujte znova.");
    }

    [RelayCommand]
    void PridajZapis()
    {
        var j = VybranaJednotka;
        if (j is null || !OverOdomknute()) return;
        var z = new UctovnyZapis { JednotkaId = j.Id, Datum = DnesVRoku(), Doklad = "ID", Popis = "Interný doklad", Zdroj = ZdrojZapisu.Rucne };
        db.UctovneZapisy.Add(z);
        zapisyRoka.Add(z);
        var vm = new ZapisVm(z, () => RokUzamknuty, PrepocitajUctovnictvo);
        Zapisy.Add(vm);
        VybranyZapis = vm;
    }

    [RelayCommand]
    async Task ZmazZapis()
    {
        var z = VybranyZapis;
        if (z is null || Dialogy is null || !OverOdomknute()) return;
        if (!z.Upravitelny)
        {
            await Dialogy.Sprava("Zmazať zápis", "Tento zápis vznikol automaticky z dokladu. Opravte doklad (faktúru, pohyb, kategóriu) a kliknite „Zaúčtovať doklady“.");
            return;
        }
        if (!await Dialogy.Potvrd("Zmazať zápis", $"Zmazať zápis {z.Doklad} {z.Md}/{z.D} {PdfVyuctovanie.Eur(z.Suma)}?")) return;
        db.UctovneZapisy.Remove(z.Z);
        zapisyRoka.Remove(z.Z);
        Zapisy.Remove(z);
        PrepocitajUctovnictvo();
    }

    [RelayCommand]
    async Task OtvoritRok()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null || !OverOdomknute()) return;
        var minule = db.UctovneZapisy.Where(z => z.JednotkaId == j.Id && z.Datum.Year == Rok - 1).ToList();
        if (minule.Count == 0)
        {
            await Dialogy.Sprava("Otvoriť rok", $"Za rok {Rok - 1} nie sú v programe žiadne zápisy. Počiatočné stavy (napr. 221 Bankové účty, 211 Pokladnica, 411 Základné imanie) " +
                                                $"zadajte ručne cez „+ Zápis“ so zdrojom počiatočný stav: účet MD / 701 pre aktíva, 701 / účet D pre pasíva.");
            return;
        }
        var stare = zapisyRoka.Where(z => z.Zdroj == ZdrojZapisu.PociatocnyStav).ToList();
        if (!await Dialogy.Potvrd("Otvoriť rok", $"Vytvoriť počiatočné stavy k 1. 1. {Rok} z konečných zostatkov roka {Rok - 1}?" +
                                                 (stare.Count > 0 ? $" Existujúce počiatočné stavy ({stare.Count}) sa nahradia." : "") +
                                                 $" Výsledok hospodárenia {Rok - 1} sa prenesie na účet 431; po schválení ho rozdeľte ručným zápisom (napr. 431/428).")) return;
        var pv = Podvojne.Predvaha(minule, UctovyRozvrh, Rok - 1);
        db.UctovneZapisy.RemoveRange(stare);
        var ps = Podvojne.PociatocneStavy(j.Id, pv, Rok);
        db.UctovneZapisy.AddRange(ps);
        db.SaveChanges();
        zapisyRoka = zapisyRoka.Except(stare).Concat(ps).ToList();
        predvahaMinuleho = pv;
        NacitajZapisy();
        Stav = $"Vytvorených {ps.Count} zápisov počiatočných stavov k 1. 1. {Rok}.";
    }

    [RelayCommand]
    void PridajPociatocnyStav()
    {
        var j = VybranaJednotka;
        if (j is null || !OverOdomknute()) return;
        var z = new UctovnyZapis { JednotkaId = j.Id, Datum = new DateOnly(Rok, 1, 1), Doklad = "PS", Popis = "Počiatočný stav", D = "701", Zdroj = ZdrojZapisu.PociatocnyStav };
        db.UctovneZapisy.Add(z);
        zapisyRoka.Add(z);
        var vm = new ZapisVm(z, () => RokUzamknuty, PrepocitajUctovnictvo);
        Zapisy.Insert(zapisyRoka.Count(x => x.Zdroj == ZdrojZapisu.PociatocnyStav) - 1, vm);
        VybranyZapis = vm;
    }

    [RelayCommand]
    void PridajUcetRozvrhu()
    {
        var j = VybranaJednotka;
        if (j is null) return;
        var u = new Ucet { JednotkaId = j.Id, Cislo = "", Nazov = "Nový účet", Typ = TypUctu.Nakladovy };
        db.Ucty.Add(u);
        UctovyRozvrh.Add(u);
        VybranyUcetRozvrhu = u;
    }

    [RelayCommand]
    async Task ZmazUcetRozvrhu()
    {
        var u = VybranyUcetRozvrhu;
        if (u is null || Dialogy is null || VybranaJednotka is null) return;
        if (u.Cislo.Length > 0 && db.UctovneZapisy.Any(z => z.JednotkaId == VybranaJednotka.Id && (z.Md == u.Cislo || z.D == u.Cislo)))
        {
            await Dialogy.Sprava("Zmazať účet", $"Na účet {u.Cislo} sú zaúčtované zápisy, preto ho nemožno zmazať.");
            return;
        }
        if (!await Dialogy.Potvrd("Zmazať účet", $"Zmazať účet {u}?")) return;
        db.Ucty.Remove(u);
        UctovyRozvrh.Remove(u);
    }

    IReadOnlyList<UctovnyZapis> ZoradeneZapisy() =>
        zapisyRoka.OrderBy(z => z.Zdroj != ZdrojZapisu.PociatocnyStav).ThenBy(z => z.Datum).ThenBy(z => z.Doklad, StringComparer.Ordinal).ThenBy(z => z.Id).ToList();

    [RelayCommand]
    async Task ZavierkaPdf()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null) return;
        var s = await Dialogy.UlozSubor("Účtovná závierka (PDF)", $"zavierka-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        var pv = Predvaha.ToList();
        var kontroly = KontrolyUctovnictva(Suvaha.ToList(), pv);
        await File.WriteAllBytesAsync(s, PdfZavierka.Zavierka(j, Rok, Suvaha.ToList(), VykazZiskovAStrat.ToList(), pv, kontroly));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task UctovnyDennikPdf()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null) return;
        var s = await Dialogy.UlozSubor("Účtovný denník (PDF)", $"uctovny-dennik-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        await File.WriteAllBytesAsync(s, PdfZavierka.Dennik(j, Rok, ZoradeneZapisy()));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task UctovnictvoExcel()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null) return;
        var s = await Dialogy.UlozSubor("Účtovníctvo (Excel)", $"uctovnictvo-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        ExportExcel.Uctovnictvo(j, Rok, ZoradeneZapisy(), Predvaha.ToList(), Podvojne.Saldokonto(vsetkyFaktury, new DateOnly(Rok, 12, 31)), Suvaha.ToList(), VykazZiskovAStrat.ToList(), s);
        Stav = $"Uložené: {s}";
    }
}

public record RiadokKnihy(DateOnly Datum, string Doklad, string Popis, string Protiucet, decimal Md, decimal D, decimal Zostatok);

/// <summary>Riadok účtovného denníka; meniť sa dajú len ručné zápisy a počiatočné stavy v neuzamknutom roku.</summary>
public partial class ZapisVm : ObservableObject
{
    public UctovnyZapis Z { get; }
    readonly Func<bool> uzamknute;
    readonly Action zmena;
    public ZapisVm(UctovnyZapis z, Func<bool> uzamknute, Action zmena) { Z = z; this.uzamknute = uzamknute; this.zmena = zmena; }

    public bool Upravitelny => Z.Zdroj is ZdrojZapisu.Rucne or ZdrojZapisu.PociatocnyStav;
    bool Mozno() { if (Upravitelny && !uzamknute()) return true; OnPropertyChanged(string.Empty); return false; }

    public DateOnly? Datum { get => Z.Datum; set { if (value is { } d && Mozno()) { Z.Datum = d; Zmenene(); } } }
    public string Doklad { get => Z.Doklad; set { if (Mozno()) { Z.Doklad = value ?? ""; OnPropertyChanged(); } } }
    public string Popis { get => Z.Popis; set { if (Mozno()) { Z.Popis = value ?? ""; OnPropertyChanged(); } } }
    public string Md { get => Z.Md; set { if (Mozno()) { Z.Md = (value ?? "").Trim(); Zmenene(); } } }
    public string D { get => Z.D; set { if (Mozno()) { Z.D = (value ?? "").Trim(); Zmenene(); } } }
    public decimal Suma { get => Z.Suma; set { if (Mozno()) { Z.Suma = value; Zmenene(); } } }
    public string Partner => Z.Partner ?? "";
    public ZdrojZapisu Zdroj => Z.Zdroj;

    void Zmenene() { OnPropertyChanged(string.Empty); zmena(); }
}
