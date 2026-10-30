using System.Numerics;
using System.Text;

namespace Uctovnictvo.Core.Services;

public static class Iban
{
    /// <summary>Odstráni medzery a prevedie na veľké písmená.</summary>
    public static string Normalizuj(string? iban) =>
        new string((iban ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    /// <summary>Kontrola IBAN podľa ISO 13616 (mod 97). Pre SK navyše dĺžka 24 znakov.</summary>
    public static bool JePlatny(string? iban)
    {
        var s = Normalizuj(iban);
        if (s.Length < 15 || s.Length > 34) return false;
        if (!char.IsLetter(s[0]) || !char.IsLetter(s[1]) || !char.IsDigit(s[2]) || !char.IsDigit(s[3])) return false;
        if (s.StartsWith("SK") && s.Length != 24) return false;
        if (!s.All(char.IsLetterOrDigit)) return false;

        var preusporiadany = s[4..] + s[..4];
        var cisla = new StringBuilder();
        foreach (var c in preusporiadany)
            cisla.Append(char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString());
        return BigInteger.Parse(cisla.ToString()) % 97 == 1;
    }

    /// <summary>Formát po štvoriciach: SK31 1200 0000 …</summary>
    public static string Formatuj(string? iban)
    {
        var s = Normalizuj(iban);
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }
}
