using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.App;

/// <summary>Zobrazí desatinné číslo po slovensky a pri zadávaní prijme čiarku aj bodku.</summary>
public class DesatinnyPrevodnik : IValueConverter
{
    static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");
    public int Miesta { get; set; } = 2;
    public bool PovolitPrazdne { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal d => d.ToString("N" + Miesta, Sk),
        int i => i.ToString(Sk),
        null => "",
        _ => value.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string ?? "").Replace(" ", "").Replace(" ", "").Replace("€", "").Replace(',', '.').Trim();
        if (s.Length == 0)
            return PovolitPrazdne || Nullable.GetUnderlyingType(targetType) is not null ? null : 0m;
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d;
        return new BindingNotification(new FormatException($"„{value}“ nie je číslo."), BindingErrorType.DataValidationError);
    }
}

public class DatumPrevodnik : IValueConverter
{
    static readonly string[] Formaty = { "d.M.yyyy", "d. M. yyyy", "d.M.yy", "yyyy-MM-dd" };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly d ? d.ToString("d.M.yyyy") : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string ?? "").Trim();
        if (s.Length == 0) return null;
        if (DateOnly.TryParseExact(s, Formaty, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        return new BindingNotification(new FormatException($"„{s}“ nie je dátum (napr. 10.1.2025)."), BindingErrorType.DataValidationError);
    }
}

/// <summary>Slovenské názvy hodnôt číselníkov.</summary>
public class EnumTextPrevodnik : IValueConverter
{
    public static string Text(object? value) => value switch
    {
        TypJednotky.Szco => "SZČO (živnostník)",
        TypJednotky.Sro => "s.r.o.",
        TypJednotky.Spolocenstvo => "Spoločenstvo vlastníkov",
        TypPriestoru.Byt => "byt",
        TypPriestoru.NebytovyPriestor => "nebytový priestor",
        KlucRozpoctu k => Core.Services.Vyuctovanie.NazovKluca(k),
        UcelUhrady u => Core.Services.ExportExcel.NazovUcelu(u),
        null => "",
        _ => value.ToString() ?? "",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Text(value);
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public static class Zoznamy
{
    public static TypJednotky[] TypyJednotiek { get; } = Enum.GetValues<TypJednotky>();
    public static TypPriestoru[] TypyPriestorov { get; } = Enum.GetValues<TypPriestoru>();
    public static KlucRozpoctu[] Kluce { get; } = Enum.GetValues<KlucRozpoctu>();
    public static UcelUhrady[] Ucely { get; } = Enum.GetValues<UcelUhrady>();
}

public class IbanKontrolaPrevodnik : IValueConverter
{
    public static readonly IbanKontrolaPrevodnik Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Core.Services.Iban.JePlatny(value as string) ? "✓ platný" : "✗ neplatný";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
