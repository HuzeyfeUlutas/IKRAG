using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CvRag.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCvChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CvChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CvDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingVector = table.Column<Vector>(type: "vector(768)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvChunks", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CvChunks");
        }
    }
}
