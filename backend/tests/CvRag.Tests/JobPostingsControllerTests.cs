using CvRag.Api.Controllers;
using CvRag.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CvRag.Tests;

public class JobPostingsControllerTests
{
    private static CvRagDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CvRagDbContext(options);
    }

    [Fact]
    public async Task Create_SavesJobPostingWithEmbedding()
    {
        var db = CreateInMemoryDb();
        var controller = new JobPostingsController(db, new FakeEmbeddingProvider());

        var result = await controller.Create(new JobPostingsController.CreateRequest
        {
            Text = "Backend Developer, 3+ yil .NET deneyimi"
        });

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Single(db.JobPostings);
        Assert.NotNull(db.JobPostings.First().EmbeddingVector);
    }
}
