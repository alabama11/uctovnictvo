using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>Jedna prichádzajúca platba z bankového výpisu.</summary>
public record PlatbaZVypisu(DateOnly Datum, decimal Suma, string? Iban, string? Meno, string? VariabilnySymbol, string? Sprava);

public record ParovanaPlatba(PlatbaZVypisu Platba, Priestor? Priestor, string Dovod);

/// <summary>
/// Import výpisu vo formáte ISO 20022 camt.053 (XML), ktorý ponúkajú slovenské banky v internet bankingu.
/// Načítajú sa len prijaté platby (CRDT).
/// </summary>
public static partial class ImportBanky
{
    public static IReadOnlyList<PlatbaZVypisu> NacitajCamt053(string path) => NacitajCamt053(XDocument.Load(path));

    public static IReadOnlyList<PlatbaZVypisu> NacitajCamt053(XDocument doc)
    {
        var vysledok = new List<PlatbaZVypisu>();
        foreach (var ntry in doc.Descendants().Where(e => e.Name.LocalName == "Ntry"))
        {
            if (Text(ntry, "CdtDbtInd") != "CRDT") continue;
            if (Text(ntry, "RvslInd") == "true") continue;

            var suma = decimal.Parse(Text(ntry, "Amt") ?? "0", CultureInfo.InvariantCulture);
            var datumText = Text(Prve(ntry, "BookgDt"), "Dt") ?? Text(Prve(ntry, "BookgDt"), "DtTm")
                         ?? Text(Prve(ntry, "ValDt"), "Dt") ?? "";
            var datum = DateOnly.FromDateTime(DateTime.Parse(datumText, CultureInfo.InvariantCulture));

            var dbtrAcct = Prve(ntry, "DbtrAcct");
            var iban = Text(dbtrAcct, "IBAN");
            var meno = Text(Prve(ntry, "Dbtr"), "Nm");
            var e2e = Text(ntry, "EndToEndId");
            var sprava = string.Join(" ", Vsetky(ntry, "Ustrd").Select(e => e.Value.Trim()));
            var ref_ = Text(Prve(ntry, "CdtrRefInf"), "Ref");

            vysledok.Add(new PlatbaZVypisu(datum, suma, iban is null ? null : Iban.Normalizuj(iban), meno,
                NajdiVs(e2e) ?? NajdiVs(ref_) ?? NajdiVs(sprava), string.IsNullOrWhiteSpace(sprava) ? null : sprava));
        }
        return vysledok;
    }

    /// <summary>Priradí platby k priestorom: najprv podľa variabilného symbolu, potom podľa IBAN vlastníka.</summary>
    public static IReadOnlyList<ParovanaPlatba> Paruj(IReadOnlyList<PlatbaZVypisu> platby, IReadOnlyList<Priestor> priestory)
    {
        var podlaVs = priestory.Where(p => !string.IsNullOrWhiteSpace(p.VariabilnySymbol))
            .GroupBy(p => p.VariabilnySymbol!.TrimStart('0'))
            .ToDictionary(g => g.Key, g => g.ToList());
        var podlaIban = priestory
            .SelectMany(p => p.Vlastnici.SelectMany(v => v.Ucty).Select(u => (Iban: Iban.Normalizuj(u.Iban), p)))
            .GroupBy(x => x.Iban)
            .ToDictionary(g => g.Key, g => g.Select(x => x.p).Distinct().ToList());

        return platby.Select(pl =>
        {
            if (pl.VariabilnySymbol is { } vs && podlaVs.TryGetValue(vs.TrimStart('0'), out var vsZhoda) && vsZhoda.Count == 1)
                return new ParovanaPlatba(pl, vsZhoda[0], $"VS {vs}");
            if (pl.Iban is { } iban && podlaIban.TryGetValue(iban, out var ibanZhoda))
            {
                if (ibanZhoda.Count == 1)
                    return new ParovanaPlatba(pl, ibanZhoda[0], "IBAN vlastníka");
                return new ParovanaPlatba(pl, null, $"IBAN patrí viacerým priestorom ({string.Join(", ", ibanZhoda.Select(p => p.Cislo))})");
            }
            return new ParovanaPlatba(pl, null, "nenašla sa zhoda");
        }).ToList();
    }

    public static string? NajdiVs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = VsRegex().Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"VS\s*[:/]?\s*(\d{1,10})", RegexOptions.IgnoreCase)]
    private static partial Regex VsRegex();

    static XElement? Prve(XElement? e, string name) => e?.Descendants().FirstOrDefault(x => x.Name.LocalName == name);
    static IEnumerable<XElement> Vsetky(XElement e, string name) => e.Descendants().Where(x => x.Name.LocalName == name);
    static string? Text(XElement? e, string name) => Prve(e, name)?.Value.Trim();
}
