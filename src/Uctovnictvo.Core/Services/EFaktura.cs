using System.Globalization;
using System.Xml.Linq;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Services;

/// <summary>
/// Načítanie prijatej elektronickej faktúry vo formáte UBL 2.1 (Invoice alebo CreditNote, Peppol BIS Billing 3.0 / EN 16931).
/// Od 1. 1. 2027 musí každá zdaniteľná osoba vrátane neplatiteľov DPH vedieť e-faktúru prijať cez doručovaciu službu;
/// XML, ktoré služba doručí, sa tu načíta ako prijatá faktúra.
/// Neplatiteľ si DPH neodpočítava, preto je nákladom celá suma s DPH – DPH sa pridá ako samostatná položka.
/// </summary>
public static class EFaktura
{
    static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public record Vysledok(Faktura Faktura, UdajeFirmy Dodavatel, string? Iban, string? Mena, List<string> Upozornenia);

    public static Vysledok Nacitaj(string path) => Nacitaj(XDocument.Load(path));

    public static Vysledok Nacitaj(XDocument doc)
    {
        var root = doc.Root ?? throw new FormatException("Prázdny súbor.");
        var dobropis = root.Name.LocalName == "CreditNote";
        if (root.Name.LocalName is not ("Invoice" or "CreditNote"))
            throw new FormatException("Súbor nie je e-faktúra vo formáte UBL (očakáva sa koreňový prvok Invoice alebo CreditNote).");
        var upozornenia = new List<string>();
        var znamienko = dobropis ? -1m : 1m;

        var mena = root.Element(Cbc + "DocumentCurrencyCode")?.Value.Trim();
        if (mena is not null && mena != "EUR") upozornenia.Add($"Faktúra je v mene {mena}, program počíta v eurách – sumy skontrolujte.");

        var dodavatel = root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party");
        var nazov = Hodnota(dodavatel, Cac + "PartyLegalEntity", Cbc + "RegistrationName")
                    ?? Hodnota(dodavatel, Cac + "PartyName", Cbc + "Name") ?? "Neznámy dodávateľ";
        var ico = dodavatel?.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "CompanyID")?.Value.Trim()
                  ?? dodavatel?.Elements(Cac + "PartyIdentification").Select(e => e.Element(Cbc + "ID")?.Value.Trim()).FirstOrDefault(x => RegisterIco.Normalizuj(x) is not null);
        var icDph = dodavatel?.Elements(Cac + "PartyTaxScheme").Select(e => e.Element(Cbc + "CompanyID")?.Value.Trim()).FirstOrDefault();
        var adr = dodavatel?.Element(Cac + "PostalAddress");
        var ulica = string.Join(" ", new[] { adr?.Element(Cbc + "StreetName")?.Value, adr?.Element(Cbc + "BuildingNumber")?.Value }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var stat = adr?.Element(Cac + "Country")?.Element(Cbc + "IdentificationCode")?.Value.Trim();
        var udaje = new UdajeFirmy(nazov.Trim(), RegisterIco.Normalizuj(ico) is { } i && ico!.Length <= 8 ? i : null,
            ulica.Length > 0 ? ulica : null, adr?.Element(Cbc + "CityName")?.Value.Trim(), adr?.Element(Cbc + "PostalZone")?.Value.Trim(),
            stat is null or "SK" ? null : stat);

        var platba = root.Elements(Cac + "PaymentMeans").FirstOrDefault();
        var iban = platba?.Element(Cac + "PayeeFinancialAccount")?.Element(Cbc + "ID")?.Value.Trim();
        var paymentId = platba?.Element(Cbc + "PaymentID")?.Value.Trim();
        var vs = ImportBanky.NajdiVs(paymentId) ?? (paymentId is { Length: > 0 and <= 10 } && paymentId.All(char.IsDigit) ? paymentId : null);

        var vystavena = Datum(root.Element(Cbc + "IssueDate")) ?? DateOnly.FromDateTime(DateTime.Today);
        var splatnost = Datum(root.Element(Cbc + "DueDate"))
                        ?? Datum(root.Element(Cac + "PaymentTerms")?.Element(Cbc + "PaymentDueDate"))
                        ?? Datum(platba?.Element(Cbc + "PaymentDueDate"))
                        ?? vystavena.AddDays(14);
        var dodanie = Datum(root.Element(Cac + "Delivery")?.Element(Cbc + "ActualDeliveryDate"))
                      ?? Datum(root.Element(Cbc + "TaxPointDate"));

        var f = new Faktura
        {
            Druh = DruhFaktury.Prijata,
            Typ = dobropis ? TypFaktury.Dobropis : TypFaktury.Faktura,
            CisloDodavatela = root.Element(Cbc + "ID")?.Value.Trim(),
            VariabilnySymbol = vs ?? Faktury.VsZCisla(root.Element(Cbc + "ID")?.Value ?? ""),
            PartnerNazov = udaje.Nazov,
            PartnerAdresa = string.Join(", ", new[] { udaje.Ulica, string.Join(" ", new[] { udaje.Psc, udaje.Obec }.Where(x => !string.IsNullOrWhiteSpace(x))), udaje.Stat }.Where(x => !string.IsNullOrWhiteSpace(x))),
            PartnerIco = udaje.Ico,
            PartnerIcDph = icDph,
            PartnerIban = iban is null ? null : Iban.Normalizuj(iban),
            DatumVystavenia = vystavena,
            DatumDodania = dodanie ?? vystavena,
            DatumSplatnosti = splatnost,
            FormaUhrady = FormaUhrady.Prevod,
            PovodnaFaktura = root.Element(Cac + "BillingReference")?.Element(Cac + "InvoiceDocumentReference")?.Element(Cbc + "ID")?.Value.Trim(),
            TextNad = root.Element(Cbc + "Note")?.Value.Trim(),
        };

        var riadky = root.Elements(Cac + (dobropis ? "CreditNoteLine" : "InvoiceLine")).ToList();
        int poradie = 1;
        foreach (var r in riadky)
        {
            var mnozstvoEl = r.Element(Cbc + (dobropis ? "CreditedQuantity" : "InvoicedQuantity"));
            var mnozstvo = Cislo(mnozstvoEl) ?? 1m;
            var spolu = Cislo(r.Element(Cbc + "LineExtensionAmount")) ?? 0m;
            var item = r.Element(Cac + "Item");
            var popis = item?.Element(Cbc + "Name")?.Value.Trim() ?? item?.Element(Cbc + "Description")?.Value.Trim() ?? "Položka";
            decimal cena;
            if (mnozstvo != 0 && Math.Round(mnozstvo * Math.Round(spolu / mnozstvo, 4), 2) == spolu)
                cena = Math.Round(spolu / mnozstvo, 4);
            else { cena = spolu; mnozstvo = 1; }
            f.Polozky.Add(new PolozkaFaktury
            {
                Poradie = poradie++,
                Popis = popis,
                Mnozstvo = mnozstvo,
                Jednotka = Jednotka(mnozstvoEl?.Attribute("unitCode")?.Value),
                CenaZaJednotku = cena * znamienko,
            });
        }

        // Zľavy a prirážky na úrovni dokladu
        foreach (var ac in root.Elements(Cac + "AllowanceCharge"))
        {
            var suma = Cislo(ac.Element(Cbc + "Amount")) ?? 0m;
            var prirazka = ac.Element(Cbc + "ChargeIndicator")?.Value.Trim() == "true";
            f.Polozky.Add(new PolozkaFaktury
            {
                Poradie = poradie++,
                Popis = ac.Element(Cbc + "AllowanceChargeReason")?.Value.Trim() ?? (prirazka ? "Prirážka" : "Zľava"),
                CenaZaJednotku = (prirazka ? suma : -suma) * znamienko,
            });
        }

        var spolu_ = root.Element(Cac + "LegalMonetaryTotal");
        var sDph = Cislo(spolu_?.Element(Cbc + "TaxInclusiveAmount"));
        var zaplatene = Cislo(spolu_?.Element(Cbc + "PrepaidAmount")) ?? 0m;
        var naUhradu = Cislo(spolu_?.Element(Cbc + "PayableAmount"));
        var celkom = sDph ?? (naUhradu is { } nu ? nu + zaplatene : f.Polozky.Sum(p => p.Suma) * znamienko);
        var rozdiel = celkom * znamienko - f.Polozky.Sum(p => p.Suma);
        if (rozdiel != 0)
        {
            var dph = Cislo(root.Element(Cac + "TaxTotal")?.Element(Cbc + "TaxAmount")) ?? 0m;
            f.Polozky.Add(new PolozkaFaktury
            {
                Poradie = poradie,
                Popis = dph != 0 && Math.Abs(dph * znamienko - rozdiel) < 0.02m ? "DPH (neodpočítateľná – nie sme platiteľom DPH)" : "DPH / zaokrúhlenie",
                CenaZaJednotku = rozdiel,
            });
        }
        if (zaplatene != 0)
            f.Uhrady.Add(new UhradaFaktury { Datum = vystavena, Suma = zaplatene * znamienko, Poznamka = "uhradené vopred (zálohou) podľa e-faktúry" });

        if (riadky.Count == 0) upozornenia.Add("E-faktúra neobsahuje žiadne položky.");
        return new Vysledok(f, udaje, f.PartnerIban, mena, upozornenia);
    }

    static string? Hodnota(XElement? e, XName a, XName b) => e?.Element(a)?.Element(b)?.Value.Trim();

    static DateOnly? Datum(XElement? e) =>
        e is not null && DateOnly.TryParse(e.Value.Trim(), CultureInfo.InvariantCulture, out var d) ? d : null;

    static decimal? Cislo(XElement? e) =>
        e is not null && decimal.TryParse(e.Value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>Najčastejšie kódy jednotiek UN/ECE Rec. 20.</summary>
    static string? Jednotka(string? kod) => kod switch
    {
        null or "" => null,
        "C62" or "H87" or "EA" or "XPP" => "ks",
        "HUR" => "hod",
        "DAY" => "deň",
        "MON" => "mes",
        "KGM" => "kg",
        "MTR" => "m",
        "MTK" => "m²",
        "MTQ" => "m³",
        "LTR" => "l",
        "KWH" => "kWh",
        "LS" => "paušál",
        _ => kod,
    };
}
