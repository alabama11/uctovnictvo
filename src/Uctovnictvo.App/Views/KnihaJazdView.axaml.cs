using Avalonia.Controls;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class KnihaJazdView : UserControl
{
    public KnihaJazdView()
    {
        InitializeComponent();
        void Obnov() => (DataContext as MainViewModel)?.PrepocitajJazdy();
        Tabulka.CellEditEnded += (_, _) => Obnov();
        Spotreba.LostFocus += (_, _) => Obnov();
        Cena.LostFocus += (_, _) => Obnov();
        Majetok.IsCheckedChanged += (_, _) => Obnov();
    }
}
