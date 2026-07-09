using Microsoft.EntityFrameworkCore;

namespace CvRag.Api.Data;

public class CvRagDbContext : DbContext
{
    public CvRagDbContext(DbContextOptions<CvRagDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        base.OnModelCreating(modelBuilder);
    }
}
