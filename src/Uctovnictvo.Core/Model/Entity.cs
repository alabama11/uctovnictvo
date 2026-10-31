namespace Uctovnictvo.Core.Model;

public enum TypJednotky
{
    Szco = 0,
    Sro = 1,
    Spolocenstvo = 2,
}

public enum TypPriestoru
{
    Byt = 0,
    NebytovyPriestor = 1,
}

/// <summary>Kľúč, podľa ktorého sa náklad rozpočítava medzi priestory.</summary>
public enum KlucRozpoctu
{
    Plocha = 0,
    SpotrebaVody = 1,
    RovnakoNaPriestor = 2,
    PocetOsob = 3,
    SpoluvlastnickyPodiel = 4,
}

/// <summary>Na čo bola platba určená.</summary>
public enum UcelUhrady
{
    Zalohy = 0,
    Poistne = 1,
    MinulyRok = 2,
}

/// <summary>Firma (SZČO, s.r.o.) alebo spoločenstvo vlastníkov.</summary>
public class UctovnaJednotka
{
    public int Id { get; set; }
    public TypJednotky Typ { get; set; }
    public string Nazov { get; set; } = "";
    public string? Ico { get; set; }
    public string? Dic { get; set; }
    public string? Ulica { get; set; }
    public string? Obec { get; set; }
    public string? Psc { get; set; }
    /// <summary>Účet, na ktorý sa platia zálohy / faktúry.</summary>
    public string? Iban { get; set; }
    /// <summary>Samostatný účet fondu prevádzky, údržby a opráv (len spoločenstvá).</summary>
    public string? IbanFondOprav { get; set; }
    /// <summary>Predseda spoločenstva / konateľ – podpisuje vyúčtovanie.</summary>
    public string? Statutar { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string? Poznamka { get; set; }
    /// <summary>Zápis v registri na obchodných listinách, napr. „Zapísaná v OR Mestského súdu Bratislava III, oddiel Sro, vložka č. 12345/B“.</summary>
    public string? ZapisVRegistri { get; set; }
    /// <summary>Formát čísla faktúry: RRRR = rok, RR = rok dvojmiestne, # = poradové číslo (počet # = počet číslic).</summary>
    public string FormatCislaFaktury { get; set; } = "RRRR####";
    public int SplatnostDni { get; set; } = 14;
    /// <summary>Počiatočný stav pokladne (k prvému dokladu).</summary>
    public decimal PokladnaPociatocnyStav { get; set; }

    public List<Priestor> Priestory { get; set; } = new();
    public List<KategoriaNakladov> Kategorie { get; set; } = new();
}

/// <summary>Byt alebo nebytový priestor v dome spoločenstva.</summary>
public class Priestor
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }

    public string Cislo { get; set; } = "";
    public TypPriestoru Typ { get; set; }
    public string? Vchod { get; set; }
    public string? Poschodie { get; set; }
    /// <summary>Podlahová plocha v m² – podľa nej sa rozpočítavajú náklady „podľa plochy“.</summary>
    public decimal Plocha { get; set; }
    /// <summary>Plocha balkóna v m² – len informatívne.</summary>
    public decimal PlochaBalkona { get; set; }
    public int PocetOsob { get; set; }
    /// <summary>Spoluvlastnícky podiel na spoločných častiach – čitateľ (napr. 4453).</summary>
    public int PodielCitatel { get; set; }
    /// <summary>Spoluvlastnícky podiel – menovateľ (napr. 298241).</summary>
    public int PodielMenovatel { get; set; }
    /// <summary>Variabilný symbol pre platby za tento priestor (zvyčajne číslo bytu).</summary>
    public string? VariabilnySymbol { get; set; }
    public string? CisloVodomeru { get; set; }
    public string? Poznamka { get; set; }

    public List<Vlastnik> Vlastnici { get; set; } = new();
    public List<PriestorRok> Roky { get; set; } = new();
    public List<PredpisPolozky> Predpisy { get; set; } = new();
    public List<Uhrada> Uhrady { get; set; } = new();

    /// <summary>Súčasní vlastníci (bez tých, ktorí byt predali).</summary>
    public string MenaVlastnikov => string.Join(", ", SucasniVlastnici.Select(v => v.Meno));

    public IEnumerable<Vlastnik> SucasniVlastnici =>
        Vlastnici.Where(v => v.VlastnikDo is null || v.VlastnikDo >= DateOnly.FromDateTime(DateTime.Today))
            .OrderByDescending(v => v.Hlavny).ThenBy(v => v.Id);
}

