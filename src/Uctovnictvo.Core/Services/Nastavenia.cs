using System.Security.Cryptography;
using System.Text;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

public record EmailNastavenia(
    string Server = "",
    int Port = 587,
    bool Ssl = false,
    string Pouzivatel = "",
    string Heslo = "",
    string Odosielatel = "",
    string MenoOdosielatela = "")
{
    public bool JeVyplnene => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Odosielatel);
}

public record ZalohaNastavenia(bool Zapnuta, string Priecinok, int Pocet);

/// <summary>Čítanie a zápis nastavení programu v databáze.</summary>
public static class Nastavenia
{
    public static string? Citaj(AppDbContext db, string kluc) => db.Nastavenia.FirstOrDefault(n => n.Kluc == kluc)?.Hodnota;

    public static void Zapis(AppDbContext db, string kluc, string? hodnota)
    {
        var n = db.Nastavenia.FirstOrDefault(x => x.Kluc == kluc);
        if (n is null) db.Nastavenia.Add(new Nastavenie { Kluc = kluc, Hodnota = hodnota });
        else n.Hodnota = hodnota;
        db.SaveChanges();
    }

    public static EmailNastavenia Email(AppDbContext db) => new(
        Citaj(db, "email.server") ?? "",
        int.TryParse(Citaj(db, "email.port"), out var port) ? port : 587,
        Citaj(db, "email.ssl") == "1",
        Citaj(db, "email.pouzivatel") ?? "",
        Odkry(Citaj(db, "email.heslo")),
        Citaj(db, "email.odosielatel") ?? "",
        Citaj(db, "email.meno") ?? "");

    public static void UlozEmail(AppDbContext db, EmailNastavenia e)
    {
        Zapis(db, "email.server", e.Server.Trim());
        Zapis(db, "email.port", e.Port.ToString());
        Zapis(db, "email.ssl", e.Ssl ? "1" : "0");
        Zapis(db, "email.pouzivatel", e.Pouzivatel.Trim());
        Zapis(db, "email.heslo", Skry(e.Heslo));
        Zapis(db, "email.odosielatel", e.Odosielatel.Trim());
        Zapis(db, "email.meno", e.MenoOdosielatela.Trim());
    }

    public static ZalohaNastavenia Zaloha(AppDbContext db, string predvolenyPriecinok) => new(
        Citaj(db, "zaloha.zapnuta") != "0",
        Citaj(db, "zaloha.priecinok") is { Length: > 0 } p ? p : predvolenyPriecinok,
        int.TryParse(Citaj(db, "zaloha.pocet"), out var n) && n > 0 ? n : 30);

    public static void UlozZalohu(AppDbContext db, ZalohaNastavenia z)
    {
        Zapis(db, "zaloha.zapnuta", z.Zapnuta ? "1" : "0");
        Zapis(db, "zaloha.priecinok", z.Priecinok);
        Zapis(db, "zaloha.pocet", z.Pocet.ToString());
    }

    // Heslo k e-mailu sa vo Windows šifruje pre prihláseného používateľa (DPAPI).
    static string Skry(string heslo)
    {
        if (string.IsNullOrEmpty(heslo)) return "";
        var data = Encoding.UTF8.GetBytes(heslo);
        if (OperatingSystem.IsWindows())
            return "dpapi:" + Convert.ToBase64String(ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser));
        return "b64:" + Convert.ToBase64String(data);
    }

    static string Odkry(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        try
        {
            if (s.StartsWith("dpapi:") && OperatingSystem.IsWindows())
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s[6..]), null, DataProtectionScope.CurrentUser));
            if (s.StartsWith("b64:"))
                return Encoding.UTF8.GetString(Convert.FromBase64String(s[4..]));
        }
        catch (CryptographicException) { }
        return "";
    }
}

/// <summary>Automatická záloha pri zatvorení programu s rotáciou starých záloh.</summary>
public static class AutomatickaZaloha
{
    public const string Predpona = "auto-";

    public static string? Vytvor(string dbPath, ZalohaNastavenia n, DateTime cas)
    {
        if (!n.Zapnuta) return null;
        Directory.CreateDirectory(n.Priecinok);
        var cesta = Path.Combine(n.Priecinok, Predpona + Services.Zaloha.NavrhniNazov(cas));
        Services.Zaloha.Vytvor(dbPath, cesta);
        foreach (var stara in Directory.GetFiles(n.Priecinok, Predpona + "*.zip").OrderByDescending(f => f).Skip(n.Pocet))
            File.Delete(stara);
        return cesta;
    }
}
