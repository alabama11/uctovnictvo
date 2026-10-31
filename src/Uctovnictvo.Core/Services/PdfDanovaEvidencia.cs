using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Uctovnictvo.Core.Model;
using static Uctovnictvo.Core.Services.PdfVyuctovanie;

namespace Uctovnictvo.Core.Services;

/// <summary>Tlač daňovej evidencie (súhrn, peňažný denník, majetok, pohľadávky a záväzky) a knihy jázd.</summary>
public static class PdfDanovaEvidencia
{
    static PdfDanovaEvidencia() => QuestPDF.Settings.License = LicenseType.Community;

    static void Strana(PageDescriptor page, bool nalezato = false)
    {
        page.Size(nalezato ? PageSizes.A4.Landscape() : PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily("Arial", "Lato"));
        page.Footer().AlignCenter().Text(t => { t.DefaultTextStyle(s => s.FontSize(7.5f)); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
    }

    static void Hlava(ColumnDescriptor col, UctovnaJednotka j, string titulok)
    {
        col.Item().Text(titulok).FontSize(15).Bold();
        col.Item().Text($"{j.Nazov}{(string.IsNullOrWhiteSpace(j.Ico) ? "" : $", IČO {j.Ico}")}{(string.IsNullOrWhiteSpace(j.Dic) ? "" : $", DIČ {j.Dic}")}");
        col.Item().PaddingBottom(4).LineHorizontal(0.5f);
    }

    static void Tabulka(ColumnDescriptor col, string nadpis, (string Nazov, float Sirka, bool Cislo)[] stlpce, IEnumerable<string[]> riadky, string[]? spolu = null)
    {
        col.Item().PaddingTop(8).Text(nadpis).Bold().FontSize(10.5f);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { foreach (var s in stlpce) if (s.Sirka == 0) cd.RelativeColumn(); else cd.ConstantColumn(s.Sirka); });
            t.Header(h => { foreach (var s in stlpce) { var c = h.Cell().Element(Hlavicka); (s.Cislo ? c.AlignRight() : c).Text(s.Nazov).Bold(); } });
            foreach (var r in riadky)
                for (int i = 0; i < stlpce.Length; i++) { var c = t.Cell().Element(Bunka); (stlpce[i].Cislo ? c.AlignRight() : c).Text(r[i]); }
            if (spolu is not null)
                for (int i = 0; i < stlpce.Length; i++) { var c = t.Cell().Element(Bunka); (stlpce[i].Cislo ? c.AlignRight() : c).Text(spolu[i]).Bold(); }
        });
    }

    public static byte[] Vytvor(UctovnaJednotka j, SuhrnDanovejEvidencie s, IReadOnlyList<RiadokDennika> dennik, IReadOnlyList<Majetok> majetok) =>
        Document.Create(doc =>
        {
            var koniec = new DateOnly(s.Rok, 12, 31);
            doc.Page(page =>
            {
                Strana(page);
                page.Content().Column(col =>
                {
                    Hlava(col, j, $"Daňová evidencia {s.Rok}");
                    col.Item().Text("Evidencia podľa § 6 ods. 11 zákona č. 595/2003 Z. z. o dani z príjmov (príjmy a výdavky v čase zaplatenia).").FontSize(7.5f);
                    col.Item().PaddingTop(8).Width(330).Column(c =>
                    {
                        Riadok(c, "Daňové príjmy", Eur(s.DanovePrijmy));
                        Riadok(c, "Daňové výdavky – zaplatené", Eur(s.DanoveVydavkyPeniazne));
                        Riadok(c, "Daňové odpisy majetku", Eur(s.Odpisy));
                        Riadok(c, "Cestovné náhrady (kniha jázd)", Eur(s.CestovneNahrady));
                        Riadok(c, "Daňové výdavky spolu", Eur(s.DanoveVydavky), true);
                        c.Item().PaddingTop(3).Background("#EEF2F6").Padding(4).Row(r =>
                        {
                            r.RelativeItem().Text("Rozdiel príjmov a výdavkov").Bold();
                            r.AutoItem().Text(Eur(s.Vysledok)).Bold();
                        });
                        Riadok(c, "Nedaňové výdavky (pre informáciu)", Eur(s.NedanoveVydavky));
                        if (s.Nezaradene != 0) Riadok(c, "⚠ Nezaradené pohyby (doplňte kategóriu)", Eur(s.Nezaradene), true);
                    });
                    Tabulka(col, "Príjmy a výdavky podľa kategórií", new[] { ("Kategória", 0f, false), ("Druh", 90f, false), ("Suma", 90f, true) },
                        s.Kategorie.Select(k => new[] { k.Nazov, k.Druh switch { DruhKategorie.Prijem => "príjem", DruhKategorie.Vydavok => k.Danovy ? "výdavok" : "nedaňový výdavok", _ => "neutrálny" }, Eur(k.Suma) }));

                    Tabulka(col, "Hmotný a nehmotný majetok",
                        new[] { ("Inv. č.", 54f, false), ("Názov", 0f, false), ("Zaradený", 56f, false), ("Sk.", 24f, true), ("Vstupná cena", 72f, true), ($"Odpis {s.Rok}", 66f, true), ("Uplatnený", 66f, true), ($"ZC 31.12.", 70f, true) },
                        majetok.Where(m => m.DatumZaradenia.Year <= s.Rok && (m.DatumVyradenia is null || m.DatumVyradenia.Value.Year >= s.Rok)).OrderBy(m => m.InventarneCislo).Select(m =>
                        {
                            var o = Odpisy.ZaRok(m, s.Rok);
                            return new[] { m.InventarneCislo, m.Nazov, Datum(m.DatumZaradenia), m.OdpisovaSkupina.ToString(), Eur(m.VstupnaCena),
                                Eur(o?.Odpis ?? 0), Eur(o?.UplatnenyOdpis ?? 0), Eur(Odpisy.ZostatkovaCena(m, s.Rok)) };
                        }),
                        new[] { "", "Spolu", "", "", "", "", Eur(s.Odpisy), "" });

                    Tabulka(col, $"Pohľadávky k 31. 12. {s.Rok}", new[] { ("Faktúra", 80f, false), ("Odberateľ", 0f, false), ("Splatná", 60f, false), ("Zostáva", 80f, true) },
                        s.Pohladavky.Select(f => new[] { f.Cislo, f.PartnerNazov, Datum(f.DatumSplatnosti), Eur(DanovaEvidencia.ZostatokK(f, koniec)) }),
                        new[] { "", "Spolu", "", Eur(s.Pohladavky.Sum(f => DanovaEvidencia.ZostatokK(f, koniec))) });
                    Tabulka(col, $"Záväzky k 31. 12. {s.Rok}", new[] { ("Doklad", 80f, false), ("Dodávateľ", 0f, false), ("Splatná", 60f, false), ("Zostáva", 80f, true) },
                        s.Zavazky.Select(f => new[] { f.CisloDodavatela ?? f.Cislo, f.PartnerNazov, Datum(f.DatumSplatnosti), Eur(DanovaEvidencia.ZostatokK(f, koniec)) }),
                        new[] { "", "Spolu", "", Eur(s.Zavazky.Sum(f => DanovaEvidencia.ZostatokK(f, koniec))) });
                });
            });
            doc.Page(page =>
            {
                Strana(page, true);
                page.Content().Column(col =>
                {
                    Hlava(col, j, $"Peňažný denník {s.Rok}");
                    var zostatok = 0m;
                    Tabulka(col, "Pohyby na účte a v pokladni",
                        new[] { ("Dátum", 56f, false), ("Doklad", 70f, false), ("Popis", 0f, false), ("Kategória", 170f, false), ("Príjem", 70f, true), ("Výdavok", 70f, true), ("Pohyb spolu", 76f, true) },
                        dennik.Select(r =>
                        {
                            zostatok += r.Suma;
                            return new[] { Datum(r.Datum), r.Doklad, r.Popis, r.Kategoria?.Nazov ?? "⚠ nezaradené", r.Prijem == 0 ? "" : Eur(r.Prijem), r.Vydavok == 0 ? "" : Eur(r.Vydavok), Eur(zostatok) };
                        }),
                        new[] { "", "", "Spolu", "", Eur(dennik.Sum(r => r.Prijem)), Eur(dennik.Sum(r => r.Vydavok)), Eur(dennik.Sum(r => r.Suma)) });
                });
            });
        }).WithMetadata(new DocumentMetadata { Title = $"Daňová evidencia {s.Rok}", Creator = "Účtovníctvo" }).GeneratePdf();

    public static byte[] KnihaJazdPdf(UctovnaJednotka j, Vozidlo v, int rok, IReadOnlyList<SadzbaKm> sadzby) =>
        Document.Create(doc => doc.Page(page =>
        {
            Strana(page, true);
            page.Content().Column(col =>
            {
                Hlava(col, j, $"Kniha jázd {rok} – {v.Nazov}{(string.IsNullOrWhiteSpace(v.Ecv) ? "" : $" ({v.Ecv})")}");
                col.Item().Text($"Spotreba podľa TP: {v.Spotreba:0.##} l/100 km, palivo: {v.Palivo ?? "-"}" +
                                (v.VObchodnomMajetku ? ", vozidlo je v obchodnom majetku (náhrady sa neuplatňujú)." : ", vlastné vozidlo mimo obchodného majetku – náhrady podľa zákona o cestovných náhradách."));
                var tach = v.StavTachometraZaciatok + v.Jazdy.Where(x => x.Datum.Year < rok).Sum(x => x.Km);
                Tabulka(col, "Jazdy",
                    new[] { ("Dátum", 56f, false), ("Odkiaľ", 110f, false), ("Kam", 110f, false), ("Účel", 0f, false), ("Vodič", 80f, false), ("Km", 44f, true), ("Tachometer", 66f, true), ("Zákl. náhrada", 70f, true), ("PHM", 60f, true) },
                    v.Jazdy.Where(x => x.Datum.Year == rok).OrderBy(x => x.Datum).ThenBy(x => x.Id).Select(x =>
                    {
                        tach += x.Km;
                        var (z, p, _) = KnihaJazd.Nahrada(x, v, sadzby);
                        return new[] { Datum(x.Datum), x.Odkial, x.Kam, x.Sukromna ? "súkromná jazda" : x.Ucel ?? "", x.Vodic ?? "", x.Km.ToString("0.#"), tach.ToString("0"), z == 0 ? "" : Eur(z), p == 0 ? "" : Eur(p) };
                    }));
                var mes = KnihaJazd.Prehlad(v, rok, sadzby);
                Tabulka(col, "Súhrn po mesiacoch", new[] { ("Mesiac", 60f, false), ("Služobné km", 80f, true), ("Súkromné km", 80f, true), ("Základná náhrada", 100f, true), ("Náhrada PHM", 90f, true), ("Spolu", 90f, true) },
                    mes.Where(m => m.KmSluzobne + m.KmSukromne > 0).Select(m => new[] { m.Mesiac.ToString(), m.KmSluzobne.ToString("0.#"), m.KmSukromne.ToString("0.#"), Eur(m.ZakladnaNahrada), Eur(m.NahradaPhm), Eur(m.Spolu) }),
                    new[] { "Spolu", mes.Sum(m => m.KmSluzobne).ToString("0.#"), mes.Sum(m => m.KmSukromne).ToString("0.#"), Eur(mes.Sum(m => m.ZakladnaNahrada)), Eur(mes.Sum(m => m.NahradaPhm)), Eur(mes.Sum(m => m.Spolu)) });
            });
        })).WithMetadata(new DocumentMetadata { Title = $"Kniha jázd {rok}", Creator = "Účtovníctvo" }).GeneratePdf();
}
