using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Okná a dialógy, ktoré model potrebuje od hlavného okna.</summary>
public interface IDialogy
{
    Task<string?> UlozSubor(string titulok, string navrhovanyNazov, string pripona, string popisTypu);
    Task<string?> OtvorSubor(string titulok, string pripona, string popisTypu);
    Task Sprava(string titulok, string text);
    Task<bool> Potvrd(string titulok, string text);
    Task<bool> ImportPlatieb(IReadOnlyList<ImportRiadok> riadky, IReadOnlyList<Priestor> priestory);
    /// <summary>Zobrazí predmet a text e-mailu na úpravu; null = zrušené.</summary>
    Task<(string Predmet, string Text)?> TextEmailu(string titulok, string info, string predmet, string text);
    Task<string?> VstupnyText(string titulok, string otazka, string predvolene);
}

public partial class MainViewModel : ObservableObject
{
    public string CestaDb { get; }
    AppDbContext db;
    public IDialogy? Dialogy { get; set; }

    public ObservableCollection<UctovnaJednotka> Jednotky { get; } = new();
    public ObservableCollection<Priestor> Priestory { get; } = new();
    public ObservableCollection<Vlastnik> Vlastnici { get; } = new();
    public ObservableCollection<BankovyUcet> Ucty { get; } = new();
    public ObservableCollection<KategoriaNakladov> Kategorie { get; } = new();
    public ObservableCollection<PredpisRiadok> PredpisRiadky { get; } = new();
    public ObservableCollection<RokRiadok> RokRiadky { get; } = new();
    public ObservableCollection<NakladRiadok> NakladRiadky { get; } = new();
    public ObservableCollection<Uhrada> Uhrady { get; } = new();
    public ObservableCollection<VyuctovanieBytu> VyuctovanieBytov { get; } = new();
    public ObservableCollection<string> Upozornenia { get; } = new();

    /// <summary>Vyvolá sa, keď sa zmenia kategórie – tabuľka predpisov musí prekresliť stĺpce.</summary>
    public event Action? KategorieZmenene;

    [ObservableProperty] private UctovnaJednotka? vybranaJednotka;
    [ObservableProperty] private Priestor? vybranyPriestor;
    [ObservableProperty] private Vlastnik? vybranyVlastnik;
    [ObservableProperty] private BankovyUcet? vybranyUcet;
    [ObservableProperty] private KategoriaNakladov? vybranaKategoria;
    [ObservableProperty] private Uhrada? vybranaUhrada;
    [ObservableProperty] private VyuctovanieBytu? vybraneVyuctovanie;
    [ObservableProperty] private Priestor? filterUhradPriestor;
    [ObservableProperty] private int rok = DateTime.Today.Month >= 6 ? DateTime.Today.Year : DateTime.Today.Year - 1;
    [ObservableProperty] private string stav = "";
    [ObservableProperty] private string sumaUhrad = "";
    [ObservableProperty] private string sumaVyuctovania = "";

    // hromadné nastavenie predpisu
    [ObservableProperty] private KategoriaNakladov? hromadnaKategoria;
    [ObservableProperty] private decimal hromadnaSadzba;
    [ObservableProperty] private bool hromadnePodlaPlochy = true;

    // údaje na vyúčtovací list
    [ObservableProperty] private string miestoVystavenia = "";
    [ObservableProperty] private DateOnly? datumVystavenia = DateOnly.FromDateTime(DateTime.Today);

    public bool JeSpolocenstvo => VybranaJednotka?.Typ == TypJednotky.Spolocenstvo;
    public bool JeFirma => VybranaJednotka is not null && !JeSpolocenstvo;
    public bool MaJednotku => VybranaJednotka is not null;
    public bool NemaJednotku => VybranaJednotka is null;

    public MainViewModel(string cestaDb)
    {
        CestaDb = cestaDb;
        Databaza.Priprav(cestaDb);
        db = AppDbContext.Open(cestaDb);
        NacitajJednotky();
    }

