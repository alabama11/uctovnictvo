using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuestPDF.Fluent;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

/// <summary>Konto vlastníkov, upomienky, e-maily, predpisy, fond opráv, import z Excelu a nastavenia.</summary>
public partial class MainViewModel
{
    public ObservableCollection<KontoPriestoru> Konta { get; } = new();
    public ObservableCollection<MesiacKonta> KontoMesiace { get; } = new();
    public ObservableCollection<PohybFondu> PohybyFondu { get; } = new();
    public static int[] MesiaceRoka { get; } = Enumerable.Range(1, 12).ToArray();

    [ObservableProperty] private int kontoKuMesiacu = 12;
    [ObservableProperty] private bool lenDlznici;
    [ObservableProperty] private KontoPriestoru? vybraneKonto;
    [ObservableProperty] private string sumaKont = "";
    [ObservableProperty] private PohybFondu? vybranyPohyb;
    [ObservableProperty] private string stavFonduText = "";
    [ObservableProperty] private decimal navyseniePercent;
    [ObservableProperty] private bool odosiela;

    public AppDbContext Db => db;

    partial void OnKontoKuMesiacuChanged(int value) => NacitajKonta();
    partial void OnLenDlzniciChanged(bool value) => NacitajKonta();

    partial void OnVybraneKontoChanged(KontoPriestoru? value)
    {
        KontoMesiace.Clear();
        if (value is null) return;
        foreach (var m in value.Mesiace.Where(m => m.Mesiac <= value.KuMesiacu)) KontoMesiace.Add(m);
    }

    void NacitajKontoAFond()
    {
        kontoKuMesiacu = KontoVlastnika.PredvolenyMesiac(Rok);
        OnPropertyChanged(nameof(KontoKuMesiacu));
        NacitajKonta();
        NacitajFond();
    }

    public void NacitajKonta()
    {
        Konta.Clear();
        if (!JeSpolocenstvo) return;
        var vsetky = Priestory.Select(p => KontoVlastnika.Vypocitaj(p, Rok, KontoKuMesiacu)).ToList();
        foreach (var k in vsetky.Where(k => !LenDlznici || k.Dlh > 0)) Konta.Add(k);
        var dlznici = vsetky.Where(k => k.Dlh > 0).ToList();
        SumaKont = $"Dlžníkov: {dlznici.Count}   Dlh spolu: {PdfVyuctovanie.Eur(dlznici.Sum(k => k.Dlh))}   " +
                   $"Preplatky spolu: {PdfVyuctovanie.Eur(vsetky.Where(k => k.Zostatok > 0).Sum(k => k.Zostatok))}";
        VybraneKonto = Konta.FirstOrDefault();
    }

    [RelayCommand]
    void ObnovKonta()
    {
        Uloz();
        NacitajKonta();
    }

    static string Emaily(IEnumerable<Vlastnik> v) =>
        string.Join("; ", v.Select(x => x.Email).Where(Email.JePlatnaAdresa));

    // ---------- upomienky ----------

