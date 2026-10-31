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

/// <summary>Ročné údaje priestoru: odpočty vody, poistné, zostatok z minulého roka.</summary>
public partial class RokRiadok : ObservableObject
{
    public Priestor Priestor { get; }
    public PriestorRok Data { get; }

    public RokRiadok(Priestor priestor, PriestorRok data)
    {
        Priestor = priestor;
        Data = data;
    }

    public DateOnly? DatumZaciatok { get => Data.DatumOdpoctuZaciatok; set { Data.DatumOdpoctuZaciatok = value; OnPropertyChanged(); } }
    public DateOnly? DatumKoniec { get => Data.DatumOdpoctuKoniec; set { Data.DatumOdpoctuKoniec = value; OnPropertyChanged(); } }
    public decimal? StavZaciatok { get => Data.StavVodomeruZaciatok; set { Data.StavVodomeruZaciatok = value; OnPropertyChanged(); OnPropertyChanged(nameof(Spotreba)); } }
    public decimal? StavKoniec { get => Data.StavVodomeruKoniec; set { Data.StavVodomeruKoniec = value; OnPropertyChanged(); OnPropertyChanged(nameof(Spotreba)); } }
    /// <summary>Spotreba na vodomere, ktorý sa počas roka vymenil.</summary>
    public decimal SpotrebaVymeneny { get => Data.SpotrebaVymenenyVodomer; set { Data.SpotrebaVymenenyVodomer = value; OnPropertyChanged(); OnPropertyChanged(nameof(Spotreba)); } }
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
    public PlatbaZVypisu Platba { get; }
    public string Dovod { get; }
    [ObservableProperty] private Priestor? priestor;
    [ObservableProperty] private UcelUhrady ucel;
    [ObservableProperty] private bool importovat;
    public bool Duplicitna { get; }

    public ImportRiadok(ParovanaPlatba p, bool duplicitna)
    {
        Platba = p.Platba;
        Dovod = duplicitna ? "už je v evidencii" : p.Dovod;
        priestor = p.Priestor;
        Duplicitna = duplicitna;
        importovat = p.Priestor is not null && !duplicitna;
    }
}
