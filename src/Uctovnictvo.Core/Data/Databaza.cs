using Microsoft.EntityFrameworkCore;

namespace Uctovnictvo.Core.Data;

/// <summary>Umiestnenie dátového súboru a jeho príprava pri štarte.</summary>
public static class Databaza
{
    public const string NazovSuboru = "uctovnictvo.db";

    public static string PredvolenyPriecinok =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Uctovnictvo");

    public static string PredvolenaCesta => Path.Combine(PredvolenyPriecinok, NazovSuboru);

    /// <summary>Vytvorí priečinok a databázu, prípadne doplní nové tabuľky po aktualizácii programu.</summary>
    public static void Priprav(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        using var db = AppDbContext.Open(dbPath);
        db.Database.Migrate();
    }
}
