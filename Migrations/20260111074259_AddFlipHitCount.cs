using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class AddFlipHitCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlipHitCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CacheKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HitCount = table.Column<int>(type: "integer", nullable: false),
                    LastHitAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlipHitCounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlipHitCounts_CacheKey",
                table: "FlipHitCounts",
                column: "CacheKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlipHitCounts_HitCount",
                table: "FlipHitCounts",
                column: "HitCount");

            migrationBuilder.CreateIndex(
                name: "IX_FlipHitCounts_LastHitAt",
                table: "FlipHitCounts",
                column: "LastHitAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlipHitCounts");
        }
    }
}
