using Microsoft.EntityFrameworkCore;

namespace RebusPoc.EventClient.Orders;

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).ValueGeneratedNever();
            order.Property(o => o.Amount).HasPrecision(18, 2);
        });
    }
}
