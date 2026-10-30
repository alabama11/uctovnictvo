using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace Uctovnictvo.Core.Services;

/// <summary>Záloha a obnova celej databázy do/zo ZIP súboru.</summary>
public static class Zaloha
{
    const string SuborVZipe = "uctovnictvo.db";

    public static string NavrhniNazov(DateTime cas) => $"uctovnictvo-zaloha-{cas:yyyy-MM-dd-HHmm}.zip";

    /// <summary>Vytvorí konzistentnú kópiu databázy (aj počas behu programu) a zbalí ju do ZIP.</summary>
    public static void Vytvor(string dbPath, string zipPath)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"uctovnictvo-{Guid.NewGuid():N}.db");
        try
        {
            using (var zdroj = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
            using (var ciel = new SqliteConnection($"Data Source={tmp};Pooling=False"))
            {
                zdroj.Open();
                ciel.Open();
                zdroj.BackupDatabase(ciel);
            }
            SqliteConnection.ClearAllPools();
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(tmp, SuborVZipe, CompressionLevel.Optimal);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>
    /// Nahradí databázu obsahom zálohy. Pôvodný súbor sa predtým odloží vedľa ako .pred-obnovou.
    /// Volajúci musí mať zatvorené všetky spojenia na databázu.
    /// </summary>
    public static void Obnov(string zipPath, string dbPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(SuborVZipe)
            ?? throw new InvalidDataException("Súbor nie je záloha tohto programu (chýba uctovnictvo.db).");

        var tmp = dbPath + ".obnova";
        entry.ExtractToFile(tmp, overwrite: true);
        OverDatabazu(tmp);

        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
            File.Copy(dbPath, dbPath + ".pred-obnovou", overwrite: true);
        File.Move(tmp, dbPath, overwrite: true);
    }

    static void OverDatabazu(string path)
    {
        using var c = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Jednotky'";
        if (Convert.ToInt64(cmd.ExecuteScalar()) != 1)
            throw new InvalidDataException("Záloha neobsahuje platnú databázu programu.");
    }
}
