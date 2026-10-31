using Avalonia.Controls;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class FakturyView : UserControl
{
    public FakturyView()
    {
        InitializeComponent();
        Polozky.CellEditEnded += (_, _) => (DataContext as MainViewModel)?.PrepocitajFaktury();
    }
}