    /// <summary>Počas načítania sa ignorujú zmeny vyvolané ovládacími prvkami.</summary>
    public bool Nacitava { get; private set; }

    void NacitajJednotky(int? vybratId = null)
    {
        Nacitava = true;
        try
        {
            VybranaJednotka = null;
            Jednotky.Clear();
            foreach (var j in db.Jednotky.OrderBy(j => j.Nazov).ToList()) Jednotky.Add(j);
            VybranaJednotka = Jednotky.FirstOrDefault(j => j.Id == vybratId) ?? Jednotky.FirstOrDefault();
        }
        finally
        {
            Nacitava = false;
        }
        Stav = $"Dáta: {CestaDb}";
    }

    partial void OnVybranaJednotkaChanged(UctovnaJednotka? oldValue, UctovnaJednotka? newValue)
    {
        if (oldValue is not null) Uloz();
        OnPropertyChanged(nameof(JeSpolocenstvo));
        OnPropertyChanged(nameof(JeFirma));
        OnPropertyChanged(nameof(MaJednotku));
        OnPropertyChanged(nameof(NemaJednotku));
        MiestoVystavenia = newValue?.Obec ?? "";
        NacitajSpolocenstvo();
    }

    partial void OnRokChanged(int value)
    {
        Uloz();
        NacitajRok();
    }

    partial void OnVybranyPriestorChanged(Priestor? value)
    {
        Vlastnici.Clear();
        if (value is not null)
            foreach (var v in value.Vlastnici.OrderByDescending(v => v.Hlavny).ThenBy(v => v.Id)) Vlastnici.Add(v);
        VybranyVlastnik = Vlastnici.FirstOrDefault();
    }

    partial void OnVybranyVlastnikChanged(Vlastnik? value)
    {
        Ucty.Clear();
        if (value is not null)
            foreach (var u in value.Ucty) Ucty.Add(u);
    }

    partial void OnFilterUhradPriestorChanged(Priestor? value) => NacitajUhrady();

    void NacitajSpolocenstvo()
    {
        Priestory.Clear();
        Kategorie.Clear();
        var j = VybranaJednotka;
        if (j is null || j.Typ != TypJednotky.Spolocenstvo)
        {
            NacitajRok();
            return;
        }

        var priestory = db.Priestory.Where(p => p.JednotkaId == j.Id)
            .Include(p => p.Vlastnici).ThenInclude(v => v.Ucty)
            .Include(p => p.Roky).Include(p => p.Predpisy).Include(p => p.Uhrady)
            .AsSplitQuery().ToList()
            .OrderBy(p => p.Cislo, CisloComparer.Instance);
        foreach (var p in priestory) Priestory.Add(p);
        foreach (var k in db.Kategorie.Where(k => k.JednotkaId == j.Id).Include(k => k.Naklady).OrderBy(k => k.Poradie).ThenBy(k => k.Id))
            Kategorie.Add(k);
        VybranyPriestor = Priestory.FirstOrDefault();
        HromadnaKategoria = Kategorie.FirstOrDefault();
        FilterUhradPriestor = null;
        NacitajRok();
    }

    /// <summary>Naplní tabuľky závislé od vybraného roka.</summary>
    public void NacitajRok()
    {
        PredpisRiadky.Clear();
        RokRiadky.Clear();
        NakladRiadky.Clear();
        VyuctovanieBytov.Clear();
        Upozornenia.Clear();
        SumaVyuctovania = "";
        NacitajCast2();
        if (!JeSpolocenstvo)
        {
            Uhrady.Clear();
            KategorieZmenene?.Invoke();
            return;
        }

        foreach (var p in Priestory)
        {
            PredpisRiadky.Add(new PredpisRiadok(db, p, Kategorie, Rok));
            RokRiadky.Add(new RokRiadok(p, ZabezpecRok(p, Rok)));
        }
        foreach (var k in Kategorie)
            NakladRiadky.Add(new NakladRiadok(db, k, Rok));
        NacitajUhrady();
        NacitajKontoAFond();
        ObnovNakladyZFaktur();
        KategorieZmenene?.Invoke();
    }

