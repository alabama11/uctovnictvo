using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class FakturyTests
{
    static UctovnaJednotka Firma() => new()
    {
        Id = 1, Typ = TypJednotky.Sro, Nazov = "Test s.r.o.", Ico = "12345678", Ulica = "Hlavná 1", Obec = "Košice", Psc = "040 01",
        Iban = "SK3112000000198742637541", FormatCislaFaktury = "RRRR####", SplatnostDni = 14,
    };

    [Theory]
    [InlineData("RRRR####", 2026, new string[0], "", "20260001")]
    [InlineData("RRRR####", 2026, new[] { "20260001", "20260007", "20250099" }, "", "20260008")]
    [InlineData("FA-RR/###", 2026, new[] { "FA-26/009" }, "", "FA-26/010")]
    [InlineData("RRRR####", 2026, new[] { "20260003", "ZF20260001" }, "ZF", "ZF20260002")]
    [InlineData("RRRR", 2027, new string[0], "", "2027001")]
    public void Dalsie_cislo_v_rade(string format, int rok, string[] existujuce, string predpona, string ocakavane) =>
        Assert.Equal(ocakavane, Faktury.DalsieCislo(format, rok, existujuce, predpona));

    [Fact]
    public void Nova_faktura_ma_vs_a_splatnost()
    {
        var f = Faktury.Nova(Firma(), DruhFaktury.Vydana, TypFaktury.Faktura, new DateOnly(2026, 11, 2), new[] { "20260004" });
        Assert.Equal("20260005", f.Cislo);
        Assert.Equal("20260005", f.VariabilnySymbol);
        Assert.Equal(new DateOnly(2026, 11, 16), f.DatumSplatnosti);
    }

    [Fact]
    public void Dobropis_a_vyuctovanie_zalohy()
    {
        var j = Firma();
        var f = Faktury.Nova(j, DruhFaktury.Vydana, TypFaktury.Faktura, new DateOnly(2026, 3, 1), Array.Empty<string>());
        f.Polozky.Add(new PolozkaFaktury { Popis = "Práca", Mnozstvo = 10, Jednotka = "hod", CenaZaJednotku = 25 });
        f.Polozky.Add(new PolozkaFaktury { Popis = "Materiál", Mnozstvo = 3, CenaZaJednotku = 3.335m });
        Assert.Equal(260.01m, f.Suma); // 250 + 10,005 → 10,01

        var d = Faktury.Dobropis(f, j, new DateOnly(2026, 3, 5), new[] { f.Cislo });
        Assert.Equal(TypFaktury.Dobropis, d.Typ);
        Assert.StartsWith("DB2026", d.Cislo);
        Assert.Equal(f.Cislo, d.PovodnaFaktura);
        Assert.Equal(-260.01m, d.Suma);

        var z = Faktury.Nova(j, DruhFaktury.Vydana, TypFaktury.Zalohova, new DateOnly(2026, 4, 1), Array.Empty<string>());
        z.Polozky.Add(new PolozkaFaktury { Popis = "Záloha na dielo", CenaZaJednotku = 500 });
        z.Uhrady.Add(new UhradaFaktury { Suma = 500, Datum = new DateOnly(2026, 4, 3) });
        var v = Faktury.ZoZalohovej(z, j, new DateOnly(2026, 5, 1), new[] { "20260001" });
        Assert.Equal("20260002", v.Cislo);
        Assert.Equal(0m, v.Suma);
        Assert.Contains(v.Polozky, p => p.Popis.Contains(z.Cislo));
    }

    [Fact]
    public void Stav_faktury()
    {
        var f = new Faktura { DatumSplatnosti = new DateOnly(2026, 1, 10) };
        f.Polozky.Add(new PolozkaFaktury { CenaZaJednotku = 100 });
        Assert.Equal("neuhradená", Faktury.Stav(f, new DateOnly(2026, 1, 10)));
        Assert.Equal("po splatnosti 5 dní", Faktury.Stav(f, new DateOnly(2026, 1, 15)));
        f.Uhrady.Add(new UhradaFaktury { Suma = 40 });
        Assert.Equal("čiastočne, po splatnosti", Faktury.Stav(f, new DateOnly(2026, 1, 15)));
        f.Uhrady.Add(new UhradaFaktury { Suma = 60 });
        Assert.Equal("uhradená", Faktury.Stav(f, new DateOnly(2026, 1, 15)));
        Assert.True(f.JeUhradena);
    }

    [Fact]
    public void Naklady_spolocenstva_z_prijatych_faktur()
    {
        var f1 = new Faktura { Druh = DruhFaktury.Prijata, KategoriaNakladovId = 5, DatumVystavenia = new DateOnly(2026, 1, 5), DatumDodania = new DateOnly(2025, 12, 31) };
        f1.Polozky.Add(new PolozkaFaktury { CenaZaJednotku = 120 });
        var f2 = new Faktura { Druh = DruhFaktury.Prijata, KategoriaNakladovId = 5, DatumVystavenia = new DateOnly(2025, 6, 1) };
        f2.Polozky.Add(new PolozkaFaktury { CenaZaJednotku = 30 });
        var n = Faktury.NakladyZFaktur(new[] { f1, f2 }, 2025);
        Assert.Equal(150m, n[5]);
    }

    [Theory]
    [InlineData(0.5, "nula eur 50 centov")]
    [InlineData(1, "jeden euro")]
    [InlineData(3, "tri eurá")]
    [InlineData(125.5, "stodvadsaťpäť eur 50 centov")]
    [InlineData(2001, "dvetisícjeden eur")]
    [InlineData(1214.02, "tisícdvestoštrnásť eur 2 centy")]
    public void Suma_slovom(decimal suma, string ocakavane) => Assert.Equal(ocakavane, Slovom.Eura(suma));

    [Fact]
    public void Pdf_faktury_upomienky_a_pokladne()
    {
        var j = Firma();
        var f = Faktury.Nova(j, DruhFaktury.Vydana, TypFaktury.Faktura, new DateOnly(2026, 3, 1), Array.Empty<string>());
        f.PartnerNazov = "Odberateľ a.s."; f.PartnerAdresa = "Dlhá 5, 811 01 Bratislava"; f.PartnerIco = "87654321";
        f.Polozky.Add(new PolozkaFaktury { Popis = "Služby", CenaZaJednotku = 99.9m });
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(PdfFaktury.Faktura(f, j), 0, 4));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(PdfFaktury.Upomienka(new[] { f }, j, new DateOnly(2026, 4, 1)), 0, 4));
        var d = Pokladna.ZFaktury(f, new DateOnly(2026, 3, 2), Array.Empty<string>());
        Assert.Equal("PPD2026001", d.Cislo);
        Assert.Equal(0m, f.Zostava);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(PdfFaktury.PokladnicnyDoklad(d, j), 0, 4));
        var (z, r) = Pokladna.Kniha(new[] { d }, 50m, 2026);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(PdfFaktury.PokladnicnaKniha(j, 2026, z, r), 0, 4));
    }
}

