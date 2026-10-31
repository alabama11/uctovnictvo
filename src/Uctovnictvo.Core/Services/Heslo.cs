using System.Security.Cryptography;
using Uctovnictvo.Core.Data;

namespace Uctovnictvo.Core.Services;

/// <summary>
/// Voliteľné heslo pri spustení programu. Ukladá sa len odtlačok (PBKDF2-SHA256 so soľou), nie heslo.
/// Chráni otvorenie programu; samotný súbor databázy nešifruje.
/// </summary>
public static class Heslo
{
    const string Kluc = "heslo.pbkdf2";
    const int Iteracie = 200_000;

    public static bool JeNastavene(AppDbContext db) => !string.IsNullOrEmpty(Nastavenia.Citaj(db, Kluc));

    public static string Odtlacok(string heslo, byte[]? sol = null)
    {
        sol ??= RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(heslo, sol, Iteracie, HashAlgorithmName.SHA256, 32);
        return $"{Iteracie}:{Convert.ToBase64String(sol)}:{Convert.ToBase64String(hash)}";
    }

    public static bool OverOdtlacok(string heslo, string odtlacok)
    {
        var c = odtlacok.Split(':');
        if (c.Length != 3 || !int.TryParse(c[0], out var it)) return false;
        var sol = Convert.FromBase64String(c[1]);
        var ocakavany = Convert.FromBase64String(c[2]);
        var hash = Rfc2898DeriveBytes.Pbkdf2(heslo, sol, it, HashAlgorithmName.SHA256, ocakavany.Length);
        return CryptographicOperations.FixedTimeEquals(hash, ocakavany);
    }

    public static bool Over(AppDbContext db, string heslo) => Nastavenia.Citaj(db, Kluc) is not { Length: > 0 } o || OverOdtlacok(heslo, o);

    /// <summary>Nastaví nové heslo; prázdne heslo ochranu zruší.</summary>
    public static void Nastav(AppDbContext db, string? heslo) => Nastavenia.Zapis(db, Kluc, string.IsNullOrEmpty(heslo) ? null : Odtlacok(heslo));
}
