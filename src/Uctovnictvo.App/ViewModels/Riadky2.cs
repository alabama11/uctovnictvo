using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Riadok zoznamu faktúr a zároveň editor vybranej faktúry.</summary>
public partial class FakturaVm : ObservableObject
{
    public Faktura F { get; }
    public ObservableCollection<PolozkaVm> Polozky { get; } = new();

    public FakturaVm(Faktura f)
    {
        F = f;
        foreach (var p in f.Polozky.OrderBy(p => p.Poradie)) Polozky.Add(new PolozkaVm(p, this));
    }

    public string Cislo { get => F.Cislo; set { F.Cislo = value; OnPropertyChanged(); } }
    public string? CisloDodavatela { get => F.CisloDodavatela; set { F.CisloDodavatela = value; OnPropertyChanged(); } }
    public string? VariabilnySymbol { get => F.VariabilnySymbol; set { F.VariabilnySymbol = value; OnPropertyChanged(); } }
    public string? KonstantnySymbol { get => F.KonstantnySymbol; set { F.KonstantnySymbol = value; OnPropertyChanged(); } }
    public string PartnerNazov { get => F.PartnerNazov; set { F.PartnerNazov = value; OnPropertyChanged(); } }
    public string? PartnerAdresa { get => F.PartnerAdresa; set { F.PartnerAdresa = value; OnPropertyChanged(); } }
    public string? PartnerIco { get => F.PartnerIco; set { F.PartnerIco = value; OnPropertyChanged(); } }
    public string? PartnerDic { get => F.PartnerDic; set { F.PartnerDic = value; OnPropertyChanged(); } }
    public string? PartnerIban { get => F.PartnerIban; set { F.PartnerIban = value; OnPropertyChanged(); } }
    public DateOnly? DatumVystavenia { get => F.DatumVystavenia; set { if (value is { } d) F.DatumVystavenia = d; OnPropertyChanged(); } }
    public DateOnly? DatumDodania { get => F.DatumDodania; set { F.DatumDodania = value; OnPropertyChanged(); } }
    public DateOnly? DatumSplatnosti { get => F.DatumSplatnosti; set { if (value is { } d) F.DatumSplatnosti = d; OnPropertyChanged(); Obnov(); } }
    public FormaUhrady FormaUhrady { get => F.FormaUhrady; set { F.FormaUhrady = value; OnPropertyChanged(); } }
    public string? TextNad { get => F.TextNad; set { F.TextNad = value; OnPropertyChanged(); } }
    public string? TextPod { get => F.TextPod; set { F.TextPod = value; OnPropertyChanged(); } }
    public string? PovodnaFaktura { get => F.PovodnaFaktura; set { F.PovodnaFaktura = value; OnPropertyChanged(); } }

    public Partner? Partner
    {
        get => F.Partner;
        set
        {
            // null prichádza aj od ComboBoxu pri prepínaní faktúr – odkaz na partnera sa tým nesmie stratiť
            if (value is null || value == F.Partner) return;
            Faktury.NastavPartnera(F, value);
            OnPropertyChanged(string.Empty);
        }
    }

    public KategoriaNakladov? Kategoria
    {
        get => F.KategoriaNakladov;
        set { if (value is null) return; F.KategoriaNakladov = value; F.KategoriaNakladovId = value.Id; OnPropertyChanged(); }
    }

    /// <summary>Výnosový / nákladový účet pre podvojné účtovníctvo (prázdne = podľa kategórie).</summary>
    public string? Ucet { get => F.Ucet; set { F.Ucet = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); OnPropertyChanged(); } }
    public KategoriaPohybu? KategoriaPohybu
    {
        get => F.KategoriaPohybu;
        set { if (value is null) return; F.KategoriaPohybu = value; F.KategoriaPohybuId = value.Id; OnPropertyChanged(); }
    }

    public string Typ => Faktury.NazovTypu(F.Typ);
    public string Popis => $"{F.Cislo}   {F.PartnerNazov}   {PdfVyuctovanie.Eur(F.Zostava)}";
    public bool JeVydana => F.Druh == DruhFaktury.Vydana;
    public bool JePrijata => F.Druh == DruhFaktury.Prijata;
    public bool JeZalohova => F.Typ == TypFaktury.Zalohova;
    public decimal Suma => F.Suma;
    public decimal Uhradene => F.Uhradene;
    public decimal Zostava => F.Zostava;
    public string Stav => Faktury.Stav(F, DateOnly.FromDateTime(DateTime.Today));
    public bool PoSplatnosti => F.JePoSplatnosti(DateOnly.FromDateTime(DateTime.Today));
    public string Uhrady => F.Uhrady.Count == 0 ? "" :
        string.Join(", ", F.Uhrady.OrderBy(u => u.Datum).Select(u => $"{u.Datum:d.M.yyyy} {PdfVyuctovanie.Eur(u.Suma)}" +
            (u.PokladnicnyDoklad is { } d ? $" ({d.Cislo})" : u.BankovyPohyb is not null ? " (banka)" : "")));

    public void Obnov()
    {
        OnPropertyChanged(nameof(Suma));
        OnPropertyChanged(nameof(Uhradene));
        OnPropertyChanged(nameof(Zostava));
        OnPropertyChanged(nameof(Stav));
        OnPropertyChanged(nameof(PoSplatnosti));
        OnPropertyChanged(nameof(Uhrady));
    }

    public void ObnovVsetko() => OnPropertyChanged(string.Empty);
}