public class Vlastnik
{
    public int Id { get; set; }
    public int PriestorId { get; set; }
    public Priestor? Priestor { get; set; }

    public string Meno { get; set; } = "";
    public string? Ulica { get; set; }
    public string? Obec { get; set; }
    public string? Psc { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    /// <summary>Kontaktná osoba, ktorej sa posiela vyúčtovanie.</summary>
    public bool Hlavny { get; set; } = true;
    /// <summary>Od kedy je vlastníkom (prázdne = odjakživa). Pri predaji bytu sa vyúčtovanie delí podľa týchto dátumov.</summary>
    public DateOnly? VlastnikOd { get; set; }
    /// <summary>Do kedy bol vlastníkom (prázdne = stále je).</summary>
    public DateOnly? VlastnikDo { get; set; }

    public bool JeVlastnikom(DateOnly den) => (VlastnikOd is null || VlastnikOd <= den) && (VlastnikDo is null || VlastnikDo >= den);
    public string? Poznamka { get; set; }

    public List<BankovyUcet> Ucty { get; set; } = new();
}

/// <summary>Účet, z ktorého vlastník platí do spoločenstva (slúži na párovanie platieb).</summary>
public class BankovyUcet
{
    public int Id { get; set; }
    public int VlastnikId { get; set; }
    public Vlastnik? Vlastnik { get; set; }
    public string Iban { get; set; } = "";
    public string? Poznamka { get; set; }
}

/// <summary>
/// Druh nákladu spoločenstva (správa, upratovanie, voda, fond opráv…) a spôsob, ako sa rozpočíta.
/// Platí pre všetky roky; skutočná suma za rok je v <see cref="NakladRoka"/>.
/// </summary>
public class KategoriaNakladov
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public string Nazov { get; set; } = "";
    public KlucRozpoctu Kluc { get; set; }
    /// <summary>Fond opráv sa nevyúčtováva – skutočný náklad bytu = jeho predpis.</summary>
    public bool JeFondOprav { get; set; }
    /// <summary>Ak je vyplnené, náklad sa delí len medzi tieto priestory (čísla oddelené čiarkou, napr. „42, 50, 52“).</summary>
    public string? LenPriestory { get; set; }
    public int Poradie { get; set; }

    public List<NakladRoka> Naklady { get; set; } = new();
}

/// <summary>Skutočne zaplatený náklad celého domu v danej kategórii za rok.</summary>
public class NakladRoka
{
    public int Id { get; set; }
    public int KategoriaId { get; set; }
    public KategoriaNakladov? Kategoria { get; set; }
    public int Rok { get; set; }
    public decimal Suma { get; set; }
}

/// <summary>Ročný predpis zálohy priestoru pre jednu kategóriu nákladov (napr. správa 243,00 € / rok).</summary>
public class PredpisPolozky
{
    public int Id { get; set; }
    public int PriestorId { get; set; }
    public Priestor? Priestor { get; set; }
    public int KategoriaId { get; set; }
    public KategoriaNakladov? Kategoria { get; set; }
    public int Rok { get; set; }
    public decimal RocnaSuma { get; set; }
}

/// <summary>Údaje priestoru viazané na rok: odpočty vodomeru, poistné, zostatok z minulého roka.</summary>
public class PriestorRok
{
    public int Id { get; set; }
    public int PriestorId { get; set; }
    public Priestor? Priestor { get; set; }
    public int Rok { get; set; }

    public DateOnly? DatumOdpoctuZaciatok { get; set; }
    public decimal? StavVodomeruZaciatok { get; set; }
    public DateOnly? DatumOdpoctuKoniec { get; set; }
    public decimal? StavVodomeruKoniec { get; set; }
    /// <summary>Spotreba nameraná na starom vodomere, ak sa počas roka menil (m³).</summary>
    public decimal SpotrebaVymenenyVodomer { get; set; }

    /// <summary>Predpis ročného poistenia bytu.</summary>
    public decimal PoistnePredpis { get; set; }

    /// <summary>Výsledok vyúčtovania minulého roka: + preplatok, − nedoplatok.</summary>
    public decimal ZostatokMinulyRok { get; set; }

