using Avalonia.Controls;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class PokladnaView : UserControl
{
    public PokladnaView()
    {
        InitializeComponent();
        Tabulka.CellEditEnded += (_, _) => (DataContext as MainViewModel)?.NacitajPokladnu();
    }
}
