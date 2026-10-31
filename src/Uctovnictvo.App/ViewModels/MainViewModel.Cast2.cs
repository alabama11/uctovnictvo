using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Časť 2: adresár, faktúry, banka, pokladňa.</summary>
public partial class MainViewModel
{
    public ObservableCollection<Partner> Partneri { get; } = new();
    public ObservableCollection<FakturaVm> Faktury { get; } = new();
    public ObservableCollection<FakturaVm> OtvoreneFaktury { get; } = new();
    public ObservableCollection<PohybVm> BankovePohyby { get; } = new();
    public ObservableCollection<DokladVm> Doklady { get; } = new();

    List<Faktura> vsetkyFaktury = new();
    List<BankovyPohyb> vsetkyPohyby = new();
    List<PokladnicnyDoklad> vsetkyDoklady = new();

    [ObservableProperty] private Partner? vybranyPartner;
    [ObservableProperty] private FakturaVm? vybranaFaktura;
    [ObservableProperty] private PolozkaVm? vybranaPolozka;
    [ObservableProperty] private PohybVm? vybranyBankovyPohyb;
    [ObservableProperty] private FakturaVm? fakturaNaPriradenie;
    [ObservableProperty] private DokladVm? vybranyDoklad;
    /// <summary>0 = vydané, 1 = prijaté.</summary>
    [ObservableProperty] private int druhFaktur;
    [ObservableProperty] private bool lenNeuhradene;
    [ObservableProperty] private string sumaFaktur = "";
    [ObservableProperty] private string sumaBanky = "";
    [ObservableProperty] private string stavPokladne = "";

    public bool ZobrazujeVydane => DruhFaktur == 0;

    partial void OnDruhFakturChanged(int value) { OnPropertyChanged(nameof(ZobrazujeVydane)); FiltrujFaktury(); }
    partial void OnLenNeuhradeneChanged(bool value) => FiltrujFaktury();

    void NacitajCast2()
    {
        Partneri.Clear(); Faktury.Clear(); OtvoreneFaktury.Clear(); BankovePohyby.Clear(); Doklady.Clear();
        vsetkyFaktury = new(); vsetkyPohyby = new(); vsetkyDoklady = new();
        var j = VybranaJednotka;
        if (j is null) return;

        foreach (var p in db.Partneri.Where(p => p.JednotkaId == j.Id).OrderBy(p => p.Nazov).ToList()) Partneri.Add(p);
        vsetkyFaktury = db.Faktury.Where(f => f.JednotkaId == j.Id)
            .Include(f => f.Polozky).Include(f => f.Uhrady).ThenInclude(u => u.PokladnicnyDoklad)
            .Include(f => f.Uhrady).ThenInclude(u => u.BankovyPohyb)
            .AsSplitQuery().ToList();
        vsetkyPohyby = db.BankovePohyby.Where(p => p.JednotkaId == j.Id).Include(p => p.Uhrady).ToList();
        vsetkyDoklady = db.PokladnicneDoklady.Where(d => d.JednotkaId == j.Id).Include(d => d.Uhrady).ToList();
        FiltrujFaktury();
        NacitajBanku();
        NacitajPokladnu();
        ObnovNakladyZFaktur();
        NacitajCast3();
    }

    IEnumerable<string> CislaFaktur(DruhFaktury druh) => vsetkyFaktury.Where(f => f.Druh == druh).Select(f => f.Cislo);

    void FiltrujFaktury(Faktura? vybrat = null)
    {
        vybrat ??= VybranaFaktura?.F;
        Faktury.Clear();
        var druh = DruhFaktur == 0 ? DruhFaktury.Vydana : DruhFaktury.Prijata;
        var zoznam = vsetkyFaktury.Where(f => f.Druh == druh && (LenNeuhradene ? f.Zostava != 0 : f.DatumVystavenia.Year == Rok))
            .OrderBy(f => f.DatumVystavenia).ThenBy(f => f.Cislo);
        foreach (var f in zoznam) Faktury.Add(new FakturaVm(f));
        VybranaFaktura = Faktury.FirstOrDefault(f => f.F == vybrat) ?? Faktury.LastOrDefault();
        PrepocitajFaktury();

        OtvoreneFaktury.Clear();
        foreach (var f in vsetkyFaktury.Where(f => f.Zostava != 0).OrderBy(f => f.Druh).ThenBy(f => f.DatumVystavenia))
            OtvoreneFaktury.Add(new FakturaVm(f));
    }

