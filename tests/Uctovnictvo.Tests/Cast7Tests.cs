using Uctovnictvo.Core.Data;
using Uctovnictvo.Core.Services;

namespace Uctovnictvo.Tests;

public class PohodlieTests
{
    [Fact]
    public void Heslo_sa_overi_a_da_sa_zrusit()
    {
        var dir = Directory.CreateTempSubdirectory("heslo").FullName;
        try
        {
            var cesta = Path.Combine(dir, "a.db");
            Databaza.Priprav(cesta);
            using (var db = AppDbContext.Open(cesta))
            {
                Assert.False(Heslo.JeNastavene(db));
                Assert.True(Heslo.Over(db, "čokoľvek"));
                Heslo.Nastav(db, "Tajné123");
                Assert.True(Heslo.JeNastavene(db));
                Assert.True(Heslo.Over(db, "Tajné123"));
                Assert.False(Heslo.Over(db, "tajné123"));
                Assert.DoesNotContain("Tajné123", Nastavenia.Citaj(db, "heslo.pbkdf2"));
                Heslo.Nastav(db, "");
                Assert.False(Heslo.JeNastavene(db));
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Aktualizacia_z_odpovede_githubu()
    {
        var v = Aktualizacie.Precitaj("""{"tag_name":"v0.8.0","html_url":"https://github.com/alabama11/uctovnictvo/releases/tag/v0.8.0"}""");
        Assert.NotNull(v);
        Assert.Equal(new Version(0, 8, 0), v!.Verzia);
        Assert.True(Aktualizacie.JeNovsia(new Version(0, 7, 0, 0), v.Verzia));
        Assert.False(Aktualizacie.JeNovsia(new Version(0, 8, 0, 0), v.Verzia));
        Assert.Null(Aktualizacie.Precitaj("""{"tag_name":"latest"}"""));
    }
}