public partial class PolozkaVm : ObservableObject
{
    public PolozkaFaktury P { get; }
    readonly FakturaVm faktura;

    public PolozkaVm(PolozkaFaktury p, FakturaVm f) { P = p; faktura = f; }

    public string Popis { get => P.Popis; set { P.Popis = value; OnPropertyChanged(); } }
    public decimal Mnozstvo { get => P.Mnozstvo; set { P.Mnozstvo = value; OnPropertyChanged(); Zmena(); } }
    public string? Jednotka { get => P.Jednotka; set { P.Jednotka = value; OnPropertyChanged(); } }
    public decimal CenaZaJednotku { get => P.CenaZaJednotku; set { P.CenaZaJednotku = value; OnPropertyChanged(); Zmena(); } }
    public decimal Suma => P.Suma;

    void Zmena()
    {
        OnPropertyChanged(nameof(Suma));
        faktura.Obnov();
    }
}

/// <summary>Bankový pohyb v zozname s informáciou o priradení.</summary>
public partial class PohybVm : ObservableObject
{
    public BankovyPohyb P { get; }
    public PohybVm(BankovyPohyb p) { P = p; }

    public DateOnly? Datum { get => P.Datum; set { if (value is { } d) P.Datum = d; OnPropertyChanged(); } }
    public decimal Suma { get => P.Suma; set { P.Suma = value; OnPropertyChanged(); } }
    public string? ProtiIban { get => P.ProtiIban; set { P.ProtiIban = value; OnPropertyChanged(); } }
    public string? ProtiNazov { get => P.ProtiNazov; set { P.ProtiNazov = value; OnPropertyChanged(); } }
    public string? VariabilnySymbol { get => P.VariabilnySymbol; set { P.VariabilnySymbol = value; OnPropertyChanged(); } }
    public string? Popis { get => P.Popis; set { P.Popis = value; OnPropertyChanged(); } }
    public string Faktury => string.Join(", ", P.Uhrady.Select(u => u.Faktura?.Cislo));
    public void Obnov() => OnPropertyChanged(nameof(Faktury));
}

public partial class DokladVm : ObservableObject
{
    public PokladnicnyDoklad D { get; }
    decimal zostatok;
    public DokladVm(PokladnicnyDoklad d, decimal zostatok) { D = d; this.zostatok = zostatok; }

    public string Cislo { get => D.Cislo; set { D.Cislo = value; OnPropertyChanged(); } }
    public string Druh => D.Druh == DruhPokladnicnehoDokladu.Prijem ? "príjem" : "výdavok";
    public DateOnly? Datum { get => D.Datum; set { if (value is { } d) D.Datum = d; OnPropertyChanged(); } }
    public decimal Suma { get => D.Suma; set { D.Suma = Math.Abs(value); OnPropertyChanged(); } }
    public string Ucel { get => D.Ucel; set { D.Ucel = value; OnPropertyChanged(); } }
    public string? Osoba { get => D.Osoba; set { D.Osoba = value; OnPropertyChanged(); } }
    public decimal? Prijem => D.Druh == DruhPokladnicnehoDokladu.Prijem ? D.Suma : null;
    public decimal? Vydavok => D.Druh == DruhPokladnicnehoDokladu.Vydavok ? D.Suma : null;
    public decimal Zostatok { get => zostatok; set { zostatok = value; OnPropertyChanged(); OnPropertyChanged(nameof(Prijem)); OnPropertyChanged(nameof(Vydavok)); } }
    public bool Zaporny => zostatok < 0;
}

/// <summary>Riadok peňažného denníka s možnosťou zmeniť kategóriu.</summary>
public partial class DennikVm : ObservableObject
{
    public RiadokDennika R { get; }
    readonly Action zmena;
    /// <summary>Zoznam kategórií pre ComboBox v riadku (väzba cez $parent sa v bunke tabuľky vyhodnotí neskoro).</summary>
    public IReadOnlyList<KategoriaPohybu> Kategorie { get; }
    public DennikVm(RiadokDennika r, IReadOnlyList<KategoriaPohybu> kategorie, Action zmena) { R = r; Kategorie = kategorie; this.zmena = zmena; }

