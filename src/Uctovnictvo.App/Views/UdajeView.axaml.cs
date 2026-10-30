using Avalonia.Controls;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class UdajeView : UserControl
{
    public UdajeView()
    {
        InitializeComponent();
        Nazov.LostFocus += (_, _) => (DataContext as MainViewModel)?.ObnovNazovJednotky();
        Typ.SelectionChanged += (_, e) =>
        {
            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is not null && e.AddedItems.Count > 0 && e.AddedItems[0] is not null)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => (DataContext as MainViewModel)?.ObnovNazovJednotky());
        };
    }
}
