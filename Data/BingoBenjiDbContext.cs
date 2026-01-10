using BingoBenji.Models;
using Microsoft.EntityFrameworkCore;

namespace BingoBenji.Data;

public class BingoBenjiDbContext : DbContext
{
    public BingoBenjiDbContext(DbContextOptions<BingoBenjiDbContext> options) : base(options) { }

    public DbSet<BingoGeneration> BingoGenerations => Set<BingoGeneration>();
    public DbSet<BingoSheet> BingoSheets => Set<BingoSheet>();
    public DbSet<Winner> Winners => Set<Winner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BingoGeneration>().ToTable("BingoGenerations");
        modelBuilder.Entity<BingoSheet>().ToTable("BingoSheets");
        modelBuilder.Entity<Winner>().ToTable("Winners");

        modelBuilder.Entity<BingoGeneration>()
            .HasIndex(x => x.GenerationCode).IsUnique();

        modelBuilder.Entity<BingoSheet>()
            .HasIndex(x => x.ContentHash).IsUnique();

        modelBuilder.Entity<BingoSheet>()
            .HasIndex(x => new { x.GenerationId, x.SheetNumber }).IsUnique();
    }
}