    public void PrepocitajFaktury()
    {
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        SumaFaktur = $"Počet: {Faktury.Count}   Spolu: {PdfVyuctovanie.Eur(Faktury.Sum(f => f.Suma))}   " +
                     $"Neuhradené: {PdfVyuctovanie.Eur(Faktury.Sum(f => f.Zostava))}   " +
                     $"Po splatnosti: {Faktury.Count(f => f.F.JePoSplatnosti(dnes))}";
    }

    // ---------- adresár ----------

    [RelayCommand]
    void PridajPartnera()
    {
        if (VybranaJednotka is null) return;
        var p = new Partner { JednotkaId = VybranaJednotka.Id, Nazov = "Nový partner" };
        db.Partneri.Add(p);
        Partneri.Add(p);
        VybranyPartner = p;
    }

    [RelayCommand]
    async Task ZmazPartnera()
    {
        var p = VybranyPartner;
        if (p is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať partnera", $"Zmazať „{p.Nazov}“ z adresára? Faktúry zostanú zachované.")) return;
        foreach (var f in vsetkyFaktury.Where(f => f.Partner == p)) { f.Partner = null; f.PartnerId = null; }
        db.Partneri.Remove(p);
        Partneri.Remove(p);
        VybranyPartner = Partneri.FirstOrDefault();
    }

    [RelayCommand]
    async Task NacitajPodlaIco()
    {
        var p = VybranyPartner;
        if (p is null || Dialogy is null) return;
        if (RegisterIco.Normalizuj(p.Ico) is null)
        {
            await Dialogy.Sprava("Načítať podľa IČO", "Najprv zadajte IČO partnera (8 číslic).");
            return;
        }
        Stav = "Hľadám v registri právnických osôb…";
        try
        {
            var u = await RegisterIco.Vyhladaj(p.Ico!);
            if (u is null)
            {
                Stav = "";
                await Dialogy.Sprava("Načítať podľa IČO", $"IČO {p.Ico} sa v registri nenašlo.");
                return;
            }
            RegisterIco.Dopln(p, u);
            var i = Partneri.IndexOf(p);
            Partneri[i] = p; // obnoví zobrazenie
            VybranyPartner = p;
            OnPropertyChanged(nameof(VybranyPartner));
            Stav = $"Načítané z registra: {u.Nazov}";
        }
        catch (Exception ex)
        {
            Stav = "";
            await Dialogy.Sprava("Načítať podľa IČO", "Register sa nepodarilo načítať: " + ex.Message +
                "\n\nSkontrolujte pripojenie na internet. Údaje môžete zadať aj ručne.");
        }
    }

    // ---------- faktúry ----------

    void PridajFakturu(Faktura f)
    {
        f.JednotkaId = VybranaJednotka!.Id;
        db.Faktury.Add(f);
        vsetkyFaktury.Add(f);
        if (!LenNeuhradene && f.DatumVystavenia.Year != Rok) Rok = f.DatumVystavenia.Year;
        DruhFaktur = f.Druh == DruhFaktury.Vydana ? 0 : 1;
        FiltrujFaktury(f);
    }

    DateOnly DnesVRoku() => DateTime.Today.Year == Rok ? DateOnly.FromDateTime(DateTime.Today) : new DateOnly(Rok, 12, 31);

    [RelayCommand]
    void NovaFaktura() => NovyDoklad(DruhFaktur == 0 ? DruhFaktury.Vydana : DruhFaktury.Prijata, TypFaktury.Faktura);

    [RelayCommand]
    void NovaZalohova() => NovyDoklad(DruhFaktury.Vydana, TypFaktury.Zalohova);

    void NovyDoklad(DruhFaktury druh, TypFaktury typ)
    {
        if (VybranaJednotka is null) return;
        var f = Core.Services.Faktury.Nova(VybranaJednotka, druh, typ, DnesVRoku(), CislaFaktur(druh));
        if (druh == DruhFaktury.Vydana)
            f.Polozky.Add(new PolozkaFaktury { Poradie = 1, Popis = "", Mnozstvo = 1, Jednotka = "ks" });
        PridajFakturu(f);
    }

    [RelayCommand]
    void KopiaFaktury()
    {
        if (VybranaFaktura is null || VybranaJednotka is null) return;
        PridajFakturu(Core.Services.Faktury.Kopia(VybranaFaktura.F, VybranaJednotka, DnesVRoku(), CislaFaktur(VybranaFaktura.F.Druh)));
    }

    [RelayCommand]
    void DobropisFaktury()
    {
        if (VybranaFaktura is null || VybranaJednotka is null) return;
        PridajFakturu(Core.Services.Faktury.Dobropis(VybranaFaktura.F, VybranaJednotka, DnesVRoku(), CislaFaktur(VybranaFaktura.F.Druh)));
    }

    [RelayCommand]
    async Task VyuctovanieZalohy()
    {
        if (VybranaFaktura is null || VybranaJednotka is null || Dialogy is null) return;
        if (VybranaFaktura.F.Typ != TypFaktury.Zalohova)
        {
            await Dialogy.Sprava("Vyúčtovanie zálohy", "Vyberte zálohovú faktúru, ku ktorej chcete vystaviť vyúčtovaciu faktúru.");
            return;
        }
        PridajFakturu(Core.Services.Faktury.ZoZalohovej(VybranaFaktura.F, VybranaJednotka, DnesVRoku(), CislaFaktur(DruhFaktury.Vydana)));
    }

    [RelayCommand]
    async Task ZmazFakturu()
    {
        var f = VybranaFaktura?.F;
        if (f is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať faktúru", $"Naozaj zmazať {Core.Services.Faktury.NazovTypu(f.Typ).ToLower()} {f.Cislo}? " +
                                  (f.Uhrady.Count > 0 ? "Zmažú sa aj jej úhrady (bankové pohyby a pokladničné doklady zostanú)." : ""))) return;
        foreach (var u in f.Uhrady.ToList())
        {
            u.BankovyPohyb?.Uhrady.Remove(u);
            u.PokladnicnyDoklad?.Uhrady.Remove(u);
            db.UhradyFaktur.Remove(u);
        }
        db.Faktury.Remove(f);
        vsetkyFaktury.Remove(f);
        VybranaFaktura = null;
        FiltrujFaktury();
        NacitajBanku();
    }