    PriestorRok ZabezpecRok(Priestor p, int rok)
    {
        var r = p.Roky.FirstOrDefault(x => x.Rok == rok);
        if (r is null)
        {
            r = new PriestorRok { Priestor = p, Rok = rok };
            p.Roky.Add(r);
            db.PriestorRoky.Add(r);
        }
        return r;
    }

    void NacitajUhrady()
    {
        Uhrady.Clear();
        var zoznam = Priestory
            .Where(p => FilterUhradPriestor is null || p == FilterUhradPriestor)
            .SelectMany(p => p.Uhrady)
            .Where(u => u.Datum.Year == Rok)
            .OrderBy(u => u.Datum).ThenBy(u => u.Id);
        foreach (var u in zoznam) Uhrady.Add(u);
        PrepocitajSumuUhrad();
    }

    public void PrepocitajSumuUhrad() =>
        SumaUhrad = $"Počet platieb: {Uhrady.Count}   Spolu: {PdfVyuctovanie.Eur(Uhrady.Sum(u => u.Suma))}";

    // ---------- ukladanie ----------

    [RelayCommand]
    public void Uloz()
    {
        try
        {
            if (!db.ChangeTracker.HasChanges()) return;
            db.SaveChanges();
            Stav = $"Uložené {DateTime.Now:HH:mm:ss} – {CestaDb}";
        }
        catch (DbUpdateException ex)
        {
            Stav = "Chyba pri ukladaní: " + (ex.InnerException?.Message ?? ex.Message);
            Dialogy?.Sprava("Zmeny sa nepodarilo uložiť", Stav + "\n\nSkontrolujte, či sa čísla priestorov neopakujú.");
        }
    }

    // ---------- účtovné jednotky ----------

    [RelayCommand]
    void PridajSpolocenstvo() => PridajJednotku(TypJednotky.Spolocenstvo, "Nové spoločenstvo");

    [RelayCommand]
    void PridajFirmu() => PridajJednotku(TypJednotky.Sro, "Nová firma");

    void PridajJednotku(TypJednotky typ, string nazov)
    {
        var j = new UctovnaJednotka { Typ = typ, Nazov = nazov };
        if (typ == TypJednotky.Spolocenstvo)
            foreach (var (n, k, fo, i) in new[]
            {
                ("správcovská činnosť", KlucRozpoctu.RovnakoNaPriestor, false, 1),
                ("upratovanie spol. častí domu, kosenie, sneh", KlucRozpoctu.Plocha, false, 2),
                ("el. energia spol. priestorov", KlucRozpoctu.Plocha, false, 3),
                ("stála služba", KlucRozpoctu.RovnakoNaPriestor, false, 4),
                ("odmeny pre volené funkcie", KlucRozpoctu.RovnakoNaPriestor, false, 5),
                ("vodné a stočné", KlucRozpoctu.SpotrebaVody, false, 6),
                ("iné náklady", KlucRozpoctu.RovnakoNaPriestor, false, 7),
                ("fond opráv", KlucRozpoctu.Plocha, true, 8),
            })
                j.Kategorie.Add(new KategoriaNakladov { Nazov = n, Kluc = k, JeFondOprav = fo, Poradie = i });
        db.Jednotky.Add(j);
        db.SaveChanges();
        NacitajJednotky(j.Id);
    }

