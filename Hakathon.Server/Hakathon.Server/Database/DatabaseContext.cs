using Microsoft.EntityFrameworkCore;

namespace Hakathon.Server.Database;

public class DatabaseContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
