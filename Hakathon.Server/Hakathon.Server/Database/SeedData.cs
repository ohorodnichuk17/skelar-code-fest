using Microsoft.EntityFrameworkCore;

namespace Hakathon.Server.Database;

public static class SeedData
{
    private static readonly TimeSpan Kyiv = TimeSpan.FromHours(2);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasData(
            new Customer { Id = "C-1", Name = "Олена Коваль", Email = "olena@example.com", Plan = "Premium Monthly · ₴399/міс" },
            new Customer { Id = "C-2", Name = "Андрій Мельник", Email = "andrii@example.com", Plan = "Premium Annual · ₴2990/рік" });

        modelBuilder.Entity<Charge>().HasData(
            new Charge { Id = "CH-1001", CustomerId = "C-1", Amount = 399, Currency = "UAH", ChargedAt = new DateTimeOffset(2026, 9, 28, 10, 2, 0, Kyiv), Description = "Premium Monthly — вересень", Status = ChargeStatus.Paid },
            new Charge { Id = "CH-1002", CustomerId = "C-1", Amount = 399, Currency = "UAH", ChargedAt = new DateTimeOffset(2026, 10, 28, 10, 2, 0, Kyiv), Description = "Premium Monthly — жовтень", Status = ChargeStatus.Paid },
            new Charge { Id = "CH-1003", CustomerId = "C-1", Amount = 399, Currency = "UAH", ChargedAt = new DateTimeOffset(2026, 10, 28, 10, 3, 0, Kyiv), Description = "Premium Monthly — жовтень", Status = ChargeStatus.Paid },
            new Charge { Id = "CH-2001", CustomerId = "C-2", Amount = 2990, Currency = "UAH", ChargedAt = new DateTimeOffset(2026, 3, 15, 9, 0, 0, Kyiv), Description = "Premium Annual — 2026", Status = ChargeStatus.Paid });

        modelBuilder.Entity<Ticket>().HasData(
            new Ticket { Id = "T-1001", CustomerId = "C-1" },
            new Ticket { Id = "T-1002", CustomerId = "C-2" });
    }
}