    public decimal SpotrebaVody =>
        (StavVodomeruZaciatok is { } z && StavVodomeruKoniec is { } k ? k - z : 0m) + SpotrebaVymenenyVodomer;
}

public class Uhrada
{
    public int Id { get; set; }
    public int PriestorId { get; set; }
    public Priestor? Priestor { get; set; }
    public DateOnly Datum { get; set; }
    /// <summary>Kladná = platba od vlastníka; záporná = vrátenie preplatku.</summary>
    public decimal Suma { get; set; }
    public UcelUhrady Ucel { get; set; }
    public string? IbanPlatitela { get; set; }
    public string? VariabilnySymbol { get; set; }
    public string? Poznamka { get; set; }
}

public enum TypPohybuFondu
{
    PociatocnyStav = 0,
    Cerpanie = 1,
    InyPrijem = 2,
}

/// <summary>Pohyb vo fonde prevádzky, údržby a opráv mimo predpisov vlastníkov (čerpanie na opravy, úroky, počiatočný stav).</summary>
public class PohybFondu
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public DateOnly Datum { get; set; }
    public TypPohybuFondu Typ { get; set; }
    public string Popis { get; set; } = "";
    /// <summary>Kladná suma; pri čerpaní sa odpočíta.</summary>
    public decimal Suma { get; set; }
}

/// <summary>Nastavenia programu (e-mail, zálohy) ako dvojice kľúč – hodnota.</summary>
public class Nastavenie
{
    public int Id { get; set; }
    public string Kluc { get; set; } = "";
    public string? Hodnota { get; set; }
}

/// <summary>Odberateľ alebo dodávateľ z adresára.</summary>
public class Partner
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public string Nazov { get; set; } = "";
    public string? Ico { get; set; }
    public string? Dic { get; set; }
    public string? IcDph { get; set; }
    public string? Ulica { get; set; }
    public string? Obec { get; set; }
    public string? Psc { get; set; }
    public string? Stat { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string? Iban { get; set; }
    public string? Poznamka { get; set; }

    public string Adresa => string.Join(", ", new[] { Ulica, string.Join(" ", new[] { Psc, Obec }.Where(x => !string.IsNullOrWhiteSpace(x))), Stat }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
}

public enum DruhFaktury
{
    Vydana = 0,
    Prijata = 1,
}

public enum TypFaktury
{
    Faktura = 0,
    Zalohova = 1,
    Dobropis = 2,
}

public enum FormaUhrady
{
    Prevod = 0,
    Hotovost = 1,
    Karta = 2,
    Dobierka = 3,
}

public class Faktura
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public DruhFaktury Druh { get; set; }
    public TypFaktury Typ { get; set; }
    /// <summary>Číslo dokladu: pri vydaných z číselného radu, pri prijatých interné poradové číslo.</summary>
    public string Cislo { get; set; } = "";
    /// <summary>Číslo faktúry dodávateľa (len prijaté).</summary>
    public string? CisloDodavatela { get; set; }
    public string? VariabilnySymbol { get; set; }
    public string? KonstantnySymbol { get; set; }

    public int? PartnerId { get; set; }
    public Partner? Partner { get; set; }
    // Údaje partnera v čase vystavenia (faktúra sa nesmie zmeniť, keď sa zmení adresár).
    public string PartnerNazov { get; set; } = "";
    public string? PartnerAdresa { get; set; }
    public string? PartnerIco { get; set; }
    public string? PartnerDic { get; set; }
    public string? PartnerIcDph { get; set; }
    public string? PartnerIban { get; set; }

    public DateOnly DatumVystavenia { get; set; }
    public DateOnly? DatumDodania { get; set; }
    public DateOnly DatumSplatnosti { get; set; }
    public FormaUhrady FormaUhrady { get; set; }
    /// <summary>Pri dobropise číslo opravovanej faktúry.</summary>
    public string? PovodnaFaktura { get; set; }
    public string? TextNad { get; set; }
    public string? TextPod { get; set; }
    /// <summary>Kategória nákladov spoločenstva, do ktorej sa prijatá faktúra započíta.</summary>
    public int? KategoriaNakladovId { get; set; }
    public KategoriaNakladov? KategoriaNakladov { get; set; }
    public DateOnly? DatumUpomienky { get; set; }
    /// <summary>Podvojné účtovníctvo: výnosový účet vydanej / nákladový účet prijatej faktúry (prázdne = podľa kategórie alebo 602 / 518).</summary>
    public string? Ucet { get; set; }
    /// <summary>Kategória daňovej evidencie, ktorú dostane úhrada faktúry.</summary>
    public int? KategoriaPohybuId { get; set; }
    public KategoriaPohybu? KategoriaPohybu { get; set; }

    public List<PolozkaFaktury> Polozky { get; set; } = new();
    public List<UhradaFaktury> Uhrady { get; set; } = new();

    public decimal Suma => Polozky.Sum(p => p.Suma);
    public decimal Uhradene => Uhrady.Sum(u => u.Suma);
    public decimal Zostava => Suma - Uhradene;
    public bool JeUhradena => Suma != 0 && Zostava == 0 || Suma == 0;
    public bool JePoSplatnosti(DateOnly dnes) => !JeUhradena && dnes > DatumSplatnosti && Zostava > 0;
}

