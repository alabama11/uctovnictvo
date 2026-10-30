using System.Xml.Linq;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class IbanAImportTests
{
    [Theory]
    [InlineData("SK3112000000198742637541", true)]
    [InlineData("SK31 1200 0000 1987 4263 7541", true)]
    [InlineData("SK3112000000198742637542", false)]
    [InlineData("SK31120000001987426375", false)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("", false)]
    public void Kontrola_iban(string iban, bool platny) => Assert.Equal(platny, Iban.JePlatny(iban));

    [Fact]
    public void Formatuje_iban_po_stvoriciach() =>
        Assert.Equal("SK31 1200 0000 1987 4263 7541", Iban.Formatuj("sk3112000000198742637541"));

    const string Camt = """
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"><BkToCstmrStmt><Stmt>
          <Ntry><Amt Ccy="EUR">85.50</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2025-03-14</Dt></BookgDt>
            <NtryDtls><TxDtls><Refs><EndToEndId>/VS0012/SS/KS0308</EndToEndId></Refs>
              <RltdPties><Dbtr><Nm>Ján Novák</Nm></Dbtr><DbtrAcct><Id><IBAN>SK3112000000198742637541</IBAN></Id></DbtrAcct></RltdPties>
            </TxDtls></NtryDtls></Ntry>
          <Ntry><Amt Ccy="EUR">40.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2025-03-15</Dt></BookgDt>
            <NtryDtls><TxDtls><Refs><EndToEndId>NOTPROVIDED</EndToEndId></Refs>
              <RltdPties><DbtrAcct><Id><IBAN>DE89370400440532013000</IBAN></Id></DbtrAcct></RltdPties>
              <RmtInf><Ustrd>zaloha marec</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry>
          <Ntry><Amt Ccy="EUR">12.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><BookgDt><Dt>2025-03-16</Dt></BookgDt></Ntry>
        </Stmt></BkToCstmrStmt></Document>
        """;

    [Fact]
    public void Nacita_prijate_platby_z_camt053_a_sparuje_ich()
    {
        var platby = ImportBanky.NacitajCamt053(XDocument.Parse(Camt));
        Assert.Equal(2, platby.Count);
        Assert.Equal(85.50m, platby[0].Suma);
        Assert.Equal(new DateOnly(2025, 3, 14), platby[0].Datum);
        Assert.Equal("0012", platby[0].VariabilnySymbol);
        Assert.Null(platby[1].VariabilnySymbol);

        var p12 = new Priestor { Id = 1, Cislo = "12", VariabilnySymbol = "12" };
        var p5 = new Priestor { Id = 2, Cislo = "5" };
        p5.Vlastnici.Add(new Vlastnik { Meno = "X", Ucty = { new BankovyUcet { Iban = "DE89 3704 0044 0532 0130 00" } } });

        var parovane = ImportBanky.Paruj(platby, new[] { p12, p5 });
        Assert.Same(p12, parovane[0].Priestor);
        Assert.Same(p5, parovane[1].Priestor);
    }
}
