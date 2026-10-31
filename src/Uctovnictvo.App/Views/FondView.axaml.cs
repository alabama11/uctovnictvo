using Avalonia.Controls;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class FondView : UserControl
{
    public FondView()
    {
        InitializeComponent();
        Tabulka.CellEditEnded += (_, _) => (DataContext as MainViewModel)?.PrepocitajFond();
    }
}
