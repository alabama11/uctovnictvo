using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Uctovnictvo.App.ViewModels;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.App.Views;

/// <summary>Nastavenie SMTP servera na odosielanie vyúčtovaní a upomienok.</summary>
public class NastaveniaEmailuWindow : Window
{
    public NastaveniaEmailuWindow(MainViewModel vm)
    {
        Title = "Nastavenia e-mailu";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var n = vm.NastaveniaEmailu();
        var server = new TextBox { Text = n.Server, Watermark = "napr. smtp.gmail.com" };
        var port = new NumericUpDown { Value = n.Port, Minimum = 1, Maximum = 65535, FormatString = "0", Increment = 1 };
        var ssl = new CheckBox { IsChecked = n.Ssl, Content = "SSL hneď od pripojenia (port 465); inak STARTTLS (port 587)" };
        var pouz = new TextBox { Text = n.Pouzivatel };
        var heslo = new TextBox { Text = n.Heslo, PasswordChar = '•' };
        var od = new TextBox { Text = n.Odosielatel, Watermark = "adresa, z ktorej sa posiela" };
        var meno = new TextBox { Text = n.MenoOdosielatela, Watermark = "napr. Spoločenstvo Spiežovec" };
        var stav = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };

        EmailNastavenia Aktualne() => new(server.Text ?? "", (int)(port.Value ?? 587), ssl.IsChecked == true, pouz.Text ?? "", heslo.Text ?? "", od.Text ?? "", meno.Text ?? "");

        var test = new Button { Content = "Poslať skúšobný e-mail sebe" };
        test.Click += async (_, _) =>
        {
            stav.Text = "Odosielam…";
            var chyba = await vm.OtestujEmail(Aktualne());
            stav.Text = chyba is null ? $"✓ Skúšobný e-mail odišiel na {od.Text}." : "✗ " + chyba;
        };
        var uloz = new Button { Content = "Uložiť", IsDefault = true, MinWidth = 90 };
        uloz.Classes.Add("hlavne");
        uloz.Click += (_, _) => { vm.UlozNastaveniaEmailu(Aktualne()); Close(); };
        var zrus = new Button { Content = "Zrušiť", IsCancel = true, MinWidth = 90 };
        zrus.Click += (_, _) => Close();

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*")};
        void Riadok(string nazov, Control c)
        {
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var r = g.RowDefinitions.Count - 1;
            var t = new TextBlock { Text = nazov, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(t, r);
            Grid.SetRow(c, r);
            Grid.SetColumn(c, 1);
            c.Margin = new Thickness(0, 3);
            g.Children.Add(t);
            g.Children.Add(c);
        }
        Riadok("SMTP server", server);
        Riadok("Port", port);
        Riadok("", ssl);
        Riadok("Používateľ", pouz);
        Riadok("Heslo", heslo);
        Riadok("Odosielateľ (e-mail)", od);
        Riadok("Meno odosielateľa", meno);

        Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Údaje nájdete u svojho poskytovateľa e-mailu. Pri Gmaile alebo Outlook.com treba namiesto bežného hesla použiť „heslo aplikácie“. Heslo sa ukladá zašifrované pre váš účet vo Windows.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brushes.DimGray,
                },
                g,
                test,
                stav,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { uloz, zrus } },
            },
        };
    }
}

/// <summary>Nastavenie automatickej zálohy pri zatvorení programu.</summary>
public class NastaveniaZalohWindow : Window
{
    public NastaveniaZalohWindow(MainViewModel vm)
    {
        Title = "Automatické zálohy";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var n = vm.NastaveniaZalohy();
        var zap = new CheckBox { IsChecked = n.Zapnuta, Content = "Pri každom zatvorení programu urobiť zálohu" };
        var priecinok = new TextBox { Text = n.Priecinok };
        var vyber = new Button { Content = "Vybrať…" };
        vyber.Click += async (_, _) =>
        {
            var d = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Priečinok na zálohy", AllowMultiple = false });
            if (d.Count > 0 && d[0].TryGetLocalPath() is { } cesta) priecinok.Text = cesta;
        };
        var pocet = new NumericUpDown { Value = n.Pocet, Minimum = 1, Maximum = 1000, FormatString = "0", Increment = 1, Width = 130, HorizontalAlignment = HorizontalAlignment.Left };
        var uloz = new Button { Content = "Uložiť", IsDefault = true, MinWidth = 90 };
        uloz.Classes.Add("hlavne");
        uloz.Click += (_, _) =>
        {
            vm.UlozNastaveniaZalohy(new ZalohaNastavenia(zap.IsChecked == true, priecinok.Text ?? n.Priecinok, (int)(pocet.Value ?? 30)));
            Close();
        };
        var zrus = new Button { Content = "Zrušiť", IsCancel = true, MinWidth = 90 };
        zrus.Click += (_, _) => Close();

        var riadokPriecinka = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(vyber, Dock.Right);
        riadokPriecinka.Children.Add(vyber);
        riadokPriecinka.Children.Add(priecinok);

        Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 10,
            Children =
            {
                zap,
                new TextBlock { Text = "Priečinok – odporúčame priečinok OneDrive alebo Google Drive, aby boli zálohy aj mimo počítača:", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                riadokPriecinka,
                new TextBlock { Text = "Koľko posledných záloh ponechať:" },
                pocet,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { uloz, zrus } },
            },
        };
    }
}
