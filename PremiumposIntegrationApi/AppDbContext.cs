using Microsoft.EntityFrameworkCore; 

namespace PremiumposIntegrationApi;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
}