public class PokladnaTests
{
    [Fact]
    public void Kniha_pocita_zostatok_a_prenasa_z_minuleho_roka()
    {
        var d = new List<PokladnicnyDoklad>
        {
            new() { Druh = DruhPokladnicnehoDokladu.Prijem, Cislo = "PPD2025001", Datum = new DateOnly(2025, 12, 1), Suma = 200 },
            new() { Druh = DruhPokladnicnehoDokladu.Vydavok, Cislo = "VPD2026001", Datum = new DateOnly(2026, 1, 3), Suma = 80 },
            new() { Druh = DruhPokladnicnehoDokladu.Prijem, Cislo = "PPD2026001", Datum = new DateOnly(2026, 1, 2), Suma = 10 },
        };
        var (zac, riadky) = Pokladna.Kniha(d, 100m, 2026);
        Assert.Equal(300m, zac);
        Assert.Equal(new[] { 310m, 230m }, riadky.Select(r => r.Zostatok));
        Assert.Null(Pokladna.PrvyZapornyZostatok(d, 100m, 2026));
        d.Add(new() { Druh = DruhPokladnicnehoDokladu.Vydavok, Cislo = "VPD2026002", Datum = new DateOnly(2026, 2, 1), Suma = 500 });
        Assert.Equal("VPD2026002", Pokladna.PrvyZapornyZostatok(d, 100m, 2026)!.Doklad.Cislo);
        Assert.Equal("VPD2026003", Pokladna.DalsieCislo(DruhPokladnicnehoDokladu.Vydavok, 2026, d.Select(x => x.Cislo)));
    }
}