    [RelayCommand]
    async Task ZmazJednotku()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať", $"Naozaj zmazať „{j.Nazov}“ so všetkými údajmi (priestory, vlastníci, platby)?\n\nOdporúčame najprv urobiť zálohu."))
            return;
        db.Jednotky.Remove(j);
        db.SaveChanges();
        NacitajJednotky();
    }

    /// <summary>Po zmene názvu alebo typu v detaile obnoví zoznam vľavo aj záložky.</summary>
    public void ObnovNazovJednotky()
    {
        var j = VybranaJednotka;
        if (j is null || Nacitava) return;
        Uloz();
        NacitajJednotky(j.Id);
    }

    [RelayCommand]
    void PodielyZPlochy()
    {
        var spolu = Priestory.Sum(p => (int)Math.Round(p.Plocha * 100m));
        foreach (var p in Priestory)
        {
            p.PodielCitatel = (int)Math.Round(p.Plocha * 100m);
            p.PodielMenovatel = spolu;
        }
        Uloz();
        var vybrany = VybranyPriestor;
        var zoznam = Priestory.ToList();
        Priestory.Clear();
        foreach (var p in zoznam) Priestory.Add(p);
        VybranyPriestor = vybrany;
    }

    // ---------- priestory, vlastníci, účty ----------

    [RelayCommand]
    void PridajPriestor()
    {
        var j = VybranaJednotka;
        if (j is null) return;
        var cisla = Priestory.Select(p => int.TryParse(p.Cislo, out var c) ? c : 0).DefaultIfEmpty(0).Max() + 1;
        var p = new Priestor { JednotkaId = j.Id, Cislo = cisla.ToString(), VariabilnySymbol = cisla.ToString() };
        db.Priestory.Add(p);
        Priestory.Add(p);
        VybranyPriestor = p;
        PredpisRiadky.Add(new PredpisRiadok(db, p, Kategorie, Rok));
        RokRiadky.Add(new RokRiadok(p, ZabezpecRok(p, Rok)));
    }

    [RelayCommand]
    async Task ZmazPriestor()
    {
        var p = VybranyPriestor;
        if (p is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať priestor", $"Zmazať priestor {p.Cislo} aj s vlastníkmi a platbami?")) return;
        db.Priestory.Remove(p);
        Priestory.Remove(p);
        Uloz();
        NacitajRok();
    }

    [RelayCommand]
    void PridajVlastnika()
    {
        var p = VybranyPriestor;
        if (p is null) return;
        var v = new Vlastnik { Priestor = p, Meno = "", Hlavny = p.Vlastnici.Count == 0 };
        p.Vlastnici.Add(v);
        db.Vlastnici.Add(v);
        Vlastnici.Add(v);
        VybranyVlastnik = v;
    }

    [RelayCommand]
    void ZmazVlastnika()
    {
        var v = VybranyVlastnik;
        if (v is null || VybranyPriestor is null) return;
        VybranyPriestor.Vlastnici.Remove(v);
        db.Vlastnici.Remove(v);
        Vlastnici.Remove(v);
        VybranyVlastnik = Vlastnici.FirstOrDefault();
    }

    [RelayCommand]
    void PridajUcet()
    {
        var v = VybranyVlastnik;
        if (v is null) return;
        var u = new BankovyUcet { Vlastnik = v, Iban = "SK" };
        v.Ucty.Add(u);
        db.BankoveUcty.Add(u);
        Ucty.Add(u);
        VybranyUcet = u;
    }

    [RelayCommand]
    void ZmazUcet()
    {
        var u = VybranyUcet;
        if (u is null || VybranyVlastnik is null) return;
        VybranyVlastnik.Ucty.Remove(u);
        db.BankoveUcty.Remove(u);
        Ucty.Remove(u);
    }

    public IEnumerable<string> NeplatneIbany() =>
        Priestory.SelectMany(p => p.Vlastnici.SelectMany(v => v.Ucty.Select(u => (p, v, u))))
            .Where(x => !Iban.JePlatny(x.u.Iban))
            .Select(x => $"byt {x.p.Cislo}, {x.v.Meno}: {x.u.Iban}");

    // ---------- kategórie a náklady ----------

    [RelayCommand]
    void PridajKategoriu()
    {
        var j = VybranaJednotka;
        if (j is null) return;
        var k = new KategoriaNakladov { JednotkaId = j.Id, Nazov = "nová položka", Kluc = KlucRozpoctu.Plocha, Poradie = Kategorie.Count + 1 };
        db.Kategorie.Add(k);
        Kategorie.Add(k);
        VybranaKategoria = k;
        Uloz();
        NacitajRok();
    }

    [RelayCommand]
    async Task ZmazKategoriu()
    {
        var k = VybranaKategoria;
        if (k is null || Dialogy is null) return;
        if (!await Dialogy.Potvrd("Zmazať položku", $"Zmazať položku „{k.Nazov}“ aj s jej predpismi a nákladmi vo všetkých rokoch?")) return;
        foreach (var p in Priestory) p.Predpisy.RemoveAll(x => x.KategoriaId == k.Id);
        db.Kategorie.Remove(k);
        Kategorie.Remove(k);
        Uloz();
        NacitajRok();
    }

    /// <summary>Po úprave kategórií (názov, kľúč, poradie) prekreslí tabuľky roka.</summary>
    [RelayCommand]
    void PotvrdKategorie()
    {
        Uloz();
        var zoradene = Kategorie.OrderBy(k => k.Poradie).ThenBy(k => k.Id).ToList();
        Kategorie.Clear();
        foreach (var k in zoradene) Kategorie.Add(k);
        NacitajRok();
    }

    // ---------- predpisy a rok ----------

    [RelayCommand]
    void NastavPredpisHromadne()
    {
        var k = HromadnaKategoria;
        if (k is null) return;
        var i = Kategorie.IndexOf(k);
        foreach (var r in PredpisRiadky)
        {
            var suma = HromadnePodlaPlochy ? r.Priestor.Plocha * HromadnaSadzba : HromadnaSadzba;
            r.Bunky[i].Suma = Math.Round(suma, 2, MidpointRounding.AwayFromZero);
        }
        Uloz();
    }

    [RelayCommand]
    async Task KopirujZMinulehoRoka()
    {
        if (Dialogy is null) return;
        var minuly = Rok - 1;
        if (!Priestory.Any(p => p.Predpisy.Any(x => x.Rok == minuly) || p.Roky.Any(x => x.Rok == minuly)))
        {
            await Dialogy.Sprava("Prevziať z minulého roka", $"Za rok {minuly} nie sú zadané žiadne údaje.");
            return;
        }
        if (!await Dialogy.Potvrd("Prevziať z minulého roka",
                $"Pre rok {Rok} sa prevezme z roka {minuly}:\n• predpisy záloh a poistného (ak v roku {Rok} ešte nie sú zadané),\n• konečný stav vodomeru ako počiatočný stav,\n• výsledok vyúčtovania {minuly} ako zostatok minulého roka.\n\nPokračovať?"))
            return;

        var vysledokMinuly = Vyuctovanie.Vypocitaj(minuly, Priestory.ToList(), Kategorie.ToList());
        foreach (var p in Priestory)
        {
            var teraz = ZabezpecRok(p, Rok);
            var predtym = p.Roky.FirstOrDefault(x => x.Rok == minuly);
            if (!p.Predpisy.Any(x => x.Rok == Rok))
                foreach (var x in p.Predpisy.Where(x => x.Rok == minuly).ToList())
                {
                    var novy = new PredpisPolozky { Priestor = p, KategoriaId = x.KategoriaId, Rok = Rok, RocnaSuma = x.RocnaSuma };
                    p.Predpisy.Add(novy);
                    db.Predpisy.Add(novy);
                }
            if (predtym is not null)
            {
                if (teraz.PoistnePredpis == 0) teraz.PoistnePredpis = predtym.PoistnePredpis;
                teraz.StavVodomeruZaciatok ??= predtym.StavVodomeruKoniec;
                teraz.DatumOdpoctuZaciatok ??= predtym.DatumOdpoctuKoniec;
            }
            var b = vysledokMinuly.Byty.FirstOrDefault(x => x.Priestor == p);
            if (b is not null && teraz.ZostatokMinulyRok == 0) teraz.ZostatokMinulyRok = b.Vysledok;
        }
        Uloz();
        NacitajRok();
        Stav = $"Údaje z roka {minuly} prevzaté do roka {Rok}.";
    }

    // ---------- úhrady ----------

    [RelayCommand]
    void PridajUhradu()
    {
        var p = FilterUhradPriestor ?? VybranaUhrada?.Priestor ?? Priestory.FirstOrDefault();
        if (p is null) return;
        var datum = new DateOnly(Rok, Math.Min(DateTime.Today.Year == Rok ? DateTime.Today.Month : 12, 12), Math.Min(DateTime.Today.Day, 28));
        var predpis = p.Predpisy.Where(x => x.Rok == Rok).Sum(x => x.RocnaSuma);
        var u = new Uhrada { Priestor = p, Datum = datum, Suma = Math.Round(predpis / 12m, 2), VariabilnySymbol = p.VariabilnySymbol };
        p.Uhrady.Add(u);
        db.Uhrady.Add(u);
        Uhrady.Add(u);
        VybranaUhrada = u;
        PrepocitajSumuUhrad();
    }

    [RelayCommand]
    void ZmazUhradu()
    {
        var u = VybranaUhrada;
        if (u is null) return;
        u.Priestor?.Uhrady.Remove(u);
        db.Uhrady.Remove(u);
        Uhrady.Remove(u);
        PrepocitajSumuUhrad();
    }

    [RelayCommand]
    async Task ImportujVypis()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var subor = await Dialogy.OtvorSubor("Bankový výpis (camt.053 XML)", "xml", "Výpis camt.053");
        if (subor is null) return;
        IReadOnlyList<PlatbaZVypisu> platby;
        try
        {
            platby = ImportBanky.NacitajCamt053(subor);
        }
        catch (Exception ex)
        {
            await Dialogy.Sprava("Výpis sa nepodarilo načítať", ex.Message);
            return;
        }
        if (platby.Count == 0)
        {
            await Dialogy.Sprava("Import výpisu", "Výpis neobsahuje žiadne prijaté platby.");
            return;
        }

        var parovane = ImportBanky.Paruj(platby, Priestory.ToList());
        var riadky = parovane.Select(p => new ImportRiadok(p, JeDuplicitna(p.Platba))).ToList();
        if (!await Dialogy.ImportPlatieb(riadky, Priestory.ToList())) return;

        int pocet = 0;
        foreach (var r in riadky.Where(r => r.Importovat && r.Priestor is not null))
        {
            var u = new Uhrada
            {
                Priestor = r.Priestor, Datum = r.Platba.Datum, Suma = r.Platba.Suma, Ucel = r.Ucel,
                IbanPlatitela = r.Platba.Iban, VariabilnySymbol = r.Platba.VariabilnySymbol,
                Poznamka = string.Join(" ", new[] { r.Platba.Meno, r.Platba.Sprava }.Where(s => !string.IsNullOrWhiteSpace(s))),
            };
            r.Priestor!.Uhrady.Add(u);
            db.Uhrady.Add(u);
            pocet++;
        }
        Uloz();
        NacitajUhrady();
        Stav = $"Importovaných platieb: {pocet}.";
    }

    bool JeDuplicitna(PlatbaZVypisu p) =>
        Priestory.SelectMany(x => x.Uhrady).Any(u => u.Datum == p.Datum && u.Suma == p.Suma && Iban.Normalizuj(u.IbanPlatitela) == (p.Iban ?? ""));

    // ---------- vyúčtovanie a exporty ----------

    [RelayCommand]
    void Vypocitaj()
    {
        Uloz();
        var v = Vyuctovanie.Vypocitaj(Rok, Priestory.ToList(), Kategorie.ToList());
        VyuctovanieBytov.Clear();
        foreach (var b in v.Byty) VyuctovanieBytov.Add(b);
        Upozornenia.Clear();
        foreach (var u in v.Upozornenia) Upozornenia.Add(u);
        var doplatky = v.Byty.Where(b => b.Vysledok < 0).Sum(b => b.Vysledok);
        var preplatky = v.Byty.Where(b => b.Vysledok > 0).Sum(b => b.Vysledok);
        SumaVyuctovania = $"Nedoplatky spolu: {PdfVyuctovanie.Eur(-doplatky)}   Preplatky spolu: {PdfVyuctovanie.Eur(preplatky)}";
        VybraneVyuctovanie = VyuctovanieBytov.FirstOrDefault();
    }

    NastaveniaVyuctovania Nastavenia() =>
        new(DatumVystavenia ?? DateOnly.FromDateTime(DateTime.Today), MiestoVystavenia);

    [RelayCommand]
    async Task PdfVsetky()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        if (VyuctovanieBytov.Count == 0) Vypocitaj();
        var s = await Dialogy.UlozSubor("Uložiť vyúčtovanie (PDF)", $"vyuctovanie-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        await Task.Run(() => PdfVyuctovanie.Uloz(VybranaJednotka, VyuctovanieBytov, Nastavenia(), s));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task PdfVybrany()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        if (VyuctovanieBytov.Count == 0) Vypocitaj();
        var b = VybraneVyuctovanie;
        if (b is null) return;
        var s = await Dialogy.UlozSubor("Uložiť vyúčtovanie bytu (PDF)", $"vyuctovanie-{Rok}-byt-{b.Priestor.Cislo}.pdf", "pdf", "PDF");
        if (s is null) return;
        await Task.Run(() => PdfVyuctovanie.Uloz(VybranaJednotka, new[] { b }, Nastavenia(), s));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task ExcelVyuctovanie()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        Vypocitaj();
        var s = await Dialogy.UlozSubor("Export vyúčtovania do Excelu", $"vyuctovanie-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        ExportExcel.Vyuctovanie(VybranaJednotka, new VysledokVyuctovania(Rok, VyuctovanieBytov.ToList(), Upozornenia.ToList()), s);
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task ExcelUhrady()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        Uloz();
        var s = await Dialogy.UlozSubor("Export úhrad do Excelu", $"uhrady-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        ExportExcel.UhradyZaRok(VybranaJednotka, Priestory.ToList(), Rok, s);
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    void VytvorUkazku()
    {
        Uloz();
        var j = Ukazka.Vytvor(db, Rok);
        if (!db.Jednotky.Any(x => x.Typ != TypJednotky.Spolocenstvo)) Ukazka.VytvorFirmu(db, Rok);
        NacitajJednotky(j.Id);
        Stav = "Vytvorené ukážkové spoločenstvo a firma – môžete si v nich všetko vyskúšať a potom ich zmazať.";
    }

    // ---------- záloha ----------

    [RelayCommand]
    async Task Zalohuj()
    {
        if (Dialogy is null) return;
        Uloz();
        var s = await Dialogy.UlozSubor("Záloha všetkých údajov", Zaloha.NavrhniNazov(DateTime.Now), "zip", "Záloha (ZIP)");
        if (s is null) return;
        Zaloha.Vytvor(CestaDb, s);
        Stav = $"Záloha uložená: {s}";
        await Dialogy.Sprava("Záloha", $"Všetky údaje boli zálohované do súboru:\n{s}\n\nSúbor si skopírujte na USB kľúč alebo do cloudu.");
    }

    [RelayCommand]
    async Task Obnov()
    {
        if (Dialogy is null) return;
        var s = await Dialogy.OtvorSubor("Obnoviť zo zálohy", "zip", "Záloha (ZIP)");
        if (s is null) return;
        if (!await Dialogy.Potvrd("Obnoviť zo zálohy", "Všetky súčasné údaje sa nahradia obsahom zálohy. Súčasný stav sa odloží vedľa ako súbor .pred-obnovou.\n\nPokračovať?"))
            return;
        Uloz();
        db.Dispose();
        try
        {
            Zaloha.Obnov(s, CestaDb);
        }
        catch (Exception ex)
        {
            await Dialogy.Sprava("Obnova zlyhala", ex.Message);
        }
        Databaza.Priprav(CestaDb);
        db = AppDbContext.Open(CestaDb);
        NacitajJednotky();
        Stav = $"Údaje obnovené zo zálohy {s}";
    }
}
