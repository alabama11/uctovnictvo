using Microsoft.EntityFrameworkCore;
using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Model;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class DatabazaTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "uct-test-" + Guid.NewGuid().ToString("N"));

    public DatabazaTests() => Directory.CreateDirectory(dir);
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Ulozi_spolocenstvo_zalohuje_a_obnovi()
    {
        var db = Path.Combine(dir, "a.db");
        Databaza.Priprav(db);
        using (var c = AppDbContext.Open(db))
        {
            var j = new UctovnaJednotka { Nazov = "SVB Hlavná 5", Typ = TypJednotky.Spolocenstvo };
            var p = new Priestor { Cislo = "1", Plocha = 55.3m };
            p.Vlastnici.Add(new Vlastnik { Meno = "Ján Novák", Ucty = { new BankovyUcet { Iban = "SK3112000000198742637541" } } });
            p.Uhrady.Add(new Uhrada { Datum = new DateOnly(2025, 1, 10), Suma = 80.25m });
            j.Priestory.Add(p);
            c.Jednotky.Add(j);
            c.SaveChanges();
        }

        var zip = Path.Combine(dir, "zaloha.zip");
        Zaloha.Vytvor(db, zip);

        using (var c = AppDbContext.Open(db))
        {
            c.Jednotky.ExecuteDelete();
        }

        Zaloha.Obnov(zip, db);
        using (var c = AppDbContext.Open(db))
        {
            var j = c.Jednotky.Include(x => x.Priestory).ThenInclude(p => p.Uhrady).Single();
            Assert.Equal("SVB Hlavná 5", j.Nazov);
            Assert.Equal(80.25m, j.Priestory[0].Uhrady[0].Suma);
        }
        Assert.True(File.Exists(db + ".pred-obnovou"));
    }

    [Fact]
    public void Export_do_excelu_vytvori_subory()
    {
        var (byty, kat) = Vzor.Dom();
        var j = new UctovnaJednotka { Nazov = "SVB" };
        var v = Vyuctovanie.Vypocitaj(Vzor.Rok, byty, kat);

        var f1 = Path.Combine(dir, "v.xlsx");
        var f2 = Path.Combine(dir, "u.xlsx");
        ExportExcel.Vyuctovanie(j, v, f1);
        ExportExcel.UhradyZaRok(j, byty, Vzor.Rok, f2);
        Assert.True(new FileInfo(f1).Length > 0);
        Assert.True(new FileInfo(f2).Length > 0);
    }
}
