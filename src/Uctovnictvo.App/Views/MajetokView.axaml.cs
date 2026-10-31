using Avalonia.Controls;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class MajetokView : UserControl
{
    public MajetokView()
    {
        InitializeComponent();
        Tabulka.CellEditEnded += (_, _) => (DataContext as MainViewModel)?.MajetokZmeneny();
    }
}