public class BankaTests
{
    const string Camt = """
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"><BkToCstmrStmt><Stmt>
          <Ntry><NtryRef>A1</NtryRef><Amt Ccy="EUR">260.01</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-03-14</Dt></BookgDt>
            <NtryDtls><TxDtls><Refs><EndToEndId>/VS20260001/SS/KS</EndToEndId></Refs>
            <RltdPties><Dbtr><Nm>Odberatel a.s.</Nm></Dbtr><DbtrAcct><Id><IBAN>SK8975000000000012345671</IBAN></Id></DbtrAcct></RltdPties></TxDtls></NtryDtls></Ntry>
          <Ntry><NtryRef>A2</NtryRef><Amt Ccy="EUR">45.60</Amt><CdtDbtInd>DBIT</CdtDbtInd><BookgDt><Dt>2026-03-15</Dt></BookgDt>
            <NtryDtls><TxDtls><Refs><EndToEndId>NOTPROVIDED</EndToEndId></Refs>
            <RltdPties><Cdtr><Nm>Dodavatel s.r.o.</Nm></Cdtr><CdtrAcct><Id><IBAN>SK3112000000198742637541</IBAN></Id></CdtrAcct></RltdPties>
            <RmtInf><Ustrd>platba faktury</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry>
          <Ntry><NtryRef>A3</NtryRef><Amt Ccy="EUR">5.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><BookgDt><Dt>2026-03-31</Dt></BookgDt>
            <NtryDtls><TxDtls><RmtInf><Ustrd>Poplatok za vedenie uctu</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry>
        </Stmt></BkToCstmrStmt></Document>
        """;

    [Fact]
    public void Nacita_prijmy_aj_vydavky_a_sparuje_na_faktury()
    {
        var pohyby = Banka.NacitajCamt053(XDocument.Parse(Camt));
        Assert.Equal(3, pohyby.Count);
        Assert.Equal(new[] { 260.01m, -45.60m, -5m }, pohyby.Select(p => p.Suma));
        Assert.Equal("SK3112000000198742637541", pohyby[1].ProtiIban);
        Assert.Equal("Dodavatel s.r.o.", pohyby[1].ProtiNazov);
        Assert.Equal("A1", pohyby[0].Referencia);

        var vydana = new Faktura { Druh = DruhFaktury.Vydana, Cislo = "20260001", VariabilnySymbol = "20260001" };
        vydana.Polozky.Add(new PolozkaFaktury { CenaZaJednotku = 260.01m });
        var prijata = new Faktura { Druh = DruhFaktury.Prijata, Cislo = "PF20260001", VariabilnySymbol = "778899", PartnerIban = "SK31 1200 0000 1987 4263 7541" };
        prijata.Polozky.Add(new PolozkaFaktury { CenaZaJednotku = 45.60m });

        var navrh = Banka.Paruj(pohyby, new[] { vydana, prijata });
        Assert.Same(vydana, navrh[0].Faktura);
        Assert.Same(prijata, navrh[1].Faktura);
        Assert.Equal("účet partnera a suma", navrh[1].Dovod);
        Assert.Null(navrh[2].Faktura);

        Banka.Uhrad(vydana, pohyby[0]);
        Banka.Uhrad(prijata, pohyby[1]);
        Assert.Equal(0m, vydana.Zostava);
        Assert.Equal(0m, prijata.Zostava);
    }
}