public class PolozkaFaktury
{
    public int Id { get; set; }
    public int FakturaId { get; set; }
    public Faktura? Faktura { get; set; }
    public int Poradie { get; set; }
    public string Popis { get; set; } = "";
    public decimal Mnozstvo { get; set; } = 1;
    public string? Jednotka { get; set; }
    public decimal CenaZaJednotku { get; set; }
    public decimal Suma => Math.Round(Mnozstvo * CenaZaJednotku, 2, MidpointRounding.AwayFromZero);
}

public class UhradaFaktury
{
    public int Id { get; set; }
    public int FakturaId { get; set; }
    public Faktura? Faktura { get; set; }
    public DateOnly Datum { get; set; }
    public decimal Suma { get; set; }
    public int? BankovyPohybId { get; set; }
    public BankovyPohyb? BankovyPohyb { get; set; }
    public int? PokladnicnyDokladId { get; set; }
    public PokladnicnyDoklad? PokladnicnyDoklad { get; set; }
    public string? Poznamka { get; set; }
}

/// <summary>Pohyb na bankovom účte firmy (príjem kladný, výdaj záporný).</summary>
public class BankovyPohyb
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public DateOnly Datum { get; set; }
    public decimal Suma { get; set; }
    public string? ProtiIban { get; set; }
    public string? ProtiNazov { get; set; }
    public string? VariabilnySymbol { get; set; }
    public string? Popis { get; set; }
    /// <summary>Jednoznačný identifikátor z výpisu – zabráni dvojitému importu.</summary>
    public string? Referencia { get; set; }
    public int? KategoriaId { get; set; }
    public KategoriaPohybu? Kategoria { get; set; }

    public List<UhradaFaktury> Uhrady { get; set; } = new();
}

public enum DruhPokladnicnehoDokladu
{
    Prijem = 0,
    Vydavok = 1,
}

/// <summary>Príjmový (PPD) alebo výdavkový (VPD) pokladničný doklad.</summary>
public class PokladnicnyDoklad
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public DruhPokladnicnehoDokladu Druh { get; set; }
    public string Cislo { get; set; } = "";
    public DateOnly Datum { get; set; }
    /// <summary>Kladná suma.</summary>
    public decimal Suma { get; set; }
    public string Ucel { get; set; } = "";
    /// <summary>Od koho prijaté / komu vyplatené.</summary>
    public string? Osoba { get; set; }
    public int? KategoriaId { get; set; }
    public KategoriaPohybu? Kategoria { get; set; }

    public List<UhradaFaktury> Uhrady { get; set; } = new();
}

public enum DruhKategorie
{
    Prijem = 0,
    Vydavok = 1,
    /// <summary>Nie je príjmom ani výdavkom: prevod medzi bankou a pokladňou, vklad alebo výber podnikateľa, úver.</summary>
    Neutralny = 2,
}

/// <summary>Kategória peňažného denníka / daňovej evidencie (napr. „Služby“, „Poistné SP a ZP“).</summary>
public class KategoriaPohybu
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public string Nazov { get; set; } = "";
    public DruhKategorie Druh { get; set; }
    /// <summary>Daňový príjem / daňovo uznaný výdavok.</summary>
    public bool Danovy { get; set; } = true;
    /// <summary>Strojový kód pre automatické priradenie a neskôr pre daňové priznanie (napr. „trzby“, „poistne“).</summary>
    public string? Kod { get; set; }
    /// <summary>Podvojné účtovníctvo: nákladový / výnosový (alebo iný) účet, na ktorý sa kategória účtuje, napr. „518“.</summary>
    public string? Ucet { get; set; }
    public int Poradie { get; set; }

    public override string ToString() => Nazov;
}

