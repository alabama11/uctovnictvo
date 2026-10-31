using System.Collections.ObjectModel;
using System.IO.Compression;
using CommunityToolkit.Mvvm.Input;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.ViewModels;

public record PolozkaPrehladu(string Nazov, string Hodnota, string Poznamka = "", bool Upozornenie = false);

/// <summary>Pohodlie: úvodný prehľad firmy, heslo, export pre účtovníka, kontrola aktualizácií.</summary>
public partial class MainViewModel
{
    public ObservableCollection<PolozkaPrehladu> Prehlad { get; } = new();

    // ---------- heslo ----------
    public bool HesloNastavene => Heslo.JeNastavene(db);
    public bool OverHeslo(string h) => Heslo.Over(db, h);
    public void NastavHeslo(string? h)
    {
        Heslo.Nastav(db, h);
        Stav = string.IsNullOrEmpty(h) ? "Heslo pri spustení je zrušené." : "Heslo pri spustení je nastavené.";
    }

    // ---------- úvodný prehľad ----------
    [RelayCommand]
    public void ObnovPrehlad()
    {
        Prehlad.Clear();
        var j = VybranaJednotka;
        if (j is null || j.Typ == TypJednotky.Spolocenstvo) return;
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        string E(decimal d) => PdfVyuctovanie.Eur(d);

        var vydane = vsetkyFaktury.Where(f => f.Druh == DruhFaktury.Vydana && f.Typ != TypFaktury.Zalohova && f.Zostava > 0).ToList();
        var poSpl = vydane.Where(f => f.JePoSplatnosti(dnes)).ToList();
        Prehlad.Add(new("Neuhradené vydané faktúry", $"{vydane.Count} ks · {E(vydane.Sum(f => f.Zostava))}",
            poSpl.Count > 0 ? $"po splatnosti {poSpl.Count} ks za {E(poSpl.Sum(f => f.Zostava))} – pošlite upomienku (Faktúry)" : "nič nie je po splatnosti", poSpl.Count > 0));
        var prijate = vsetkyFaktury.Where(f => f.Druh == DruhFaktury.Prijata && f.Zostava > 0).ToList();
        var naZaplatenie = prijate.Where(f => f.DatumSplatnosti <= dnes.AddDays(7)).ToList();
        Prehlad.Add(new("Nezaplatené prijaté faktúry", $"{prijate.Count} ks · {E(prijate.Sum(f => f.Zostava))}",
            naZaplatenie.Count > 0 ? $"splatné do 7 dní alebo po splatnosti: {naZaplatenie.Count} ks za {E(naZaplatenie.Sum(f => f.Zostava))}" : "", naZaplatenie.Count > 0));

        var (_, riadky) = Pokladna.Kniha(vsetkyDoklady, j.PokladnaPociatocnyStav, Rok);
        var pokladna = riadky.Count > 0 ? riadky[^1].Zostatok : j.PokladnaPociatocnyStav;
        Prehlad.Add(new($"Pokladňa ({Rok})", E(pokladna), pokladna < 0 ? "záporný zostatok – chýba príjmový doklad" : "", pokladna < 0));
        Prehlad.Add(new($"Bankové pohyby {Rok}", $"príjmy {E(vsetkyPohyby.Where(p => p.Datum.Year == Rok && p.Suma > 0).Sum(p => p.Suma))} · výdavky {E(-vsetkyPohyby.Where(p => p.Datum.Year == Rok && p.Suma < 0).Sum(p => p.Suma))}"));

        if (j.Typ == TypJednotky.Szco)
        {
            var s = VypocitajSuhrn(out _);
            Prehlad.Add(new($"Daňová evidencia {Rok}", $"príjmy {E(s.DanovePrijmy)} · výdavky {E(s.DanoveVydavky)} · rozdiel {E(s.Vysledok)}",
                s.Nezaradene != 0 ? $"nezaradené pohyby {E(s.Nezaradene)} – priraďte kategóriu" : "", s.Nezaradene != 0));
        }
        else
        {
            Prehlad.Add(zapisyRoka.Count == 0
                ? new($"Účtovníctvo {Rok}", "nezaúčtované", "kliknite „Zaúčtovať doklady“ v záložke Účtovníctvo", true)
                : new($"Účtovníctvo {Rok}", $"výsledok hospodárenia {E(Podvojne.VysledokHospodarenia(Predvaha.ToList()))}", KontrolyZavierky.StartsWith('✓') ? "" : "pozrite upozornenia v záložke Účtovníctvo → Závierka", !KontrolyZavierky.StartsWith('✓')));
        }
        var dp = RiadkyDp.LastOrDefault(r => r.Zvyraznit);
        if (dp is not null) Prehlad.Add(new($"Daň z príjmov za {Rok} (odhad)", $"{dp.Text}: {E(dp.Suma ?? 0)}", $"priznanie do 31. 3. {Rok + 1}"));

        if (Zamestnanci.Count > 0)
        {
            var posledny = vsetkyMzdy.Where(m => m.Rok == Rok).Select(m => m.Mesiac).DefaultIfEmpty(0).Max();
            Prehlad.Add(new("Mzdy", $"{Zamestnanci.Count(z => z.DatumUkoncenia is null || z.DatumUkoncenia >= dnes)} zamestnanci · spracované do {(posledny == 0 ? "–" : $"{posledny}/{Rok}")}"));
        }
        var zaloha = NastaveniaZalohy();
        if (!zaloha.Zapnuta) Prehlad.Add(new("Zálohy", "automatická záloha je vypnutá", "zapnite ju v menu Súbor → Automatické zálohy", true));
        if (dnes < new DateOnly(2027, 1, 1))
            Prehlad.Add(new("E-faktúry", "od 1. 1. 2027", "aj neplatitelia DPH musia vedieť prijať elektronickú faktúru – import e-faktúry je v záložke Faktúry"));
    }

