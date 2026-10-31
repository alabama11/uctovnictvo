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
