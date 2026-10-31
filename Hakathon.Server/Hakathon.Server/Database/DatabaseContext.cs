using Microsoft.EntityFrameworkCore;

namespace Hakathon.Server.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<PendingAction> PendingActions => Set<PendingAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SeedData.Apply(modelBuilder);
    }
}
