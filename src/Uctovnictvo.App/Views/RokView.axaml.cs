using Avalonia.Controls;
using Avalonia.Data;
using Uctovnictvo.App.ViewModels;

namespace Uctovnictvo.App.Views;

public partial class RokView : UserControl
{
    MainViewModel? vm;

    public RokView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (vm is not null) vm.KategorieZmenene -= PostavStlpce;
            vm = DataContext as MainViewModel;
            if (vm is not null) vm.KategorieZmenene += PostavStlpce;
            PostavStlpce();
        };
    }

    /// <summary>Stĺpec pre každú položku nákladov – menia sa podľa nastavenia spoločenstva.</summary>
    void PostavStlpce()
    {
        var eur = (Avalonia.Data.Converters.IValueConverter)Avalonia.Application.Current!.Resources["Eur"]!;
        Predpisy.Columns.Clear();
        Predpisy.Columns.Add(new DataGridTextColumn { Header = "Byt", Binding = new Binding("Priestor.Cislo"), IsReadOnly = true, Width = new DataGridLength(64) });
        Predpisy.Columns.Add(new DataGridTextColumn { Header = "Vlastník", Binding = new Binding("Priestor.MenaVlastnikov"), IsReadOnly = true, Width = new DataGridLength(180) });
        if (vm is null) return;
        for (int i = 0; i < vm.Kategorie.Count; i++)
        {
            var k = vm.Kategorie[i];
            Predpisy.Columns.Add(new DataGridTextColumn
            {
                Header = new TextBlock { Text = k.Nazov, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 110, FontSize = 12 },
                Binding = new Binding($"Bunky[{i}].Suma") { Converter = eur, Mode = BindingMode.TwoWay },
                Width = new DataGridLength(115),
                CellStyleClasses = { "cislo" },
            });
        }
        Predpisy.Columns.Add(new DataGridTextColumn { Header = "Spolu za rok", Binding = new Binding("Spolu") { Converter = eur }, IsReadOnly = true, Width = new DataGridLength(110), CellStyleClasses = { "cislo" } });
        Predpisy.Columns.Add(new DataGridTextColumn { Header = "Mesačne", Binding = new Binding("Mesacne") { Converter = eur }, IsReadOnly = true, Width = new DataGridLength(100), CellStyleClasses = { "cislo" } });
    }
}
