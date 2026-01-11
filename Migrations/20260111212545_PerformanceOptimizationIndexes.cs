using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceOptimizationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AveragePrices_Granularity_Timestamp_Volume",
                table: "AveragePrices",
                columns: new[] { "Granularity", "Timestamp", "Volume" });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Status_SoldAt",
                table: "Auctions",
                columns: new[] { "Status", "SoldAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AveragePrices_Granularity_Timestamp_Volume",
                table: "AveragePrices");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_Status_SoldAt",
                table: "Auctions");
        }
    }
}
