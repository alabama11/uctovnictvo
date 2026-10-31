using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Uctovnictvo.App.ViewModels;
using Uctovnictvo.App.Views;

// Vytvorí snímky obrazoviek všetkých záložiek s ukážkovými dátami (na kontrolu vzhľadu bez Windows).
var vystup = args.Length > 0 ? args[0] : "snimky";
Directory.CreateDirectory(vystup);
var db = Path.Combine(Path.GetTempPath(), $"snimky-{Guid.NewGuid():N}.db");
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sk-SK");

AppBuilder.Configure<Uctovnictvo.App.App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var vm = new MainViewModel(db) { Rok = 2025 };
vm.VytvorUkazkuCommand.Execute(null);
var w = new MainWindow { DataContext = vm, Width = 1320, Height = 820 };
w.Show();

void Snimka(string nazov)
{
    Dispatcher.UIThread.RunJobs();
    for (int i = 0; i < 3; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
    w.CaptureRenderedFrame()!.Save(Path.Combine(vystup, nazov + ".png"));
}

var zalozky = w.FindControl<TabControl>("Zalozky")!;
int Index(string hlavicka) => zalozky.Items.Cast<TabItem>().ToList().FindIndex(t => (string?)t.Header == hlavicka);
(string Tab, string Subor)[] mena = { ("Údaje", "1-udaje"), ("Byty a vlastníci", "2-byty"), ("Položky nákladov", "3-polozky"), ("Predpisy a odpočty", "4-predpisy"),
    ("Úhrady", "5-uhrady"), ("Konto a upomienky", "6-konto"), ("Vyúčtovanie", "7-vyuctovanie"), ("Fond opráv", "8-fond") };
foreach (var (tab, subor) in mena)
{
    zalozky.SelectedIndex = Index(tab);
    if (tab == "Vyúčtovanie") vm.VypocitajCommand.Execute(null);
    Snimka(subor);
}
// vnorené záložky roka
zalozky.SelectedIndex = Index("Predpisy a odpočty");
Dispatcher.UIThread.RunJobs();
var vnorene = w.GetVisualDescendantsOfType<TabControl>().First(t => t != zalozky);
vnorene.SelectedIndex = 1; Snimka("4b-odpocty");
vnorene.SelectedIndex = 2; Snimka("4c-naklady");
// firma
vm.VybranaJednotka = vm.Jednotky.First(j => j.Typ != Uctovnictvo.Core.Model.TypJednotky.Spolocenstvo);
Dispatcher.UIThread.RunJobs();
zalozky.SelectedIndex = Index("Faktúry"); Snimka("9-faktury");
vm.DruhFaktur = 1; Snimka("9b-prijate"); vm.DruhFaktur = 0;
zalozky.SelectedIndex = Index("Adresár"); Snimka("10-adresar");
zalozky.SelectedIndex = Index("Banka"); Snimka("11-banka");
zalozky.SelectedIndex = Index("Pokladňa"); Snimka("12-pokladna");
vm.ZaradAutomatickyCommand.Execute(null);
zalozky.SelectedIndex = Index("Daňová evidencia"); Snimka("13-danova-evidencia");
zalozky.SelectedIndex = Index("Majetok"); Snimka("14-majetok");
zalozky.SelectedIndex = Index("Kniha jázd"); Snimka("15-kniha-jazd");
vm.ZauctujDokladyCommand.Execute(null);
zalozky.SelectedIndex = Index("Účtovníctvo");
Dispatcher.UIThread.RunJobs();
var uct = w.GetVisualDescendantsOfType<TabControl>().First(t => t != zalozky && t.Items.Cast<TabItem>().Any(x => (string?)x.Header == "Závierka"));
string[] uctMena = { "16a-dennik", "16b-hlavna-kniha", "16c-saldokonto", "16d-zavierka", "16e-rozvrh" };
for (int i = 0; i < uctMena.Length; i++) { uct.SelectedIndex = i; Snimka(uctMena[i]); }
zalozky.SelectedIndex = Index("Daňové priznanie"); Snimka("17-danove-priznanie");
vm.Rok = 2026; vm.MesiacMzdy = 3;
Dispatcher.UIThread.RunJobs();
zalozky.SelectedIndex = Index("Mzdy");
vm.SpracujMzdyCommand.Execute(null);
vm.MesiacMzdy = 1; vm.SpracujMzdyCommand.Execute(null);
vm.MesiacMzdy = 2; vm.SpracujMzdyCommand.Execute(null);
Snimka("18-mzdy");
zalozky.SelectedIndex = Index("Prehľad"); Snimka("0-prehlad-firmy");
Console.WriteLine("Hotovo: " + Path.GetFullPath(vystup));

static class Rozsirenia
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual v) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>();
}
