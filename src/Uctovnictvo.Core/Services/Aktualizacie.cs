using System.Net.Http.Headers;
using System.Text.Json;

namespace Uctovnictvo.Core.Services;

public record NovaVerzia(Version Verzia, string Adresa);

/// <summary>Kontrola novej verzie podľa posledného vydania na GitHube (ručne z menu, nič sa nesťahuje automaticky).</summary>
public static class Aktualizacie
{
    public const string Repozitar = "alabama11/uctovnictvo";
    public static string StrankaVydani => $"https://github.com/{Repozitar}/releases";

    /// <summary>Z JSON odpovede GitHub API (releases/latest) vyberie verziu z tagu „v0.8.0“.</summary>
    public static NovaVerzia? Precitaj(string json)
    {
        using var d = JsonDocument.Parse(json);
        var tag = d.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        var url = d.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
        if (tag is null || !Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
        return new NovaVerzia(v, url ?? StrankaVydani);
    }

    public static bool JeNovsia(Version aktualna, Version najnovsia) =>
        new Version(najnovsia.Major, najnovsia.Minor, Math.Max(0, najnovsia.Build)) > new Version(aktualna.Major, aktualna.Minor, Math.Max(0, aktualna.Build));

    public static async Task<NovaVerzia?> Zisti(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Uctovnictvo", "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repozitar}/releases/latest", ct);
        return Precitaj(json);
    }
}
