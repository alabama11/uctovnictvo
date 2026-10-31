using System.Globalization;
using System.IO.Hashing;
using System.Text;
using QRCoder;
using SharpCompress.Compressors.LZMA;

namespace Uctovnictvo.Core.Services;

/// <summary>Údaje platby pre QR kód PAY by square (štandard Slovenskej bankovej asociácie).</summary>
public record PlatbaQr(
    string Iban,
    decimal Suma,
    string? VariabilnySymbol = null,
    string? KonstantnySymbol = null,
    string? SpecifickySymbol = null,
    string? Poznamka = null,
    DateOnly? Splatnost = null,
    string? Prijemca = null);

/// <summary>
/// Kódovanie PAY by square verzie 1.0.0: údaje oddelené tabulátorom, CRC32, LZMA (lc=3, lp=0, pb=2, slovník 2^17),
/// 4-bajtová hlavička a base32hex. Postup zodpovedá referenčnej knižnici bysquare (xseman/bysquare).
/// </summary>
public static class PayBySquare
{
    const string Base32Hex = "0123456789ABCDEFGHIJKLMNOPQRSTUV";

    public static string Zakoduj(PlatbaQr p)
    {
        var polia = new List<string?>
        {
            null,                                   // invoiceId
            "1",                                    // počet platieb
            "1",                                    // typ: platobný príkaz
            p.Suma.ToString("0.##", CultureInfo.InvariantCulture),
            "EUR",
            p.Splatnost?.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            Cislice(p.VariabilnySymbol, 10),
            Cislice(p.KonstantnySymbol, 4),
            Cislice(p.SpecifickySymbol, 10),
            null,                                   // originator's reference
            BezDiakritiky(p.Poznamka, 140),
            "1",                                    // počet účtov
            Iban.Normalizuj(p.Iban),
            null,                                   // BIC
            "0",                                    // trvalý príkaz – nie
            "0",                                    // inkaso – nie
            BezDiakritiky(p.Prijemca, 70),
            null,
            null,
        };
        var text = string.Join('\t', polia.Select(x => x ?? ""));
        var data = Encoding.UTF8.GetBytes(text);

        var sKontrolou = new byte[4 + data.Length];
        BitConverter.GetBytes(Crc32.HashToUInt32(data)).CopyTo(sKontrolou, 0);  // little-endian
        data.CopyTo(sKontrolou, 4);
        if (!BitConverter.IsLittleEndian) Array.Reverse(sKontrolou, 0, 4);

        var komprimovane = Lzma(sKontrolou);
        var vystup = new byte[4 + komprimovane.Length];
        vystup[0] = 0x00;                                           // typ 0 (PAY), verzia 1.0.0
        vystup[1] = 0x00;                                           // dokument 0, rezerva
        vystup[2] = (byte)(sKontrolou.Length & 0xFF);
        vystup[3] = (byte)(sKontrolou.Length >> 8);
        komprimovane.CopyTo(vystup, 4);
        return Base32(vystup);
    }

    /// <summary>QR kód ako PNG (na vloženie do PDF).</summary>
    public static byte[] QrPng(PlatbaQr p, int pixelovNaModul = 10)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(Zakoduj(p), QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelovNaModul);
    }

    static byte[] Lzma(byte[] vstup)
    {
        var vlastnosti = new LzmaEncoderProperties(eos: false, dictionary: 1 << 17, numFastBytes: 273);
        using var ms = new MemoryStream();
        using (var lz = new LzmaStream(vlastnosti, false, ms))
            lz.Write(vstup, 0, vstup.Length);
        return ms.ToArray();
    }

    static string Base32(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bity = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bity += 8;
            while (bity >= 5)
            {
                sb.Append(Base32Hex[(buffer >> (bity - 5)) & 31]);
                bity -= 5;
            }
        }
        if (bity > 0) sb.Append(Base32Hex[(buffer << (5 - bity)) & 31]);
        return sb.ToString();
    }

    static string? Cislice(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var c = new string(s.Where(char.IsDigit).ToArray());
        return c.Length == 0 ? null : c.Length > max ? c[..max] : c;
    }

    /// <summary>Banky odporúčajú text bez diakritiky – inak niektoré aplikácie zobrazia otázniky.</summary>
    public static string? BezDiakritiky(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && ch != '\t')
                sb.Append(ch);
        var t = sb.ToString().Normalize(NormalizationForm.FormC).Trim();
        return t.Length > max ? t[..max] : t;
    }
}
