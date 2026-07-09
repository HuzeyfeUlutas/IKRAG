using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CvRag.Api.Data;

public class CvRagDbContext : DbContext
{
    public CvRagDbContext(DbContextOptions<CvRagDbContext> options) : base(options) { }

    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<CvDocument>()
            .Property(c => c.EmbeddingVector)
            .HasColumnType("vector(768)");

        base.OnModelCreating(modelBuilder);
    }
}
