using Kasa.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kasa.Api.Data;

public class KasaDbContext(DbContextOptions<KasaDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<FleetSnapshot> FleetSnapshots => Set<FleetSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.IsActive).HasDefaultValue(true);
            e.HasIndex(c => new { c.Type, c.IsActive });
            e.HasData(SeedCategories());
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Note).HasMaxLength(500);
            // SQLite TEXT'te Kind kaybolur; CreatedAt sözleşmesi UTC'dir, okurken geri işaretle.
            e.Property(t => t.CreatedAt).HasConversion(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.HasIndex(t => t.Date);
            e.HasOne(t => t.Category)
                .WithMany()
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Transaction_AmountSatang", "\"AmountSatang\" > 0"));
        });

        modelBuilder.Entity<FleetSnapshot>(e =>
        {
            e.HasIndex(f => f.Date).IsUnique(); // gün başına tek kayıt: upsert bu indekse yaslanır
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FleetSnapshot_NonNegative",
                    "\"TotalBikes\" >= 0 AND \"BrokenBikes\" >= 0 AND \"RentedBikes\" >= 0");
                t.HasCheckConstraint("CK_FleetSnapshot_Capacity",
                    "\"BrokenBikes\" + \"RentedBikes\" <= \"TotalBikes\"");
            });
        });

        modelBuilder.Entity<Setting>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Value).IsRequired();
            e.HasData(
                new Setting { Key = "PosFeeRate", Value = "0.035" },
                new Setting { Key = "Partner1Name", Value = "Amornrat Thanmaen" },
                new Setting { Key = "Partner1Share", Value = "0.90" },
                new Setting { Key = "Partner2Name", Value = "Thanchanok Sabancıoğlu" },
                new Setting { Key = "Partner2Share", Value = "0.10" });
        });
    }

    // Seed Id'leri deterministik: 1-6 gelir, 7-14 gider. Migration'lar bu Id'lere bağlı, değiştirme.
    private static Category[] SeedCategories()
    {
        string[] income = ["Kiralama", "Eksik Yakıt Tahsilatı", "Ekstra Servis", "Kiralama Uzatma", "Hasar", "Diğer"];
        string[] expense = ["Servis Bakım", "Yakıt Alım", "Su Alım", "Nakliye", "Kira", "Elektrik", "Maaş", "Diğer"];

        var categories = new List<Category>();
        for (var i = 0; i < income.Length; i++)
            categories.Add(new Category { Id = i + 1, Name = income[i], Type = TransactionType.Income, IsActive = true, SortOrder = i + 1 });
        for (var i = 0; i < expense.Length; i++)
            categories.Add(new Category { Id = income.Length + i + 1, Name = expense[i], Type = TransactionType.Expense, IsActive = true, SortOrder = i + 1 });
        return [.. categories];
    }
}