    [RelayCommand]
    async Task UpomienkyPdf()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var dlznici = Konta.Where(k => k.Dlh > 0).ToList();
        if (dlznici.Count == 0) { await Dialogy.Sprava("Upomienky", "Žiadny byt nemá k vybranému mesiacu dlh."); return; }
        var s = await Dialogy.UlozSubor("Upomienky (PDF)", $"upomienky-{Rok}-{KontoKuMesiacu:00}.pdf", "pdf", "PDF");
        if (s is null) return;
        var j = VybranaJednotka;
        await Task.Run(() => PdfDokumenty.Upomienky(j, dlznici, MiestoVystavenia, Dnes, 14, s));
        Stav = $"Upomienky ({dlznici.Count}) uložené: {s}";
    }

    [RelayCommand]
    async Task UpomienkyEmail()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var dlznici = Konta.Where(k => k.Dlh > 0).ToList();
        var j = VybranaJednotka;
        await OdosliHromadne(
            "Upomienky e-mailom",
            dlznici.Select(k => (k.Priestor.SucasniVlastnici.ToList(), k.Priestor, k.Dlh)).ToList(),
            $"Upomienka – nedoplatok, byt {{byt}}",
            $"Dobrý deň,\n\npodľa našej evidencie k {Dnes:d.M.yyyy} evidujeme za byt č. {{byt}} nedoplatok {{suma}}.\n" +
            $"Prosíme o úhradu do 14 dní na účet {Iban.Formatuj(j.Iban)}, variabilný symbol {{vs}}. Podrobnosti a QR kód na platbu sú v prílohe.\n\n" +
            $"Ak ste medzičasom zaplatili, považujte túto správu za bezpredmetnú.\n\nS pozdravom\n{j.Statutar}\n{j.Nazov}",
            x => ($"upomienka-byt-{x.Priestor.Cislo}.pdf",
                  PdfDokumenty.Upomienka(j, dlznici.First(k => k.Priestor == x.Priestor), MiestoVystavenia, Dnes, 14)));
    }

    DateOnly Dnes => DateOnly.FromDateTime(DateTime.Today);

    // ---------- vyúčtovanie e-mailom ----------

    [RelayCommand]
    async Task VyuctovanieEmail()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        if (VyuctovanieBytov.Count == 0) Vypocitaj();
        var j = VybranaJednotka;
        var nast = Nastavenia();
        var byty = VyuctovanieBytov.ToList();
        await OdosliHromadne(
            "Vyúčtovanie e-mailom",
            byty.Select(b => (b.Adresati.ToList(), b.Priestor, b.Vysledok)).ToList(),
            $"Vyúčtovanie za rok {Rok} – byt {{byt}}",
            $"Dobrý deň,\n\nv prílohe vám posielame ročné vyúčtovanie za rok {Rok} za byt č. {{byt}}.\n" +
            "Výsledok: {vysledok}.\n\n" +
            $"Nedoplatok prosíme uhradiť do 14 dní na účet {Iban.Formatuj(j.Iban)}, VS {{vs}} (QR kód je vo vyúčtovaní).\n\nS pozdravom\n{j.Statutar}\n{j.Nazov}",
            x =>
            {
                var b = byty.First(y => y.Priestor == x.Priestor && y.Adresati.SequenceEqual(x.Vlastnici));
                using var ms = new MemoryStream();
                PdfVyuctovanie.Vytvor(j, new[] { b }, nast).GeneratePdf(ms);
                return ($"vyuctovanie-{Rok}-byt-{x.Priestor.Cislo}.pdf", ms.ToArray());
            });
    }

    // ---------- predpisy (evidenčné listy) ----------

    [RelayCommand]
    async Task PredpisyPdf()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        Uloz();
        var s = await Dialogy.UlozSubor("Predpis platieb – evidenčné listy (PDF)", $"predpis-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        var j = VybranaJednotka;
        var listy = Priestory.Select(p => PdfDokumenty.Predpis(p, Kategorie, Rok, new DateOnly(Rok, 1, 1))).ToList();
        await Task.Run(() => PdfDokumenty.EvidencneListy(j, listy, MiestoVystavenia, Dnes, s));
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task PredpisyEmail()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        Uloz();
        var j = VybranaJednotka;
        await OdosliHromadne(
            "Predpis platieb e-mailom",
            Priestory.Select(p => (p.SucasniVlastnici.ToList(), p, PdfDokumenty.Predpis(p, Kategorie, Rok, new DateOnly(Rok, 1, 1)).Mesacne)).ToList(),
            $"Predpis mesačných platieb na rok {Rok} – byt {{byt}}",
            $"Dobrý deň,\n\nv prílohe vám posielame predpis mesačných platieb na rok {Rok} za byt č. {{byt}}. Mesačná platba je {{suma}}, " +
            $"účet {Iban.Formatuj(j.Iban)}, VS {{vs}}.\n\nS pozdravom\n{j.Statutar}\n{j.Nazov}",
            x => ($"predpis-{Rok}-byt-{x.Priestor.Cislo}.pdf",
                  PdfDokumenty.EvidencnyList(j, PdfDokumenty.Predpis(x.Priestor, Kategorie, Rok, new DateOnly(Rok, 1, 1)), MiestoVystavenia, Dnes)));
    }

    [RelayCommand]
    async Task NavrhniZalohy()
    {
        if (Dialogy is null) return;
        var vstup = await Dialogy.VstupnyText("Navrhnúť zálohy na rok " + (Rok + 1),
            $"Predpisy na rok {Rok + 1} sa vypočítajú zo skutočných nákladov roka {Rok} (fond opráv ostáva). " +
            "O koľko percent ich chcete navýšiť ako rezervu? (napr. 0, 3 alebo 5)", "0");
        if (vstup is null) return;
        if (!decimal.TryParse(vstup.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var perc))
        {
            await Dialogy.Sprava("Navrhnúť zálohy", "Zadajte číslo.");
            return;
        }
        var buduci = Rok + 1;
        if (Priestory.Any(p => p.Predpisy.Any(x => x.Rok == buduci)) &&
            !await Dialogy.Potvrd("Navrhnúť zálohy", $"Na rok {buduci} už predpisy existujú. Prepísať ich návrhom?"))
            return;

        Vypocitaj();
        var navrh = NavrhZaloh.Vypocitaj(new VysledokVyuctovania(Rok, VyuctovanieBytov.ToList(), Upozornenia.ToList()), perc);
        foreach (var p in Priestory)
            foreach (var k in Kategorie)
            {
                if (!navrh.TryGetValue((p.Id, k.Id), out var suma)) continue;
                var e = p.Predpisy.FirstOrDefault(x => x.Rok == buduci && x.KategoriaId == k.Id);
                if (e is null)
                {
                    if (suma == 0) continue;
                    e = new PredpisPolozky { Priestor = p, KategoriaId = k.Id, Rok = buduci };
                    p.Predpisy.Add(e);
                    db.Predpisy.Add(e);
                }
                e.RocnaSuma = suma;
            }
        Uloz();
        Rok = buduci;
        Stav = $"Predpisy na rok {buduci} navrhnuté podľa skutočnosti {buduci - 1} (+{perc} %). Skontrolujte ich v záložke Predpisy a odpočty.";
    }

    // ---------- hromadné e-maily ----------

    async Task OdosliHromadne(string titulok, List<(List<Vlastnik> Vlastnici, Priestor Priestor, decimal Suma)> zoznam,
        string predmet, string text, Func<(List<Vlastnik> Vlastnici, Priestor Priestor), (string, byte[])> priloha)
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var nast = Core.Services.Nastavenia.Email(db);
        if (!nast.JeVyplnene)
        {
            await Dialogy.Sprava(titulok, "Najprv nastavte odosielanie e-mailov v menu Súbor → Nastavenia e-mailu.");
            return;
        }
        if (zoznam.Count == 0) { await Dialogy.Sprava(titulok, "Nie je čo poslať."); return; }
        var sEmailom = zoznam.Where(x => Emaily(x.Vlastnici).Length > 0).ToList();
        var bez = zoznam.Where(x => Emaily(x.Vlastnici).Length == 0).Select(x => x.Priestor.Cislo).ToList();
        var info = $"Odošle sa {sEmailom.Count} e-mailov z adresy {nast.Odosielatel}." +
                   (bez.Count > 0 ? $"\nBez e-mailovej adresy (pošlite inak): byty {string.Join(", ", bez)}." : "") +
                   "\nZnačky {byt}, {vs}, {suma}, {vysledok} sa nahradia údajmi bytu.";
        var t = await Dialogy.TextEmailu(titulok, info, predmet, text);
        if (t is null || sEmailom.Count == 0) return;

        Odosiela = true;
        try
        {
            Stav = "Pripravujem prílohy…";
            var spravy = await Task.Run(() => sEmailom.Select(x =>
            {
                var vs = x.Priestor.VariabilnySymbol ?? x.Priestor.Cislo;
                var vysledok = x.Suma < 0 ? $"nedoplatok {PdfVyuctovanie.Eur(-x.Suma)}" : x.Suma > 0 ? $"preplatok {PdfVyuctovanie.Eur(x.Suma)}" : "vyrovnané";
                string Nahrad(string s) => s.Replace("{byt}", x.Priestor.Cislo).Replace("{vs}", vs)
                    .Replace("{suma}", PdfVyuctovanie.Eur(Math.Abs(x.Suma))).Replace("{vysledok}", vysledok);
                var (nazov, obsah) = priloha((x.Vlastnici, x.Priestor));
                return new EmailSprava(Emaily(x.Vlastnici), string.Join(", ", x.Vlastnici.Select(v => v.Meno)),
                    Nahrad(t.Value.Predmet), Nahrad(t.Value.Text), new[] { (nazov, obsah) });
            }).ToList());
            var priebeh = new Progress<int>(i => Stav = $"Odosielam e-maily: {i} / {spravy.Count}");
            var chyby = await Email.Odosli(nast, spravy, priebeh);
            Stav = $"Odoslané: {spravy.Count - chyby.Count} z {spravy.Count}.";
            await Dialogy.Sprava(titulok, chyby.Count == 0
                ? $"Všetkých {spravy.Count} e-mailov bolo odoslaných."
                : $"Odoslané {spravy.Count - chyby.Count} z {spravy.Count}. Chyby:\n" + string.Join("\n", chyby));
        }
        catch (Exception ex)
        {
            Stav = "Odosielanie zlyhalo.";
            await Dialogy.Sprava(titulok, "Odosielanie zlyhalo: " + ex.Message + "\n\nSkontrolujte nastavenia e-mailu.");
        }
        finally
        {
            Odosiela = false;
        }
    }

    public async Task<string?> OtestujEmail(EmailNastavenia n)
    {
        try
        {
            var chyby = await Email.Odosli(n, new[] { new EmailSprava(n.Odosielatel, n.MenoOdosielatela, "Skúšobný e-mail z programu Účtovníctvo",
                "Ak čítate túto správu, odosielanie e-mailov funguje.", Array.Empty<(string, byte[])>()) });
            return chyby.Count == 0 ? null : string.Join("\n", chyby);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ---------- fond opráv ----------

    void NacitajFond()
    {
        PohybyFondu.Clear();
        StavFonduText = "";
        var j = VybranaJednotka;
        if (j is null || !JeSpolocenstvo) return;
        foreach (var x in db.PohybyFondu.Where(x => x.JednotkaId == j.Id).OrderBy(x => x.Datum).ThenBy(x => x.Id)) PohybyFondu.Add(x);
        PrepocitajFond();
    }

    public void PrepocitajFond()
    {
        if (VybranaJednotka is null) return;
        var s = FondOprav.Vypocitaj(Priestory.ToList(), Kategorie.ToList(), PohybyFondu.ToList(), Rok);
        StavFonduText = $"Stav k 1. 1. {Rok}: {PdfVyuctovanie.Eur(s.Zaciatok)}   + tvorba z predpisov: {PdfVyuctovanie.Eur(s.Tvorba)}   " +
                        $"+ iné príjmy: {PdfVyuctovanie.Eur(s.InePrijmy)}   − čerpanie: {PdfVyuctovanie.Eur(s.Cerpanie)}   = stav k 31. 12.: {PdfVyuctovanie.Eur(s.Koniec)}";
    }

    [RelayCommand]
    void PridajPohybFondu()
    {
        var j = VybranaJednotka;
        if (j is null) return;
        var bezStavu = !PohybyFondu.Any(x => x.Typ == TypPohybuFondu.PociatocnyStav);
        var x = new PohybFondu
        {
            JednotkaId = j.Id,
            Datum = bezStavu ? new DateOnly(Rok, 1, 1) : new DateOnly(Rok, Math.Min(DateTime.Today.Month, 12), 1),
            Typ = bezStavu ? TypPohybuFondu.PociatocnyStav : TypPohybuFondu.Cerpanie,
            Popis = bezStavu ? "počiatočný stav fondu" : "",
        };
        db.PohybyFondu.Add(x);
        PohybyFondu.Add(x);
        VybranyPohyb = x;
    }

    [RelayCommand]
    void ZmazPohybFondu()
    {
        if (VybranyPohyb is not { } x) return;
        db.PohybyFondu.Remove(x);
        PohybyFondu.Remove(x);
        PrepocitajFond();
    }

    // ---------- dokumenty pre schôdzu ----------

    [RelayCommand]
    async Task PrezencnaListina()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.UlozSubor("Prezenčná listina (PDF)", "prezencna-listina.pdf", "pdf", "PDF");
        if (s is null) return;
        PdfDokumenty.PrezencnaListina(VybranaJednotka, Priestory.ToList(), DatumVystavenia ?? Dnes, s);
        Stav = $"Uložené: {s}";
    }

    [RelayCommand]
    async Task RocnaSprava()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        Vypocitaj();
        var s = await Dialogy.UlozSubor("Správa o hospodárení (PDF)", $"sprava-o-hospodareni-{Rok}.pdf", "pdf", "PDF");
        if (s is null) return;
        var fond = FondOprav.Vypocitaj(Priestory.ToList(), Kategorie.ToList(), PohybyFondu.ToList(), Rok);
        var kont = Priestory.Select(p => KontoVlastnika.Vypocitaj(p, Rok, 12)).ToList();
        PdfDokumenty.RocnaSprava(VybranaJednotka, new VysledokVyuctovania(Rok, VyuctovanieBytov.ToList(), Upozornenia.ToList()), fond, kont, MiestoVystavenia, DatumVystavenia ?? Dnes, s);
        Stav = $"Uložené: {s}";
    }

    // ---------- import z Excelu ----------

    [RelayCommand]
    async Task SablonaExcel()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        Uloz();
        var s = await Dialogy.UlozSubor("Šablóna na import z Excelu", $"import-{Rok}.xlsx", "xlsx", "Excel");
        if (s is null) return;
        ImportExcel.Sablona(VybranaJednotka, Priestory.ToList(), Kategorie.ToList(), Rok, s);
        Stav = $"Šablóna uložená: {s}";
    }

    [RelayCommand]
    async Task ImportZExcelu()
    {
        if (Dialogy is null || VybranaJednotka is null) return;
        var s = await Dialogy.OtvorSubor("Import bytov a vlastníkov z Excelu", "xlsx", "Excel");
        if (s is null) return;
        if (!await Dialogy.Potvrd("Import z Excelu",
                $"Byty z tabuľky sa spárujú podľa čísla: existujúce sa aktualizujú, nové sa pridajú. Predpisy sa uložia do roka {Rok}.\n\nOdporúčame predtým urobiť zálohu. Pokračovať?"))
            return;
        Uloz();
        VysledokImportu r;
        try
        {
            r = ImportExcel.Importuj(db, VybranaJednotka, Rok, s);
        }
        catch (Exception ex)
        {
            await Dialogy.Sprava("Import z Excelu", "Súbor sa nepodarilo načítať: " + ex.Message + "\n\nNemáte ho otvorený v Exceli?");
            return;
        }
        var id = VybranaJednotka.Id;
        db.ChangeTracker.Clear();
        NacitajJednotky(id);
        await Dialogy.Sprava("Import z Excelu", $"Pridané byty: {r.Pridane}\nAktualizované byty: {r.Aktualizovane}" +
            (r.Upozornenia.Count > 0 ? "\n\nUpozornenia:\n" + string.Join("\n", r.Upozornenia.Take(30)) : ""));
    }

    // ---------- automatická záloha ----------

    public ZalohaNastavenia NastaveniaZalohy() =>
        Core.Services.Nastavenia.Zaloha(db, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(CestaDb))!, "Zalohy"));

    public void UlozNastaveniaZalohy(ZalohaNastavenia z) => Core.Services.Nastavenia.UlozZalohu(db, z);
    public EmailNastavenia NastaveniaEmailu() => Core.Services.Nastavenia.Email(db);
    public void UlozNastaveniaEmailu(EmailNastavenia e) => Core.Services.Nastavenia.UlozEmail(db, e);

    /// <summary>Volá sa pri zatvorení programu.</summary>
    public void Koniec()
    {
        Uloz();
        try
        {
            var cesta = AutomatickaZaloha.Vytvor(CestaDb, NastaveniaZalohy(), DateTime.Now);
            if (cesta is not null) Stav = "Automatická záloha: " + cesta;
        }
        catch
        {
            // záloha nesmie zabrániť zatvoreniu programu
        }
    }
}
