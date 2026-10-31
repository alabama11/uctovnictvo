using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Uctovnictvo.App.ViewModels;
using Uctovnictvo.App.Views;
using Uctovnictvo.Core.Data;

namespace Uctovnictvo.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var cesta = Environment.GetEnvironmentVariable("UCTOVNICTVO_DB") ?? Databaza.PredvolenaCesta;
            Databaza.Priprav(cesta);
            bool chranene;
            using (var db = AppDbContext.Open(cesta)) chranene = Core.Services.Heslo.JeNastavene(db);
            if (!chranene)
                desktop.MainWindow = new MainWindow { DataContext = new MainViewModel(cesta) };
            else
            {
                PrihlasenieWindow? login = null;
                login = new PrihlasenieWindow(h =>
                {
                    using var db = AppDbContext.Open(cesta);
                    return Core.Services.Heslo.Over(db, h);
                }, () =>
                {
                    var hlavne = new MainWindow { DataContext = new MainViewModel(cesta) };
                    desktop.MainWindow = hlavne;
                    hlavne.Show();
                    login!.Close();
                });
                desktop.MainWindow = login;
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