    [RelayCommand]
    void PridajPolozku()
    {
        var f = VybranaFaktura;
        if (f is null) return;
        var p = new PolozkaFaktury { Poradie = f.F.Polozky.Count == 0 ? 1 : f.F.Polozky.Max(x => x.Poradie) + 1, Mnozstvo = 1, Jednotka = "ks" };
        f.F.Polozky.Add(p);
        if (f.F.Id != 0) db.PolozkyFaktur.Add(p);
        var vm = new PolozkaVm(p, f);
        f.Polozky.Add(vm);
        VybranaPolozka = vm;
        f.Obnov();
    }

    [RelayCommand]
    void ZmazPolozku()
    {
        var f = VybranaFaktura;
        var p = VybranaPolozka;
        if (f is null || p is null) return;
        f.F.Polozky.Remove(p.P);
        if (p.P.Id != 0) db.PolozkyFaktur.Remove(p.P);
        f.Polozky.Remove(p);
        f.Obnov();
        PrepocitajFaktury();
    }

    [RelayCommand]
    async Task FakturaPdf()
    {
        var f = VybranaFaktura?.F;
        if (f is null || Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Uložiť faktúru (PDF)", $"{BezZnakov(Core.Services.Faktury.NazovTypu(f.Typ))}-{BezZnakov(f.Cislo)}.pdf", "pdf", "PDF");
        if (s is null) return;
        Uloz();
        await File.WriteAllBytesAsync(s, PdfFaktury.Faktura(f, VybranaJednotka));
        Stav = $"Uložené: {s}";
    }

    static string BezZnakov(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c));

