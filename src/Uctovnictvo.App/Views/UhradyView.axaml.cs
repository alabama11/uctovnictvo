using Avalonia.Controls;
using Avalonia.Interactivity;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class UhradyView : UserControl
{
    public UhradyView()
    {
        InitializeComponent();
        Tabulka.CellEditEnded += (_, _) => (DataContext as MainViewModel)?.PrepocitajSumuUhrad();
    }

    void ZrusFilter(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.FilterUhradPriestor = null;
    }
}