public class RegisterIcoTests
{
    // Skrátená odpoveď RPO API (štruktúra podľa api.statistics.sk/rpo/v1/search).
    const string Odpoved = """
        {"results":[{"id":123,
          "identifiers":[{"value":"35763469","validFrom":"1999-01-01"}],
          "fullNames":[{"value":"Stary nazov a.s.","validFrom":"1999-01-01","validTo":"2010-03-01"},{"value":"Slovak Telekom, a.s.","validFrom":"2010-03-02"}],
          "addresses":[{"street":"Bajkalská","regNumber":0,"buildingNumber":"28","postalCodes":["81762"],
                        "municipality":{"value":"Bratislava - mestská časť Ružinov"},"country":{"value":"Slovenská republika"},"validFrom":"2010-03-02"}],
          "establishment":"1999-01-01"}]}
        """;

    [Fact]
    public void Precita_aktualny_nazov_a_adresu()
    {
        var u = RegisterIco.Precitaj(Odpoved, "35763469")!;
        Assert.Equal("Slovak Telekom, a.s.", u.Nazov);
        Assert.Equal("Bajkalská 28", u.Ulica);
        Assert.Equal("817 62", u.Psc);
        Assert.Equal("Bratislava - mestská časť Ružinov", u.Obec);
        Assert.Null(u.Stat);
        Assert.Null(RegisterIco.Precitaj("""{"results":[]}""", "35763469"));
    }

    [Theory]
    [InlineData("35 763 469", "35763469")]
    [InlineData("123456", "00123456")]
    [InlineData("12", null)]
    public void Normalizuje_ico(string vstup, string? vystup) => Assert.Equal(vystup, RegisterIco.Normalizuj(vstup));
}