    [RelayCommand]
    async Task FakturaEmail()
    {
        var f = VybranaFaktura?.F;
        if (f is null || Dialogy is null || VybranaJednotka is null) return;
        var nast = Core.Services.Nastavenia.Email(db);
        if (!nast.JeVyplnene) { await Dialogy.Sprava("Poslať faktúru", "Najprv nastavte odosielanie e-mailov v menu Súbor → Nastavenia e-mailu."); return; }
        var adresa = f.Partner?.Email;
        if (!Email.JePlatnaAdresa(adresa))
        {
            adresa = await Dialogy.VstupnyText("Poslať faktúru", $"E-mailová adresa odberateľa {f.PartnerNazov}:", adresa ?? "");
            if (adresa is null) return;
            if (!Email.JePlatnaAdresa(adresa)) { await Dialogy.Sprava("Poslať faktúru", "Adresa nie je platná."); return; }
            if (f.Partner is not null) f.Partner.Email = adresa;
        }
        var typ = Core.Services.Faktury.NazovTypu(f.Typ);
        var t = await Dialogy.TextEmailu("Poslať faktúru", $"Komu: {adresa}\nZnačky {{cislo}}, {{suma}}, {{splatnost}}, {{vs}} sa nahradia údajmi faktúry.",
            $"{typ} č. {{cislo}} – {VybranaJednotka.Nazov}",
            $"Dobrý deň,\n\nv prílohe vám posielame {typ.ToLower()} č. {{cislo}} na sumu {{suma}} so splatnosťou {{splatnost}}.\n" +
            "Pri platbe prosíme uviesť variabilný symbol {vs}.\n\nS pozdravom\n" + (VybranaJednotka.Statutar ?? VybranaJednotka.Nazov));
        if (t is null) return;
        Odosiela = true;
        try
        {
            Uloz();
            var sprava = new EmailSprava(adresa!, f.PartnerNazov, Core.Services.Faktury.NahradZnaky(t.Value.Predmet, f), Core.Services.Faktury.NahradZnaky(t.Value.Text, f),
                new[] { ($"{BezZnakov(typ)}-{BezZnakov(f.Cislo)}.pdf", PdfFaktury.Faktura(f, VybranaJednotka)) });
            var chyby = await Email.Odosli(nast, new[] { sprava });
            Stav = chyby.Count == 0 ? $"Faktúra {f.Cislo} odoslaná na {adresa}." : "Odoslanie zlyhalo.";
            if (chyby.Count > 0) await Dialogy.Sprava("Poslať faktúru", string.Join("\n", chyby));
        }
        catch (Exception ex)
        {
            await Dialogy.Sprava("Poslať faktúru", "Odosielanie zlyhalo: " + ex.Message);
        }
        finally { Odosiela = false; }
    }

    [RelayCommand]
    async Task UhradaHotovostou()
    {
        var f = VybranaFaktura?.F;
        if (f is null || Dialogy is null) return;
        if (f.Zostava == 0) { await Dialogy.Sprava("Úhrada v hotovosti", "Faktúra je už uhradená."); return; }
        var d = Pokladna.ZFaktury(f, DnesVRoku(), vsetkyDoklady.Select(x => x.Cislo));
        d.JednotkaId = VybranaJednotka!.Id;
        db.PokladnicneDoklady.Add(d);
        vsetkyDoklady.Add(d);
        VybranaFaktura!.Obnov();
        PrepocitajFaktury();
        NacitajPokladnu();
        Stav = $"Vytvorený pokladničný doklad {d.Cislo} na {PdfVyuctovanie.Eur(d.Suma)}.";
    }

