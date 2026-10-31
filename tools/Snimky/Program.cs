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
string[] mena = { "1-udaje", "2-byty", "3-polozky", "4-predpisy", "5-uhrady", "6-konto", "7-vyuctovanie", "8-fond" };
for (int i = 0; i < mena.Length; i++)
{
    zalozky.SelectedIndex = i;
    if (i == 6) vm.VypocitajCommand.Execute(null);
    Snimka(mena[i]);
}
// vnorené záložky roka
zalozky.SelectedIndex = 3;
Dispatcher.UIThread.RunJobs();
var vnorene = w.GetVisualDescendantsOfType<TabControl>().First(t => t != zalozky);
vnorene.SelectedIndex = 1; Snimka("4b-odpocty");
vnorene.SelectedIndex = 2; Snimka("4c-naklady");
Console.WriteLine("Hotovo: " + Path.GetFullPath(vystup));

static class Rozsirenia
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual v) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>();
}
