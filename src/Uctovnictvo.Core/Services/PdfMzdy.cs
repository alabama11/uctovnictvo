using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Uctovnictvo.Core.Model;
using static Uctovnictvo.Core.Services.PdfVyuctovanie;

namespace Uctovnictvo.Core.Services;

/// <summary>Výplatné pásky zamestnancov za mesiac a rekapitulácia odvodov.</summary>
public static class PdfMzdy
{
    static PdfMzdy() => QuestPDF.Settings.License = LicenseType.Community;

    static void Riadok(ColumnDescriptor c, string text, decimal suma, bool tucne = false)
    {
        c.Item().Row(r =>
        {
            var t = r.RelativeItem().Text(text); if (tucne) t.Bold();
            var s = r.ConstantItem(90).AlignRight().Text(Eur(suma)); if (tucne) s.Bold();
        });
    }

    public static byte[] VyplatnePasky(UctovnaJednotka j, ParametreMzdy p, IReadOnlyList<MzdaMesiac> mzdy, RekapitulaciaMzdy rekap) =>
        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial", "Lato"));
                page.Content().Column(col =>
                {
                    foreach (var m in mzdy)
                    {
                        var z = m.Zamestnanec!;
                        var v = Mzdy.Vypocitaj(p, z, m.HrubaMzda);
                        col.Item().PaddingBottom(14).Border(0.5f).BorderColor("#9AA5B1").Padding(10).Column(c =>
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"Výplatná páska {m.Mesiac:00}/{m.Rok}").FontSize(13).Bold();
                                r.AutoItem().Text(j.Nazov);
                            });
                            c.Item().Text($"{z.Meno}{(string.IsNullOrWhiteSpace(z.Funkcia) ? "" : $", {z.Funkcia}")}{(string.IsNullOrWhiteSpace(z.ZdravotnaPoistovna) ? "" : $" · ZP: {z.ZdravotnaPoistovna}")}");
                            c.Item().PaddingVertical(4).LineHorizontal(0.5f);
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Column(l =>
                                {
                                    Riadok(l, "Hrubá mzda", m.HrubaMzda, true);
                                    foreach (var f in v.Fondy.Where(f => f.Zamestnanec != 0)) Riadok(l, "  " + f.Fond, f.Zamestnanec);
                                    Riadok(l, "  Zdravotné poistenie", m.ZpZamestnanec);
                                    Riadok(l, "Poistné spolu", m.SpZamestnanec + m.ZpZamestnanec);
                                    Riadok(l, "Nezdaniteľná časť", m.Nczd);
                                    Riadok(l, "Zdaniteľná mzda po nezdaniteľnej časti", m.ZakladDane);
                                    Riadok(l, "Preddavok na daň", m.Preddavok);
                                    if (m.DanovyBonus != 0) Riadok(l, "Daňový bonus", m.DanovyBonus);
                                    l.Item().PaddingTop(3).Background("#EEF2F6").Padding(3).Row(r2 =>
                                    {
                                        r2.RelativeItem().Text("Čistá mzda na výplatu").Bold();
                                        r2.ConstantItem(90).AlignRight().Text(Eur(m.CistaMzda)).Bold();
                                    });
                                    if (!string.IsNullOrWhiteSpace(z.Iban)) l.Item().PaddingTop(2).Text($"Na účet: {z.Iban}").FontSize(8);
                                });
                                r.ConstantItem(20);
                                r.RelativeItem().Column(rr =>
                                {
                                    rr.Item().Text("Odvody zamestnávateľa").Bold();
                                    foreach (var f in v.Fondy.Where(f => f.Zamestnavatel != 0)) Riadok(rr, "  " + f.Fond, f.Zamestnavatel);
                                    Riadok(rr, "  Zdravotné poistenie", m.ZpZamestnavatel);
                                    Riadok(rr, "Spolu", m.SpZamestnavatel + m.ZpZamestnavatel, true);
                                    Riadok(rr, "Celková cena práce", m.CenaPrace, true);
                                    if (!string.IsNullOrWhiteSpace(m.Poznamka)) rr.Item().PaddingTop(4).Text(m.Poznamka).Italic();
                                });
                            });
                        });
                    }
                    col.Item().PaddingTop(6).Text($"Rekapitulácia {rekap.Mesiac:00}/{rekap.Rok}").FontSize(12).Bold();
                    col.Item().Width(330).Column(c =>
                    {
                        Riadok(c, "Hrubé mzdy spolu", rekap.Hruba);
                        Riadok(c, "Čisté mzdy na výplatu", rekap.NaVyplatu, true);
                        Riadok(c, "Sociálna poisťovňa (zamestnanci + zamestnávateľ)", rekap.SocialnaPoistovna, true);
                        foreach (var (poistovna, suma) in rekap.Zdravotne) Riadok(c, $"Zdravotná poisťovňa {poistovna}", suma, true);
                        Riadok(c, "Daňový úrad – preddavky na daň (po odpočte bonusu)", rekap.PreddavkyNaDan, true);
                        Riadok(c, "Celková cena práce", rekap.CenaPrace);
                    });
                    col.Item().PaddingTop(6).Text("Výpočet pre pracovný pomer so sadzbami poistného platnými v danom roku. Mesačné výkazy pre Sociálnu poisťovňu, zdravotné poisťovne " +
                                                  "a prehľad pre daňový úrad podajte v ich elektronických službách.").FontSize(7.5f);
                });
            });
        }).WithMetadata(new DocumentMetadata { Title = $"Výplatné pásky {rekap.Mesiac:00}/{rekap.Rok}", Creator = "Účtovníctvo" }).GeneratePdf();
}
