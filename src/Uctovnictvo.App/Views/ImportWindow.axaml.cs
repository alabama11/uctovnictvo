using Avalonia.Controls;
using Avalonia.Interactivity;
using Uctovnictvo.App.ViewModels;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.App.Views;

public partial class ImportWindow : Window
{
    public IReadOnlyList<Priestor> Priestory { get; } = Array.Empty<Priestor>();

    public ImportWindow() => InitializeComponent();

    public ImportWindow(IReadOnlyList<ImportRiadok> riadky, IReadOnlyList<Priestor> priestory)
    {
        Priestory = priestory;
        foreach (var r in riadky) r.Moznosti = priestory;
        InitializeComponent();
        Tabulka.ItemsSource = riadky;
    }

    static readonly Avalonia.Media.IBrush SlaboCervena = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FADBD8"));

    /// <summary>Platby bez variabilného symbolu sú podfarbené slabočervenou.</summary>
    void NacitanieRiadku(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is ImportRiadok { BezVs: true }) e.Row.Background = SlaboCervena;
        else e.Row.ClearValue(DataGridRow.BackgroundProperty);
    }

    void Ok(object? sender, RoutedEventArgs e) => Close(true);
    void Zrus(object? sender, RoutedEventArgs e) => Close(false);
}
