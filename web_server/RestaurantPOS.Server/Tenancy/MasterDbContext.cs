using Microsoft.EntityFrameworkCore;

namespace RestaurantPOS.Server.Tenancy;

public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantEntity>()
            .HasIndex(t => t.StoreCode)
            .IsUnique();
    }
}
