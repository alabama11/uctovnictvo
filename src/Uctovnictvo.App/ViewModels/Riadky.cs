using CommunityToolkit.Mvvm.ComponentModel;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Riadok tabuľky predpisov: jeden priestor, stĺpec pre každú kategóriu nákladov.</summary>
public partial class PredpisRiadok : ObservableObject
{
    public Priestor Priestor { get; }
    public List<PredpisBunka> Bunky { get; } = new();

    public PredpisRiadok(AppDbContext db, Priestor priestor, IEnumerable<KategoriaNakladov> kategorie, int rok)
    {
        Priestor = priestor;
        foreach (var k in kategorie)
            Bunky.Add(new PredpisBunka(db, priestor, k, rok, this));
    }

    public decimal Spolu => Bunky.Sum(b => b.Suma);
    public decimal Mesacne => Math.Round(Spolu / 12m, 2, MidpointRounding.AwayFromZero);

    internal void Prepocitaj()
    {
        OnPropertyChanged(nameof(Spolu));
        OnPropertyChanged(nameof(Mesacne));
    }
}

public partial class PredpisBunka : ObservableObject
{
    readonly AppDbContext db;
    readonly Priestor priestor;
    readonly KategoriaNakladov kategoria;
    readonly int rok;
    readonly PredpisRiadok riadok;

    public PredpisBunka(AppDbContext db, Priestor priestor, KategoriaNakladov kategoria, int rok, PredpisRiadok riadok)
    {
        this.db = db; this.priestor = priestor; this.kategoria = kategoria; this.rok = rok; this.riadok = riadok;
    }

    PredpisPolozky? Entita => priestor.Predpisy.FirstOrDefault(p => p.KategoriaId == kategoria.Id && p.Rok == rok);

    public decimal Suma
    {
        get => Entita?.RocnaSuma ?? 0m;
        set
        {
            var e = Entita;
            if (e is null)
            {
                if (value == 0) return;
                e = new PredpisPolozky { Priestor = priestor, Kategoria = kategoria, KategoriaId = kategoria.Id, Rok = rok };
                priestor.Predpisy.Add(e);
                db.Predpisy.Add(e);
            }
            e.RocnaSuma = value;
            OnPropertyChanged();
            riadok.Prepocitaj();
        }
    }
}

/// <summary>Ročné údaje priestoru: odpočty vody podľa tlačiva (stĺpce 1–13), poistné, zostatok z minulého roka.</summary>
public partial class RokRiadok : ObservableObject
{
    readonly Func<VodaRoka?> voda;
    readonly Func<KategoriaNakladov?> vodaKat;
    public Priestor Priestor { get; }
    public PriestorRok Data { get; }

    public RokRiadok(Priestor priestor, PriestorRok data, Func<VodaRoka?>? voda = null, Func<KategoriaNakladov?>? vodaKat = null)
    {
        Priestor = priestor;
        Data = data;
        this.voda = voda ?? (() => null);
        this.vodaKat = vodaKat ?? (() => null);
    }

    void Zmena([System.Runtime.CompilerServices.CallerMemberName] string? meno = null)
    {
        OnPropertyChanged(meno);
        foreach (var n in new[] { nameof(Spotreba1), nameof(Spotreba2), nameof(Spotreba), nameof(Cena1), nameof(Cena2), nameof(VodaSpolu), nameof(VodaRozdiel) })
            OnPropertyChanged(n);
    }

    public string Vlastnik => Priestor.MenaVlastnikov;
    /// <summary>Stĺpec 1.</summary>
    public string? Vodomer1 { get => Priestor.CisloVodomeru; set { Priestor.CisloVodomeru = value; OnPropertyChanged(); } }
    /// <summary>Stĺpec 2.</summary>
    public decimal? Stav2 { get => Data.StavVodomeruZaciatok; set { Data.StavVodomeruZaciatok = value; Zmena(); } }
    /// <summary>Stĺpec 3.</summary>
    public decimal? Stav3 { get => Data.StavVodomeruKoniec; set { Data.StavVodomeruKoniec = value; Zmena(); } }
    /// <summary>Stĺpec 4 = 3 − 2 (vrátane staršie zadanej spotreby vymeneného vodomeru).</summary>
    public decimal Spotreba1 => Data.Spotreba1 + Data.SpotrebaVymenenyVodomer;
    /// <summary>Stĺpec 5 = 4 × 1. cena.</summary>
    public decimal Cena1 => voda() is { } v ? Vyuctovanie.NakladVody(Data, v).Naklad1 : 0m;
    /// <summary>Stĺpec 6.</summary>
    public string? Vodomer2 { get => Priestor.CisloVodomeru2; set { Priestor.CisloVodomeru2 = value; OnPropertyChanged(); } }
    /// <summary>Stĺpec 7.</summary>
    public decimal? Stav7 { get => Data.StavVodomeru2Zaciatok; set { Data.StavVodomeru2Zaciatok = value; Zmena(); } }
    /// <summary>Stĺpec 8.</summary>
    public decimal? Stav8 { get => Data.StavVodomeru2Koniec; set { Data.StavVodomeru2Koniec = value; Zmena(); } }
    /// <summary>Stĺpec 9 = 8 − 7.</summary>
    public decimal Spotreba2 => Data.Spotreba2;
    /// <summary>Stĺpec 10 = 9 × 2. cena.</summary>
    public decimal Cena2 => voda() is { } v ? Vyuctovanie.NakladVody(Data, v).Naklad2 : 0m;
    /// <summary>Stĺpec 11 – predpis záloh v položke vodné a stočné.</summary>
    public decimal Zaloha => vodaKat() is { } k ? Priestor.Predpisy.Where(x => x.Rok == Data.Rok && x.KategoriaId == k.Id).Sum(x => x.RocnaSuma) : 0m;
    /// <summary>Stĺpec 12 = 5 + 10.</summary>
    public decimal VodaSpolu => Cena1 + Cena2;
    /// <summary>Stĺpec 13 = 11 − 12: − nedoplatok / + preplatok.</summary>
    public decimal VodaRozdiel => Zaloha - VodaSpolu;
    public decimal Spotreba => Data.SpotrebaVody;