    [RelayCommand]
    async Task ZrusUhrady()
    {
        var f = VybranaFaktura?.F;
        if (f is null || Dialogy is null || f.Uhrady.Count == 0) return;
        if (!await Dialogy.Potvrd("Zrušiť úhrady", $"Zrušiť priradenie {f.Uhrady.Count} úhrad k faktúre {f.Cislo}? Pokladničné doklady vytvorené k faktúre sa zmažú.")) return;
        foreach (var u in f.Uhrady.ToList())
        {
            u.BankovyPohyb?.Uhrady.Remove(u);
            if (u.PokladnicnyDoklad is { } d && d.Uhrady.Count == 1)
            {
                db.PokladnicneDoklady.Remove(d);
                vsetkyDoklady.Remove(d);
            }
            u.PokladnicnyDoklad?.Uhrady.Remove(u);
            f.Uhrady.Remove(u);
            db.UhradyFaktur.Remove(u);
        }
        VybranaFaktura!.Obnov();
        FiltrujFaktury();
        NacitajBanku();
        NacitajPokladnu();
    }

    [RelayCommand]
    async Task UpomienkaFaktury()
    {
        var f = VybranaFaktura?.F;
        if (f is null || Dialogy is null || VybranaJednotka is null) return;
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        var zoznam = vsetkyFaktury.Where(x => x.Druh == DruhFaktury.Vydana && x.PartnerNazov == f.PartnerNazov && x.JePoSplatnosti(dnes))
            .OrderBy(x => x.DatumSplatnosti).ToList();
        if (zoznam.Count == 0) { await Dialogy.Sprava("Upomienka", $"Odberateľ {f.PartnerNazov} nemá faktúry po splatnosti."); return; }
        var s = await Dialogy.UlozSubor("Upomienka (PDF)", $"upomienka-{BezZnakov(f.PartnerNazov)}.pdf", "pdf", "PDF");
        if (s is null) return;
        await File.WriteAllBytesAsync(s, PdfFaktury.Upomienka(zoznam, VybranaJednotka, dnes));
        foreach (var x in zoznam) x.DatumUpomienky = dnes;
        Stav = $"Upomienka ({zoznam.Count} faktúr) uložená: {s}";
    }