public enum MetodaOdpisovania
{
    Rovnomerna = 0,
    Zrychlena = 1,
}

/// <summary>Dlhodobý hmotný alebo nehmotný majetok s daňovými odpismi.</summary>
public class Majetok
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public string InventarneCislo { get; set; } = "";
    public string Nazov { get; set; } = "";
    public DateOnly? DatumObstarania { get; set; }
    public DateOnly DatumZaradenia { get; set; }
    public decimal VstupnaCena { get; set; }
    public int OdpisovaSkupina { get; set; } = 1;
    public MetodaOdpisovania Metoda { get; set; }
    /// <summary>Podiel použitia na podnikanie v % (napr. auto používané aj súkromne).</summary>
    public decimal PodielPodnikanie { get; set; } = 100;
    public DateOnly? DatumVyradenia { get; set; }
    public string? SposobVyradenia { get; set; }
    public string? Poznamka { get; set; }
}

/// <summary>Vozidlo pre knihu jázd.</summary>
public class Vozidlo
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public string Nazov { get; set; } = "";
    public string? Ecv { get; set; }
    /// <summary>Kombinovaná spotreba podľa technického preukazu (l/100 km).</summary>
    public decimal Spotreba { get; set; }
    public string? Palivo { get; set; }
    /// <summary>Vozidlo je v obchodnom majetku – výdavky sa uplatňujú skutočné, náhrady sa nepočítajú.</summary>
    public bool VObchodnomMajetku { get; set; }
    /// <summary>Cena paliva za liter použitá pre náhradu (ak nie je zadaná pri jazde).</summary>
    public decimal CenaPaliva { get; set; }
    public decimal StavTachometraZaciatok { get; set; }

    public List<Jazda> Jazdy { get; set; } = new();
}

public class Jazda
{
    public int Id { get; set; }
    public int VozidloId { get; set; }
    public Vozidlo? Vozidlo { get; set; }
    public DateOnly Datum { get; set; }
    public string Odkial { get; set; } = "";
    public string Kam { get; set; } = "";
    public string? Ucel { get; set; }
    public decimal Km { get; set; }
    public bool Sukromna { get; set; }
    public string? Vodic { get; set; }
    /// <summary>Cena paliva pre túto jazdu (napr. podľa bločku); ak chýba, použije sa cena z vozidla.</summary>
    public decimal? CenaPaliva { get; set; }
}

public enum TypUctu
{
    Aktivny = 0,
    Pasivny = 1,
    Nakladovy = 2,
    Vynosovy = 3,
    Uzavierkovy = 4,
    Podsuvahovy = 5,
}

/// <summary>Účet účtového rozvrhu (syntetický „221“ alebo analytický „221.001“).</summary>
public class Ucet
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public string Cislo { get; set; } = "";
    public string Nazov { get; set; } = "";
    public TypUctu Typ { get; set; }
    /// <summary>Náklad / výnos je daňový (pre daňové priznanie PO).</summary>
    public bool Danovy { get; set; } = true;

    public string Synteticky => Cislo.Length >= 3 ? Cislo[..3] : Cislo;
    public override string ToString() => $"{Cislo} {Nazov}";
}

public enum ZdrojZapisu
{
    Rucne = 0,
    PociatocnyStav = 1,
    Faktura = 2,
    Banka = 3,
    Pokladna = 4,
    Odpisy = 5,
    Uzavierka = 6,
}

/// <summary>Účtovný zápis: suma na ťarchu účtu MD a v prospech účtu D.</summary>
public class UctovnyZapis
{
    public int Id { get; set; }
    public int JednotkaId { get; set; }
    public UctovnaJednotka? Jednotka { get; set; }
    public DateOnly Datum { get; set; }
    public string Doklad { get; set; } = "";
    public string Popis { get; set; } = "";
    public string Md { get; set; } = "";
    public string D { get; set; } = "";
    public decimal Suma { get; set; }
    public ZdrojZapisu Zdroj { get; set; }
    /// <summary>Faktúra, ku ktorej zápis patrí (saldokonto).</summary>
    public int? FakturaId { get; set; }
    public string? Partner { get; set; }
}
