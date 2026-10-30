using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Uctovnictvo.App.Views;

/// <summary>Jednoduché okno so správou alebo otázkou Áno/Nie.</summary>
public static class Dialog
{
    public static async Task<bool> Ukaz(Window vlastnik, string titulok, string text, bool otazka)
    {
        var okno = new Window
        {
            Title = titulok,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        var ok = new Button { Content = otazka ? "Áno" : "OK", IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Classes.Add("hlavne");
        var tlacidla = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        tlacidla.Children.Add(ok);
        if (otazka)
        {
            var nie = new Button { Content = "Nie", IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            nie.Click += (_, _) => okno.Close(false);
            tlacidla.Children.Add(nie);
        }
        ok.Click += (_, _) => okno.Close(true);
        okno.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 18,
            Children =
            {
                new SelectableTextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                tlacidla,
            },
        };
        return await okno.ShowDialog<bool?>(vlastnik) ?? false;
    }
}
