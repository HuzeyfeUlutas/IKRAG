using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CvRag.Api.Data;

public class CvRagDbContext : DbContext
{
    public CvRagDbContext(DbContextOptions<CvRagDbContext> options) : base(options) { }

    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();
    public DbSet<CvChunk> CvChunks => Set<CvChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<CvDocument>()
                .Property(c => c.EmbeddingVector)
                .HasColumnType("vector(768)");

            modelBuilder.Entity<CvChunk>()
                .Property(c => c.EmbeddingVector)
                .HasColumnType("vector(768)");
        }
        else
        {
            // Providers without pgvector support (e.g. EF Core InMemory used in tests)
            // cannot map the Vector type; exclude it from the model for those providers.
            modelBuilder.Entity<CvDocument>().Ignore(c => c.EmbeddingVector);
            modelBuilder.Entity<CvChunk>().Ignore(c => c.EmbeddingVector);
        }

        base.OnModelCreating(modelBuilder);
    }
}
