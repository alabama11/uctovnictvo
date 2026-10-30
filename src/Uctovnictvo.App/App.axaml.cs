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
            var vm = new MainViewModel(cesta);
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