    // ---------- export pre účtovníka ----------
    [RelayCommand]
    async Task ExportPreUctovnika()
    {
        var j = VybranaJednotka;
        if (j is null || Dialogy is null || j.Typ == TypJednotky.Spolocenstvo) return;
        Uloz();
        var bezpecne = string.Concat(j.Nazov.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        var s = await Dialogy.UlozSubor("Export pre účtovníka (ZIP)", $"{bezpecne}-{Rok}-pre-uctovnika.zip", "zip", "ZIP");
        if (s is null) return;
        var tmp = Directory.CreateTempSubdirectory("export").FullName;
        try
        {
            string P(string n) => Path.Combine(tmp, n);
            ExportExcel.KnihaFaktur(j, vsetkyFaktury, Rok, P($"faktury-{Rok}.xlsx"));
            ExportExcel.BankovePohyby(j, vsetkyPohyby, Rok, P($"banka-{Rok}.xlsx"));
            var (zac, riadky) = Pokladna.Kniha(vsetkyDoklady, j.PokladnaPociatocnyStav, Rok);
            ExportExcel.PokladnicnaKniha(j, Rok, zac, riadky, P($"pokladnicna-kniha-{Rok}.xlsx"));
            var suhrn = VypocitajSuhrn(out var dennik);
            ExportExcel.DanovaEvidencia(j, suhrn, dennik, vsetokMajetok, P($"danova-evidencia-a-majetok-{Rok}.xlsx"));
            if (j.Typ == TypJednotky.Szco)
                await File.WriteAllBytesAsync(P($"danova-evidencia-{Rok}.pdf"), PdfDanovaEvidencia.Vytvor(j, suhrn, dennik, vsetokMajetok));
            if (j.Typ == TypJednotky.Sro && zapisyRoka.Count > 0)
            {
                var zap = ZoradeneZapisy();
                ExportExcel.Uctovnictvo(j, Rok, zap, Predvaha.ToList(), Podvojne.Saldokonto(vsetkyFaktury, new DateOnly(Rok, 12, 31)), Suvaha.ToList(), VykazZiskovAStrat.ToList(), P($"uctovnictvo-{Rok}.xlsx"));
                await File.WriteAllBytesAsync(P($"zavierka-{Rok}.pdf"), PdfZavierka.Zavierka(j, Rok, Suvaha.ToList(), VykazZiskovAStrat.ToList(), Predvaha.ToList(), KontrolyUctovnictva(Suvaha.ToList(), Predvaha.ToList())));
                await File.WriteAllBytesAsync(P($"uctovny-dennik-{Rok}.pdf"), PdfZavierka.Dennik(j, Rok, zap));
            }
            if (RiadkyDp.Count > 0)
                await File.WriteAllBytesAsync(P($"podklad-danove-priznanie-{Rok}.pdf"), PdfDanovePriznanie.Vytvor(j, $"Podklad k daňovému priznaniu za {Rok}", RiadkyDp.ToList(), InfoDp));
            if (Mzdy.Parametre(Rok) is { } pm)
                foreach (var mes in vsetkyMzdy.Where(m => m.Rok == Rok).Select(m => m.Mesiac).Distinct().OrderBy(m => m))
                    await File.WriteAllBytesAsync(P($"mzdy-{Rok}-{mes:00}.pdf"), PdfMzdy.VyplatnePasky(j, pm,
                        vsetkyMzdy.Where(m => m.Rok == Rok && m.Mesiac == mes).ToList(), Mzdy.Rekapitulacia(Rok, mes, vsetkyMzdy)));
            var faktDir = Directory.CreateDirectory(P("faktury")).FullName;
            foreach (var f in vsetkyFaktury.Where(f => f.Druh == DruhFaktury.Vydana && f.DatumVystavenia.Year == Rok))
                await File.WriteAllBytesAsync(Path.Combine(faktDir, $"{string.Concat(f.Cislo.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' or ':' ? '-' : c))}.pdf"), PdfFaktury.Faktura(f, j));
            Zaloha.Vytvor(CestaDb, P("zaloha-celej-databazy.zip"));
            if (File.Exists(s)) File.Delete(s);
            ZipFile.CreateFromDirectory(tmp, s);
            Stav = $"Export pre účtovníka uložený: {s}";
            await Dialogy.Sprava("Export pre účtovníka", $"Hotovo. Súbor obsahuje knihy v Exceli, PDF zostavy, vydané faktúry a zálohu databázy za rok {Rok}:\n{s}");
        }
        finally { Directory.Delete(tmp, true); }
    }

    // ---------- aktualizácie ----------
    [RelayCommand]
    async Task SkontrolujAktualizacie()
    {
        if (Dialogy is null) return;
        var aktualna = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0, 0);
        try
        {
            var n = await Aktualizacie.Zisti();
            if (n is null) await Dialogy.Sprava("Aktualizácie", $"Nepodarilo sa zistiť najnovšiu verziu. Pozrite {Aktualizacie.StrankaVydani}");
            else if (Aktualizacie.JeNovsia(aktualna, n.Verzia))
            {
                if (await Dialogy.Potvrd("Aktualizácie", $"Je k dispozícii verzia {n.Verzia.ToString(3)} (máte {aktualna.ToString(3)}). Otvoriť stránku na stiahnutie?\n\nPred aktualizáciou si urobte zálohu (Súbor → Zálohovať). Údaje zostávajú v priečinku Dokumenty\\Uctovnictvo."))
                    OtvorAdresu(n.Adresa);
            }
            else await Dialogy.Sprava("Aktualizácie", $"Máte najnovšiu verziu {aktualna.ToString(3)}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await Dialogy.Sprava("Aktualizácie", $"Bez pripojenia k internetu sa nedá overiť. Stránka s verziami: {Aktualizacie.StrankaVydani}");
        }
    }

    static void OtvorAdresu(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* bez prehliadača */ }
    }
}
