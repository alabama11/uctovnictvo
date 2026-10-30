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
        InitializeComponent();
        Tabulka.ItemsSource = riadky;
    }

    void Ok(object? sender, RoutedEventArgs e) => Close(true);
    void Zrus(object? sender, RoutedEventArgs e) => Close(false);
}
