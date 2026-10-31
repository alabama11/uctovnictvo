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

    public static async Task<(string Predmet, string Text)?> TextEmailu(Window vlastnik, string titulok, string info, string predmet, string text)
    {
        var okno = new Window { Title = titulok, Width = 680, Height = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var p = new TextBox { Text = predmet };
        var t = new TextBox { Text = text, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 300 };
        var ok = new Button { Content = "Odoslať", IsDefault = true, MinWidth = 100 };
        ok.Classes.Add("hlavne");
        var zrus = new Button { Content = "Zrušiť", IsCancel = true, MinWidth = 90 };
        (string, string)? vysledok = null;
        ok.Click += (_, _) => { vysledok = (p.Text ?? "", t.Text ?? ""); okno.Close(); };
        zrus.Click += (_, _) => okno.Close();
        okno.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = info, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brushes.DimGray },
                new TextBlock { Text = "Predmet", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                p,
                new TextBlock { Text = "Text správy", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                t,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, zrus } },
            },
        };
        await okno.ShowDialog(vlastnik);
        return vysledok;
    }

    public static async Task<string?> Vstup(Window vlastnik, string titulok, string otazka, string predvolene)
    {
        var okno = new Window { Title = titulok, Width = 460, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var t = new TextBox { Text = predvolene };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90 };
        ok.Classes.Add("hlavne");
        var zrus = new Button { Content = "Zrušiť", IsCancel = true, MinWidth = 90 };
        string? vysledok = null;
        ok.Click += (_, _) => { vysledok = t.Text ?? ""; okno.Close(); };
        zrus.Click += (_, _) => okno.Close();
        okno.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = otazka, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                t,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, zrus } },
            },
        };
        await okno.ShowDialog(vlastnik);
        return vysledok;
    }
}