    [RelayCommand]
    async Task ImportEFaktury()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.OtvorSubor("Načítať prijatú e-faktúru (UBL XML)", "xml", "E-faktúra XML");
        if (s is null) return;
        try
        {
            var v = EFaktura.Nacitaj(s);
            var f = v.Faktura;
            if (vsetkyFaktury.Any(x => x.Druh == DruhFaktury.Prijata && x.CisloDodavatela == f.CisloDodavatela && x.PartnerNazov == f.PartnerNazov)
                && !await Dialogy.Potvrd("E-faktúra", $"Faktúra {f.CisloDodavatela} od {f.PartnerNazov} už v evidencii je. Načítať ju znova?"))
                return;
            f.Cislo = Core.Services.Faktury.DalsieCislo(VybranaJednotka.FormatCislaFaktury, f.DatumVystavenia.Year, CislaFaktur(DruhFaktury.Prijata), "PF");
            var partner = Partneri.FirstOrDefault(p => v.Dodavatel.Ico is not null && p.Ico == v.Dodavatel.Ico)
                          ?? Partneri.FirstOrDefault(p => p.Nazov == v.Dodavatel.Nazov);
            if (partner is null)
            {
                partner = new Partner { JednotkaId = VybranaJednotka.Id, IcDph = f.PartnerIcDph, Iban = f.PartnerIban };
                RegisterIco.Dopln(partner, v.Dodavatel);
                db.Partneri.Add(partner);
                Partneri.Add(partner);
            }
            f.Partner = partner;
            PridajFakturu(f);
            Stav = $"Načítaná e-faktúra {f.CisloDodavatela} od {f.PartnerNazov} na {PdfVyuctovanie.Eur(f.Suma)}.";
            if (v.Upozornenia.Count > 0) await Dialogy.Sprava("E-faktúra", string.Join("\n", v.Upozornenia));
        }
        catch (Exception ex)
        {
            await Dialogy.Sprava("E-faktúra", "Súbor sa nepodarilo načítať: " + ex.Message);
        }
    }

    [RelayCommand]
    async Task ExportFaktur()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Kniha faktúr (Excel)", $"faktury-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        ExportExcel.KnihaFaktur(VybranaJednotka, vsetkyFaktury, Rok, s);
        Stav = $"Uložené: {s}";
    }

    /// <summary>Spoločenstvo: súčty prijatých faktúr podľa kategórie nákladov.</summary>
    void ObnovNakladyZFaktur()
    {
        if (!JeSpolocenstvo) return;
        var sucty = Core.Services.Faktury.NakladyZFaktur(vsetkyFaktury, Rok);
        foreach (var r in NakladRiadky) r.ZFaktur = sucty.TryGetValue(r.Kategoria.Id, out var x) ? x : null;
    }

    [RelayCommand]
    async Task PrevezmiNakladyZFaktur()
    {
        if (Dialogy is null) return;
        ObnovNakladyZFaktur();
        var zmeny = NakladRiadky.Where(r => r.ZFaktur is { } z && z != r.Suma && r.Upravitelne).ToList();
        if (zmeny.Count == 0) { await Dialogy.Sprava("Náklady z faktúr", "Žiadna kategória nemá prijaté faktúry s inou sumou, než je zadaná."); return; }
        if (!await Dialogy.Potvrd("Náklady z faktúr", "Prepísať skutočné náklady sumami z prijatých faktúr?\n\n" +
                                  string.Join("\n", zmeny.Select(r => $"{r.Nazov}: {PdfVyuctovanie.Eur(r.Suma)} → {PdfVyuctovanie.Eur(r.ZFaktur!.Value)}")))) return;
        foreach (var r in zmeny) r.Suma = r.ZFaktur!.Value;
    }

    // ---------- banka ----------

    void NacitajBanku()
    {
        var vybrany = VybranyBankovyPohyb?.P;
        BankovePohyby.Clear();
        foreach (var p in vsetkyPohyby.Where(p => p.Datum.Year == Rok).OrderBy(p => p.Datum).ThenBy(p => p.Id))
            BankovePohyby.Add(new PohybVm(p));
        VybranyBankovyPohyb = BankovePohyby.FirstOrDefault(x => x.P == vybrany);
        var prijmy = BankovePohyby.Where(p => p.Suma > 0).Sum(p => p.Suma);
        var vydavky = -BankovePohyby.Where(p => p.Suma < 0).Sum(p => p.Suma);
        SumaBanky = $"Príjmy: {PdfVyuctovanie.Eur(prijmy)}   Výdavky: {PdfVyuctovanie.Eur(vydavky)}   " +
                    $"Nespárované: {BankovePohyby.Count(p => p.P.Uhrady.Count == 0)}";
        NacitajDennik();
    }

    [RelayCommand]
    async Task ImportVypisu()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.OtvorSubor("Načítať bankový výpis (camt.053 XML)", "xml", "Výpis XML");
        if (s is null) return;
        try
        {
            var nove = Banka.NacitajCamt053(s);
            var existujuce = vsetkyPohyby.Select(p => p.Referencia).Where(r => r is not null).ToHashSet();
            var pridane = nove.Where(p => !existujuce.Contains(p.Referencia)).ToList();
            foreach (var p in pridane)
            {
                p.JednotkaId = VybranaJednotka.Id;
                db.BankovePohyby.Add(p);
                vsetkyPohyby.Add(p);
            }
            var sparovane = SparujAutomaticky(pridane);
            if (pridane.Count > 0 && pridane.Max(p => p.Datum).Year != Rok) Rok = pridane.Max(p => p.Datum).Year;
            FiltrujFaktury();
            NacitajBanku();
            await Dialogy.Sprava("Bankový výpis", $"Načítaných pohybov: {pridane.Count} (už v evidencii: {nove.Count - pridane.Count}).\n" +
                                                  $"Automaticky spárované s faktúrami: {sparovane}.\nOstatné môžete priradiť ručne.");
        }
        catch (Exception ex)
        {
            await Dialogy.Sprava("Bankový výpis", "Výpis sa nepodarilo načítať: " + ex.Message +
                "\n\nV internet bankingu zvoľte export výpisu vo formáte XML (camt.053).");
        }
    }

    int SparujAutomaticky(IEnumerable<BankovyPohyb> pohyby)
    {
        var n = 0;
        foreach (var x in Banka.Paruj(pohyby, vsetkyFaktury))
            if (x.Faktura is { } f) { Banka.Uhrad(f, x.Pohyb); n++; }
        return n;
    }

    [RelayCommand]
    async Task SparujBanku()
    {
        var n = SparujAutomaticky(vsetkyPohyby.Where(p => p.Datum.Year == Rok));
        FiltrujFaktury();
        NacitajBanku();
        if (Dialogy is not null) await Dialogy.Sprava("Párovanie", $"Spárovaných pohybov: {n}.");
    }

    [RelayCommand]
    async Task PriradPohyb()
    {
        var p = VybranyBankovyPohyb?.P;
        var f = FakturaNaPriradenie?.F;
        if (p is null || Dialogy is null) return;
        if (f is null) { await Dialogy.Sprava("Priradiť k faktúre", "Vyberte faktúru v zozname vedľa tlačidla."); return; }
        Banka.Uhrad(f, p);
        FiltrujFaktury();
        NacitajBanku();
    }

    [RelayCommand]
    void ZrusParovaniePohybu()
    {
        var p = VybranyBankovyPohyb?.P;
        if (p is null) return;
        foreach (var u in p.Uhrady.ToList())
        {
            u.Faktura?.Uhrady.Remove(u);
            p.Uhrady.Remove(u);
            db.UhradyFaktur.Remove(u);
        }
        FiltrujFaktury();
        NacitajBanku();
    }

    [RelayCommand]
    void PridajPohyb()
    {
        if (VybranaJednotka is null) return;
        var p = new BankovyPohyb { JednotkaId = VybranaJednotka.Id, Datum = DnesVRoku() };
        db.BankovePohyby.Add(p);
        vsetkyPohyby.Add(p);
        NacitajBanku();
        VybranyBankovyPohyb = BankovePohyby.FirstOrDefault(x => x.P == p);
    }

    [RelayCommand]
    async Task ZmazPohyb()
    {
        var p = VybranyBankovyPohyb?.P;
        if (p is null || Dialogy is null) return;
        if (p.Uhrady.Count > 0 && !await Dialogy.Potvrd("Zmazať pohyb", "Pohyb je priradený k faktúre. Zmazať ho aj s úhradou?")) return;
        foreach (var u in p.Uhrady.ToList()) { u.Faktura?.Uhrady.Remove(u); db.UhradyFaktur.Remove(u); }
        db.BankovePohyby.Remove(p);
        vsetkyPohyby.Remove(p);
        FiltrujFaktury();
        NacitajBanku();
    }

    [RelayCommand]
    async Task ExportBanky()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Bankové pohyby (Excel)", $"banka-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        ExportExcel.BankovePohyby(VybranaJednotka, vsetkyPohyby, Rok, s);
        Stav = $"Uložené: {s}";
    }

    // ---------- pokladňa ----------

    public decimal PokladnaPociatocnyStav
    {
        get => VybranaJednotka?.PokladnaPociatocnyStav ?? 0;
        set { if (VybranaJednotka is null) return; VybranaJednotka.PokladnaPociatocnyStav = value; OnPropertyChanged(); NacitajPokladnu(); }
    }

    public void NacitajPokladnu()
    {
        var vybrany = VybranyDoklad?.D;
        Doklady.Clear();
        if (VybranaJednotka is null) return;
        var (zac, riadky) = Pokladna.Kniha(vsetkyDoklady, VybranaJednotka.PokladnaPociatocnyStav, Rok);
        foreach (var r in riadky) Doklady.Add(new DokladVm(r.Doklad, r.Zostatok));
        VybranyDoklad = Doklady.FirstOrDefault(d => d.D == vybrany);
        var koniec = riadky.Count > 0 ? riadky[^1].Zostatok : zac;
        var zaporny = riadky.FirstOrDefault(r => r.Zostatok < 0);
        StavPokladne = $"Zostatok k 1. 1. {Rok}: {PdfVyuctovanie.Eur(zac)}   Príjmy: {PdfVyuctovanie.Eur(riadky.Sum(r => r.Prijem))}   " +
                       $"Výdavky: {PdfVyuctovanie.Eur(riadky.Sum(r => r.Vydavok))}   Zostatok: {PdfVyuctovanie.Eur(koniec)}" +
                       (zaporny is null ? "" : $"   ⚠ Po doklade {zaporny.Doklad.Cislo} je pokladňa v mínuse – skontrolujte dátumy a sumy.");
        OnPropertyChanged(nameof(PokladnaPociatocnyStav));
        NacitajDennik();
    }

    [RelayCommand]
    void PridajPrijem() => PridajDoklad(DruhPokladnicnehoDokladu.Prijem);

    [RelayCommand]
    void PridajVydavok() => PridajDoklad(DruhPokladnicnehoDokladu.Vydavok);

    void PridajDoklad(DruhPokladnicnehoDokladu druh)
    {
        if (VybranaJednotka is null) return;
        var datum = DnesVRoku();
        var d = new PokladnicnyDoklad
        {
            JednotkaId = VybranaJednotka.Id, Druh = druh, Datum = datum,
            Cislo = Pokladna.DalsieCislo(druh, datum.Year, vsetkyDoklady.Select(x => x.Cislo)),
        };
        db.PokladnicneDoklady.Add(d);
        vsetkyDoklady.Add(d);
        NacitajPokladnu();
        VybranyDoklad = Doklady.FirstOrDefault(x => x.D == d);
    }

    [RelayCommand]
    async Task ZmazDoklad()
    {
        var d = VybranyDoklad?.D;
        if (d is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať doklad", $"Zmazať doklad {d.Cislo}?" + (d.Uhrady.Count > 0 ? " Zruší sa aj úhrada faktúry." : ""))) return;
        foreach (var u in d.Uhrady.ToList()) { u.Faktura?.Uhrady.Remove(u); db.UhradyFaktur.Remove(u); }
        db.PokladnicneDoklady.Remove(d);
        vsetkyDoklady.Remove(d);
        FiltrujFaktury();
        NacitajPokladnu();
    }

    [RelayCommand]
    async Task DokladPdf()
    {
        var d = VybranyDoklad?.D;
        if (d is null || Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Pokladničný doklad (PDF)", $"{d.Cislo}.pdf", "pdf", "PDF");
        if (s is null) return;
        await File.WriteAllBytesAsync(s, PdfFaktury.PokladnicnyDoklad(d, VybranaJednotka));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task PokladnicnaKnihaPdf()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Pokladničná kniha (PDF)", $"pokladnicna-kniha-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        var (zac, riadky) = Pokladna.Kniha(vsetkyDoklady, VybranaJednotka.PokladnaPociatocnyStav, Rok);
        await File.WriteAllBytesAsync(s, PdfFaktury.PokladnicnaKniha(VybranaJednotka, Rok, zac, riadky));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task PokladnicnaKnihaExcel()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Pokladničná kniha (Excel)", $"pokladnicna-kniha-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        var (zac, riadky) = Pokladna.Kniha(vsetkyDoklady, VybranaJednotka.PokladnaPociatocnyStav, Rok);
        ExportExcel.PokladnicnaKniha(VybranaJednotka, Rok, zac, riadky, s);
        Stav = $"Uložené: {s}";
    }
}
