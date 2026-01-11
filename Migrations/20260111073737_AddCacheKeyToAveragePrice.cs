using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheKeyToAveragePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AveragePrices_ItemTag_Timestamp_Granularity",
                table: "AveragePrices");

            migrationBuilder.AddColumn<string>(
                name: "CacheKey",
                table: "AveragePrices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AveragePrices_CacheKey_Granularity",
                table: "AveragePrices",
                columns: new[] { "CacheKey", "Granularity" });

            migrationBuilder.CreateIndex(
                name: "IX_AveragePrices_CacheKey_Timestamp_Granularity",
                table: "AveragePrices",
                columns: new[] { "CacheKey", "Timestamp", "Granularity" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AveragePrices_CacheKey_Granularity",
                table: "AveragePrices");

            migrationBuilder.DropIndex(
                name: "IX_AveragePrices_CacheKey_Timestamp_Granularity",
                table: "AveragePrices");

            migrationBuilder.DropColumn(
                name: "CacheKey",
                table: "AveragePrices");

            migrationBuilder.CreateIndex(
                name: "IX_AveragePrices_ItemTag_Timestamp_Granularity",
                table: "AveragePrices",
                columns: new[] { "ItemTag", "Timestamp", "Granularity" },
                unique: true);
        }
    }
}
