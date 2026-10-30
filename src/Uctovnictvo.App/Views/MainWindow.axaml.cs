using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Uctovnictvo.App.ViewModels;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.App.Views;

public partial class MainWindow : Window, IDialogy
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.Dialogy = this;
        };
        Closing += (_, _) => (DataContext as MainViewModel)?.Uloz();
        Zalozky.SelectionChanged += (_, e) =>
        {
            if (e.Source == Zalozky) (DataContext as MainViewModel)?.Uloz();
        };
    }

    MainViewModel? Vm => DataContext as MainViewModel;

    void Koniec(object? sender, RoutedEventArgs e) => Close();

    void OtvorPriecinok(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var dir = Path.GetDirectoryName(Path.GetFullPath(Vm.CestaDb))!;
        try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
        catch { _ = Sprava("Priečinok s údajmi", dir); }
    }

    void OProgramme(object? sender, RoutedEventArgs e)
    {
        var verzia = typeof(MainWindow).Assembly.GetName().Version;
        _ = Sprava("O programe", $"Účtovníctvo {verzia?.ToString(3)}\n\nEvidencia spoločenstiev vlastníkov bytov a nebytových priestorov, ročné vyúčtovanie, úhrady a zálohy.\n\nÚdaje: {Vm?.CestaDb}");
    }

    // ---------- IDialogy ----------

    public async Task<string?> UlozSubor(string titulok, string navrhovanyNazov, string pripona, string popisTypu)
    {
        var f = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = titulok,
            SuggestedFileName = navrhovanyNazov,
            DefaultExtension = pripona,
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { new FilePickerFileType(popisTypu) { Patterns = new[] { "*." + pripona } } },
        });
        return f?.TryGetLocalPath();
    }

    public async Task<string?> OtvorSubor(string titulok, string pripona, string popisTypu)
    {
        var f = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = titulok,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(popisTypu) { Patterns = new[] { "*." + pripona } }, FilePickerFileTypes.All },
        });
        return f.Count > 0 ? f[0].TryGetLocalPath() : null;
    }

    public Task Sprava(string titulok, string text) => Dialog.Ukaz(this, titulok, text, false);

    public Task<bool> Potvrd(string titulok, string text) => Dialog.Ukaz(this, titulok, text, true);

    public Task<bool> ImportPlatieb(IReadOnlyList<ImportRiadok> riadky, IReadOnlyList<Priestor> priestory) =>
        new ImportWindow(riadky, priestory).ShowDialog<bool>(this);
}