    public decimal PoistnePredpis { get => Data.PoistnePredpis; set { Data.PoistnePredpis = value; OnPropertyChanged(); } }
    public decimal ZostatokMinulyRok { get => Data.ZostatokMinulyRok; set { Data.ZostatokMinulyRok = value; OnPropertyChanged(); } }

    public void Obnov() => OnPropertyChanged(string.Empty);
}

/// <summary>Skutočný náklad domu v kategórii za vybraný rok.</summary>
public partial class NakladRiadok : ObservableObject
{
    readonly AppDbContext db;
    readonly int rok;
    public KategoriaNakladov Kategoria { get; }

    public NakladRiadok(AppDbContext db, KategoriaNakladov kategoria, int rok)
    {
        this.db = db; this.rok = rok; Kategoria = kategoria;
    }

    public string Nazov => Kategoria.Nazov;
    public string Kluc => Kategoria.JeFondOprav ? "fond opráv – nevyúčtováva sa" : Vyuctovanie.NazovKluca(Kategoria.Kluc)
        + (string.IsNullOrWhiteSpace(Kategoria.LenPriestory) ? "" : $", len {Kategoria.LenPriestory}");
    public bool Upravitelne => !Kategoria.JeFondOprav;

    /// <summary>Súčet prijatých faktúr zaradených do tejto kategórie (null = žiadne).</summary>
    [ObservableProperty] private decimal? zFaktur;

    public decimal Suma
    {
        get => Kategoria.Naklady.FirstOrDefault(n => n.Rok == rok)?.Suma ?? 0m;
        set
        {
            var e = Kategoria.Naklady.FirstOrDefault(n => n.Rok == rok);
            if (e is null)
            {
                e = new NakladRoka { Kategoria = Kategoria, KategoriaId = Kategoria.Id, Rok = rok };
                Kategoria.Naklady.Add(e);
                db.NakladyRoka.Add(e);
            }
            e.Suma = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>Platba z výpisu pred importom – používateľ môže zmeniť priradenie.</summary>
public partial class ImportRiadok : ObservableObject
{
    readonly Func<string, Priestor?> hladajPodlaVs;
    readonly Priestor? povodnyPriestor;
    public PlatbaZVypisu Platba { get; }
    public string Dovod { get; }
    [ObservableProperty] private Priestor? priestor;
    [ObservableProperty] private UcelUhrady ucel;
    [ObservableProperty] private bool importovat;
    /// <summary>Variabilný symbol – z výpisu, alebo ručne doplnený, ak platba VS nemala.</summary>
    [ObservableProperty] private string? vs;
    public bool Duplicitna { get; }
    /// <summary>Byty na výber v stĺpci „Byt“.</summary>
    public IReadOnlyList<Priestor> Moznosti { get; set; } = Array.Empty<Priestor>();
    /// <summary>Platba prišla bez variabilného symbolu – zvýrazní sa a VS sa dá doplniť ručne.</summary>
    public bool BezVs { get; }

    public ImportRiadok(ParovanaPlatba p, bool duplicitna, Func<string, Priestor?>? hladajPodlaVs = null)
    {
        this.hladajPodlaVs = hladajPodlaVs ?? (_ => null);
        Platba = p.Platba;
        Dovod = duplicitna ? "už je v evidencii" : p.Dovod;
        priestor = povodnyPriestor = p.Priestor;
        vs = p.Platba.VariabilnySymbol;
        BezVs = string.IsNullOrWhiteSpace(p.Platba.VariabilnySymbol);
        Duplicitna = duplicitna;
        importovat = p.Priestor is not null && !duplicitna;
    }

    partial void OnVsChanged(string? value)
    {
        if (!BezVs) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            Priestor = povodnyPriestor;
            return;
        }
        // po ručnom zadaní VS sa platba priradí k bytu s týmto VS a označí na import
        Priestor = hladajPodlaVs(value.Trim()) ?? povodnyPriestor;
        Importovat = true;
    }
}
