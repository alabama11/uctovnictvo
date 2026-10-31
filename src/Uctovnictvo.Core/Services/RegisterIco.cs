using System.Net.Http;
using System.Text.Json;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Údaje firmy nájdené v registri.</summary>
public record UdajeFirmy(string Nazov, string? Ico, string? Ulica, string? Obec, string? Psc, string? Stat);

/// <summary>
/// Vyhľadanie firmy podľa IČO v Registri právnických osôb, podnikateľov a orgánov verejnej moci (RPO),
/// ktorý prevádzkuje Štatistický úrad SR. Verejné API: https://api.statistics.sk/rpo/v1/search?identifier=IČO
/// (obsahuje aj živnostníkov). DIČ v registri nie je.
/// </summary>
public static class RegisterIco
{
    public const string Adresa = "https://api.statistics.sk/rpo/v1/search?identifier=";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>IČO má 8 číslic (staršie 6, doplnia sa nuly zľava).</summary>
    public static string? Normalizuj(string? ico)
    {
        var c = new string((ico ?? "").Where(char.IsDigit).ToArray());
        if (c.Length is < 6 or > 8) return null;
        return c.PadLeft(8, '0');
    }

    public static async Task<UdajeFirmy?> Vyhladaj(string ico, CancellationToken ct = default)
    {
        var n = Normalizuj(ico) ?? throw new ArgumentException("IČO musí mať 6 až 8 číslic.");
        using var req = new HttpRequestMessage(HttpMethod.Get, Adresa + n);
        req.Headers.Accept.ParseAdd("application/json");
        using var resp = await Http.SendAsync(req, ct);
        if ((int)resp.StatusCode == 404) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return Precitaj(json, n);
    }

    /// <summary>Spracuje odpoveď RPO: vyberie záznam s daným IČO a jeho aktuálny názov a adresu (bez dátumu ukončenia).</summary>
    public static UdajeFirmy? Precitaj(string json, string? ico = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var zaznamy = root.ValueKind == JsonValueKind.Array ? root
            : root.TryGetProperty("results", out var r) ? r : default;
        if (zaznamy.ValueKind != JsonValueKind.Array) return null;

        JsonElement? vybrany = null;
        foreach (var z in zaznamy.EnumerateArray())
        {
            var icoZaznamu = Aktualna(z, "identifiers") is { } id ? Retazec(id, "value") : null;
            var zaniknuty = z.TryGetProperty("termination", out var t) && t.ValueKind == JsonValueKind.String;
            if (ico is null || Normalizuj(icoZaznamu) == ico)
            {
                if (vybrany is null || !zaniknuty) vybrany = z;
                if (!zaniknuty) break;
            }
        }
        if (vybrany is not { } v) return null;

        var nazov = Aktualna(v, "fullNames") is { } fn ? Retazec(fn, "value") : null;
        if (string.IsNullOrWhiteSpace(nazov)) return null;
        var icoV = Aktualna(v, "identifiers") is { } i ? Retazec(i, "value") : ico;

        string? ulica = null, obec = null, psc = null, stat = null;
        if (Aktualna(v, "addresses") is { } a)
        {
            var nazovUlice = Retazec(a, "street");
            var supisne = a.TryGetProperty("regNumber", out var rn) && rn.ValueKind == JsonValueKind.Number && rn.GetInt64() > 0 ? rn.GetInt64().ToString() : null;
            var orientacne = Retazec(a, "buildingNumber");
            var cislo = supisne is not null && orientacne is not null ? $"{supisne}/{orientacne}" : supisne ?? orientacne;
            obec = a.TryGetProperty("municipality", out var m) ? Retazec(m, "value") : null;
            ulica = string.Join(" ", new[] { nazovUlice ?? (cislo is null ? null : obec), cislo }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (ulica.Length == 0) ulica = null;
            if (a.TryGetProperty("postalCodes", out var pc) && pc.ValueKind == JsonValueKind.Array && pc.GetArrayLength() > 0)
                psc = FormatPsc(pc[0].GetString());
            stat = a.TryGetProperty("country", out var c) ? Retazec(c, "value") : null;
            if (stat is "Slovenská republika" or "Slovensko") stat = null;
        }
        return new UdajeFirmy(nazov.Trim(), Normalizuj(icoV), ulica, obec, psc, stat);
    }

    public static void Dopln(Partner p, UdajeFirmy u)
    {
        p.Nazov = u.Nazov;
        if (u.Ico is not null) p.Ico = u.Ico;
        p.Ulica = u.Ulica;
        p.Obec = u.Obec;
        p.Psc = u.Psc;
        p.Stat = u.Stat;
    }

    static string? FormatPsc(string? psc)
    {
        var c = new string((psc ?? "").Where(char.IsDigit).ToArray());
        return c.Length == 5 ? $"{c[..3]} {c[3..]}" : psc;
    }

    /// <summary>Z poľa historických hodnôt vyberie platnú (bez validTo), inak tú s najneskorším validFrom.</summary>
    static JsonElement? Aktualna(JsonElement z, string pole)
    {
        if (!z.TryGetProperty(pole, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
        return arr.EnumerateArray()
            .OrderBy(x => x.TryGetProperty("validTo", out var vt) && vt.ValueKind == JsonValueKind.String ? 1 : 0)
            .ThenByDescending(x => Retazec(x, "validFrom") ?? "", StringComparer.Ordinal)
            .First();
    }

    static string? Retazec(JsonElement e, string nazov) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nazov, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;
}
