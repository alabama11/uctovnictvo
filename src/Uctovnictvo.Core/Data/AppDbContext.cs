using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Uctovnictvo.Core.Model;

namespace Uctovnictvo.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UctovnaJednotka> Jednotky => Set<UctovnaJednotka>();
    public DbSet<Priestor> Priestory => Set<Priestor>();
    public DbSet<Vlastnik> Vlastnici => Set<Vlastnik>();
    public DbSet<BankovyUcet> BankoveUcty => Set<BankovyUcet>();
    public DbSet<KategoriaNakladov> Kategorie => Set<KategoriaNakladov>();
    public DbSet<NakladRoka> NakladyRoka => Set<NakladRoka>();
    public DbSet<PredpisPolozky> Predpisy => Set<PredpisPolozky>();
    public DbSet<PriestorRok> PriestorRoky => Set<PriestorRok>();
    public DbSet<Uhrada> Uhrady => Set<Uhrada>();
    public DbSet<PohybFondu> PohybyFondu => Set<PohybFondu>();
    public DbSet<Nastavenie> Nastavenia => Set<Nastavenie>();
    public DbSet<Partner> Partneri => Set<Partner>();
    public DbSet<Faktura> Faktury => Set<Faktura>();
    public DbSet<PolozkaFaktury> PolozkyFaktur => Set<PolozkaFaktury>();
    public DbSet<UhradaFaktury> UhradyFaktur => Set<UhradaFaktury>();
    public DbSet<BankovyPohyb> BankovePohyby => Set<BankovyPohyb>();
    public DbSet<PokladnicnyDoklad> PokladnicneDoklady => Set<PokladnicnyDoklad>();
    public DbSet<KategoriaPohybu> KategoriePohybov => Set<KategoriaPohybu>();
    public DbSet<Majetok> Majetok => Set<Majetok>();
    public DbSet<Vozidlo> Vozidla => Set<Vozidlo>();
    public DbSet<Jazda> Jazdy => Set<Jazda>();
    public DbSet<Ucet> Ucty => Set<Ucet>();
    public DbSet<UctovnyZapis> UctovneZapisy => Set<UctovnyZapis>();

    public static AppDbContext Open(string dbPath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new AppDbContext(options);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UctovnaJednotka>().ToTable("Jednotky");
        b.Entity<Priestor>(e =>
        {
            e.ToTable("Priestory");
            e.HasIndex(p => new { p.JednotkaId, p.Cislo }).IsUnique();
            e.Ignore(p => p.MenaVlastnikov);
        });
        b.Entity<Vlastnik>().ToTable("Vlastnici");
        b.Entity<BankovyUcet>(e =>
        {
            e.ToTable("BankoveUcty");
            e.HasIndex(u => u.Iban);
        });
        b.Entity<KategoriaNakladov>().ToTable("KategorieNakladov");
        b.Entity<NakladRoka>(e =>
        {
            e.ToTable("NakladyRoka");
            e.HasIndex(n => new { n.KategoriaId, n.Rok }).IsUnique();
        });
        b.Entity<PredpisPolozky>(e =>
        {
            e.ToTable("Predpisy");
            e.HasIndex(p => new { p.PriestorId, p.KategoriaId, p.Rok }).IsUnique();
            e.HasOne(p => p.Kategoria).WithMany().HasForeignKey(p => p.KategoriaId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PriestorRok>(e =>
        {
            e.ToTable("PriestorRoky");
            e.HasIndex(p => new { p.PriestorId, p.Rok }).IsUnique();
            e.Ignore(p => p.SpotrebaVody);
        });
        b.Entity<Uhrada>(e =>
        {
            e.ToTable("Uhrady");
            e.HasIndex(u => u.Datum);
        });
        b.Entity<Priestor>().Ignore(p => p.SucasniVlastnici);
        b.Entity<PohybFondu>().ToTable("PohybyFondu");
        b.Entity<Nastavenie>(e =>
        {
            e.ToTable("Nastavenia");
            e.HasIndex(n => n.Kluc).IsUnique();
        });
        b.Entity<Partner>(e =>
        {
            e.ToTable("Partneri");
            e.HasIndex(p => new { p.JednotkaId, p.Ico });
            e.Ignore(p => p.Adresa);
        });
        b.Entity<Faktura>(e =>
        {
            e.ToTable("Faktury");
            e.HasIndex(f => new { f.JednotkaId, f.Druh, f.Cislo }).IsUnique();
            e.HasOne(f => f.Partner).WithMany().HasForeignKey(f => f.PartnerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(f => f.KategoriaNakladov).WithMany().HasForeignKey(f => f.KategoriaNakladovId).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(f => f.Suma); e.Ignore(f => f.Uhradene); e.Ignore(f => f.Zostava); e.Ignore(f => f.JeUhradena);
        });
        b.Entity<PolozkaFaktury>(e => { e.ToTable("PolozkyFaktur"); e.Ignore(p => p.Suma); });
        b.Entity<UhradaFaktury>(e =>
        {
            e.ToTable("UhradyFaktur");
            e.HasOne(u => u.BankovyPohyb).WithMany(p => p.Uhrady).HasForeignKey(u => u.BankovyPohybId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(u => u.PokladnicnyDoklad).WithMany(p => p.Uhrady).HasForeignKey(u => u.PokladnicnyDokladId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<BankovyPohyb>(e =>
        {
            e.ToTable("BankovePohyby");
            e.HasIndex(p => new { p.JednotkaId, p.Datum });
            e.HasIndex(p => new { p.JednotkaId, p.Referencia });
        });
        b.Entity<PokladnicnyDoklad>(e =>
        {
            e.ToTable("PokladnicneDoklady");
            e.HasIndex(p => new { p.JednotkaId, p.Cislo }).IsUnique();
            e.HasOne(p => p.Kategoria).WithMany().HasForeignKey(p => p.KategoriaId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<BankovyPohyb>().HasOne(p => p.Kategoria).WithMany().HasForeignKey(p => p.KategoriaId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Faktura>().HasOne(p => p.KategoriaPohybu).WithMany().HasForeignKey(p => p.KategoriaPohybuId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<KategoriaPohybu>().ToTable("KategoriePohybov");
        b.Entity<Majetok>().ToTable("Majetok");
        b.Entity<Vozidlo>().ToTable("Vozidla");
        b.Entity<Jazda>(e => { e.ToTable("Jazdy"); e.HasIndex(j => new { j.VozidloId, j.Datum }); });
        b.Entity<Ucet>(e => { e.ToTable("Ucty"); e.HasIndex(u => new { u.JednotkaId, u.Cislo }).IsUnique(); e.Ignore(u => u.Synteticky); });
        b.Entity<UctovnyZapis>(e => { e.ToTable("UctovneZapisy"); e.HasIndex(z => new { z.JednotkaId, z.Datum }); });
    }
}

/// <summary>Používa ho len nástroj dotnet-ef pri generovaní migrácií.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => AppDbContext.Open("design.db");
}