public class EFakturaTests
{
    const string Ubl = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:CustomizationID>urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0</cbc:CustomizationID>
          <cbc:ID>FV-2027-0042</cbc:ID>
          <cbc:IssueDate>2027-01-15</cbc:IssueDate>
          <cbc:DueDate>2027-01-29</cbc:DueDate>
          <cbc:InvoiceTypeCode>380</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>EUR</cbc:DocumentCurrencyCode>
          <cac:AccountingSupplierParty><cac:Party>
            <cac:PostalAddress><cbc:StreetName>Priemyselná 7</cbc:StreetName><cbc:CityName>Žilina</cbc:CityName><cbc:PostalZone>01001</cbc:PostalZone>
              <cac:Country><cbc:IdentificationCode>SK</cbc:IdentificationCode></cac:Country></cac:PostalAddress>
            <cac:PartyTaxScheme><cbc:CompanyID>SK2020123456</cbc:CompanyID><cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme></cac:PartyTaxScheme>
            <cac:PartyLegalEntity><cbc:RegistrationName>Dodávateľ energií a.s.</cbc:RegistrationName><cbc:CompanyID>44556677</cbc:CompanyID></cac:PartyLegalEntity>
          </cac:Party></cac:AccountingSupplierParty>
          <cac:PaymentMeans><cbc:PaymentMeansCode>58</cbc:PaymentMeansCode><cbc:PaymentID>2027000042</cbc:PaymentID>
            <cac:PayeeFinancialAccount><cbc:ID>SK3112000000198742637541</cbc:ID></cac:PayeeFinancialAccount></cac:PaymentMeans>
          <cac:TaxTotal><cbc:TaxAmount currencyID="EUR">23.00</cbc:TaxAmount></cac:TaxTotal>
          <cac:LegalMonetaryTotal>
            <cbc:LineExtensionAmount currencyID="EUR">100.00</cbc:LineExtensionAmount>
            <cbc:TaxExclusiveAmount currencyID="EUR">100.00</cbc:TaxExclusiveAmount>
            <cbc:TaxInclusiveAmount currencyID="EUR">123.00</cbc:TaxInclusiveAmount>
            <cbc:PayableAmount currencyID="EUR">123.00</cbc:PayableAmount>
          </cac:LegalMonetaryTotal>
          <cac:InvoiceLine><cbc:ID>1</cbc:ID><cbc:InvoicedQuantity unitCode="KWH">400</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount currencyID="EUR">80.00</cbc:LineExtensionAmount>
            <cac:Item><cbc:Name>Elektrina spoločné priestory</cbc:Name></cac:Item><cac:Price><cbc:PriceAmount currencyID="EUR">0.20</cbc:PriceAmount></cac:Price></cac:InvoiceLine>
          <cac:InvoiceLine><cbc:ID>2</cbc:ID><cbc:InvoicedQuantity unitCode="MON">1</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount currencyID="EUR">20.00</cbc:LineExtensionAmount>
            <cac:Item><cbc:Name>Stála platba</cbc:Name></cac:Item><cac:Price><cbc:PriceAmount currencyID="EUR">20.00</cbc:PriceAmount></cac:Price></cac:InvoiceLine>
        </Invoice>
        """;

    [Fact]
    public void Nacita_UBL_fakturu_s_DPH_ako_naklad()
    {
        var v = EFaktura.Nacitaj(XDocument.Parse(Ubl));
        var f = v.Faktura;
        Assert.Equal(DruhFaktury.Prijata, f.Druh);
        Assert.Equal("FV-2027-0042", f.CisloDodavatela);
        Assert.Equal("2027000042", f.VariabilnySymbol);
        Assert.Equal("Dodávateľ energií a.s.", f.PartnerNazov);
        Assert.Equal("44556677", f.PartnerIco);
        Assert.Equal("SK2020123456", f.PartnerIcDph);
        Assert.Equal(new DateOnly(2027, 1, 29), f.DatumSplatnosti);
        Assert.Equal(3, f.Polozky.Count);
        Assert.Equal("kWh", f.Polozky[0].Jednotka);
        Assert.Equal(0.2m, f.Polozky[0].CenaZaJednotku);
        Assert.Contains("DPH", f.Polozky[2].Popis);
        Assert.Equal(123m, f.Suma);
        Assert.Empty(v.Upozornenia);
    }

    [Fact]
    public void Odmietne_iny_subor() =>
        Assert.Throws<FormatException>(() => EFaktura.Nacitaj(XDocument.Parse("<Document/>")));
}

public class FakturyDatabazaTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "fdb-" + Guid.NewGuid().ToString("N"));
    public FakturyDatabazaTests() => Directory.CreateDirectory(dir);
    public void Dispose() { try { Directory.Delete(dir, true); } catch { } }

    [Fact]
    public void Ulozi_fakturu_s_uhradou_z_banky_a_pokladne()
    {
        var db = Path.Combine(dir, "t.db");
        Databaza.Priprav(db);
        using (var c = AppDbContext.Open(db))
        {
            var j = new UctovnaJednotka { Typ = TypJednotky.Szco, Nazov = "Ján Test" };
            c.Jednotky.Add(j);
            c.SaveChanges();
            Assert.Equal("RRRR####", c.Jednotky.Single().FormatCislaFaktury);
            var p = new Partner { JednotkaId = j.Id, Nazov = "Partner", Ico = "12345678", Obec = "Nitra" };
            c.Partneri.Add(p);
            var f = Faktury.Nova(j, DruhFaktury.Vydana, TypFaktury.Faktura, new DateOnly(2026, 2, 1), Array.Empty<string>());
            Faktury.NastavPartnera(f, p);
            f.Polozky.Add(new PolozkaFaktury { Popis = "A", CenaZaJednotku = 100 });
            c.Faktury.Add(f);
            var bp = new BankovyPohyb { JednotkaId = j.Id, Datum = new DateOnly(2026, 2, 5), Suma = 60, Referencia = "X" };
            c.BankovePohyby.Add(bp);
            Banka.Uhrad(f, bp);
            var doklad = Pokladna.ZFaktury(f, new DateOnly(2026, 2, 6), Array.Empty<string>());
            doklad.JednotkaId = j.Id;
            c.PokladnicneDoklady.Add(doklad);
            c.SaveChanges();
        }
        using (var c = AppDbContext.Open(db))
        {
            var f = c.Faktury.Include(x => x.Polozky).Include(x => x.Uhrady).Single();
            Assert.Equal(100m, f.Suma);
            Assert.Equal(0m, f.Zostava);
            Assert.Equal("Partner", f.PartnerNazov);
            Assert.Equal(40m, c.PokladnicneDoklady.Single().Suma);
            // zmazanie bankového pohybu zruší aj úhradu
            c.BankovePohyby.Remove(c.BankovePohyby.Single());
            c.SaveChanges();
            Assert.Equal(1, c.UhradyFaktur.Count());
        }
    }
}