    public DateOnly Datum => R.Datum;
    public string Doklad => R.Doklad;
    public string Popis => R.Popis;
    public decimal? Prijem => R.Prijem == 0 ? null : R.Prijem;
    public decimal? Vydavok => R.Vydavok == 0 ? null : R.Vydavok;
    public bool Nezaradeny => Kategoria is null;

    public KategoriaPohybu? Kategoria
    {
        get => R.Polozka switch { BankovyPohyb p => p.Kategoria, PokladnicnyDoklad d => d.Kategoria, _ => null };
        set
        {
            if (value is null) return;
            switch (R.Polozka)
            {
                case BankovyPohyb p: p.Kategoria = value; p.KategoriaId = value.Id; break;
                case PokladnicnyDoklad d: d.Kategoria = value; d.KategoriaId = value.Id; break;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(Nezaradeny));
            zmena();
        }
    }
}

public partial class MajetokVm : ObservableObject
{
    public Majetok M { get; }
    public MajetokVm(Majetok m) { M = m; }

    public string InventarneCislo { get => M.InventarneCislo; set { M.InventarneCislo = value; OnPropertyChanged(); } }
    public string Nazov { get => M.Nazov; set { M.Nazov = value; OnPropertyChanged(); } }
    public DateOnly? DatumZaradenia { get => M.DatumZaradenia; set { if (value is { } d) M.DatumZaradenia = d; Zmena(); } }
    public decimal VstupnaCena { get => M.VstupnaCena; set { M.VstupnaCena = value; Zmena(); } }
    public int OdpisovaSkupina { get => M.OdpisovaSkupina; set { M.OdpisovaSkupina = Math.Clamp(value, 0, 6); if (Odpisy.Koeficienty(M.OdpisovaSkupina) is null) M.Metoda = MetodaOdpisovania.Rovnomerna; Zmena(); } }
    public bool Zrychlene
    {
        get => M.Metoda == MetodaOdpisovania.Zrychlena;
        set { M.Metoda = value && Odpisy.Koeficienty(M.OdpisovaSkupina) is not null ? MetodaOdpisovania.Zrychlena : MetodaOdpisovania.Rovnomerna; Zmena(); }
    }
    public decimal PodielPodnikanie { get => M.PodielPodnikanie; set { M.PodielPodnikanie = Math.Clamp(value, 0, 100); Zmena(); } }
    public DateOnly? DatumVyradenia { get => M.DatumVyradenia; set { M.DatumVyradenia = value; Zmena(); } }
    public string? Poznamka { get => M.Poznamka; set { M.Poznamka = value; OnPropertyChanged(); } }
    public string Doba => M.OdpisovaSkupina is >= 0 and <= 6 ? $"{Odpisy.DobaOdpisovania[M.OdpisovaSkupina]} r." : "";

    public int Rok { get; set; }
    public decimal OdpisRoka => Odpisy.ZaRok(M, Rok)?.UplatnenyOdpis ?? 0;
    public decimal ZostatkovaCena => Odpisy.ZostatkovaCena(M, Rok);
    public IReadOnlyList<OdpisRoka> Plan => Odpisy.Plan(M);

    void Zmena() => OnPropertyChanged(string.Empty);
}

public partial class JazdaVm : ObservableObject
{
    public Jazda J { get; }
    readonly Vozidlo v;
    readonly Func<IReadOnlyList<SadzbaKm>> sadzby;
    readonly Action zmena;
    public JazdaVm(Jazda j, Vozidlo v, Func<IReadOnlyList<SadzbaKm>> sadzby, Action zmena) { J = j; this.v = v; this.sadzby = sadzby; this.zmena = zmena; }

    public DateOnly? Datum { get => J.Datum; set { if (value is { } d) J.Datum = d; Zmena(); } }
    public string Odkial { get => J.Odkial; set { J.Odkial = value; OnPropertyChanged(); } }
    public string Kam { get => J.Kam; set { J.Kam = value; OnPropertyChanged(); } }
    public string? Ucel { get => J.Ucel; set { J.Ucel = value; OnPropertyChanged(); } }
    public decimal Km { get => J.Km; set { J.Km = value; Zmena(); } }
    public bool Sukromna { get => J.Sukromna; set { J.Sukromna = value; Zmena(); } }
    public string? Vodic { get => J.Vodic; set { J.Vodic = value; OnPropertyChanged(); } }
    public decimal? CenaPaliva { get => J.CenaPaliva; set { J.CenaPaliva = value; Zmena(); } }
    public decimal Nahrada { get { var n = KnihaJazd.Nahrada(J, v, sadzby()); return n.Zakladna + n.Phm; } }

    void Zmena() { OnPropertyChanged(string.Empty); zmena(); }
}
