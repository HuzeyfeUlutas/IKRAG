using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvRag.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CvDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimilarityScore = table.Column<double>(type: "double precision", nullable: false),
                    LlmScore = table.Column<int>(type: "integer", nullable: false),
                    LlmReasoning = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchResults");
        }
    }
}
