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
    }
}

/// <summary>Používa ho len nástroj dotnet-ef pri generovaní migrácií.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => AppDbContext.Open("design.db");
}
