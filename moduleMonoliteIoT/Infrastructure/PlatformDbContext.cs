using Microsoft.EntityFrameworkCore;
using moduleMonoliteloT.Domain;

namespace moduleMonoliteloT.Infrastructure;

public class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options) : base(options)
    {
    }

    // Объявляем наши таблицы
    public DbSet<AccountBalance> Balances { get; set; } = null!;
    public DbSet<BillingTransaction> Transactions { get; set; } = null!;

    // Настройка правил для таблиц (Констрейнты)
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Говорим Postgres, что IdempotencyKey должен быть СТРОГО уникальным. 
        // База сама выбросит ошибку, если мы попытаемся вставить дубликат!
        modelBuilder.Entity<BillingTransaction>()
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        // Делаем UserId уникальным для таблицы балансов (у одного фермера — один кошелек)
        modelBuilder.Entity<AccountBalance>()
            .HasIndex(b => b.UserId)
            .IsUnique();
    }
}
