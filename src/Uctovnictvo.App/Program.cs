using System.Globalization;
using Avalonia;

namespace Uctovnictvo.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var sk = CultureInfo.GetCultureInfo("sk-SK");
        CultureInfo.DefaultThreadCurrentCulture = sk;
        CultureInfo.DefaultThreadCurrentUICulture = sk;
        CultureInfo.CurrentCulture = sk;
        CultureInfo.CurrentUICulture = sk;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
