using Microsoft.EntityFrameworkCore;
using PaymentService.Models;

namespace PaymentService.DAL;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.IdempotencyKey)
            .IsUnique();
    }
}
