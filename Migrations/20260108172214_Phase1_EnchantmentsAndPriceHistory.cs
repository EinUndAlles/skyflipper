using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_EnchantmentsAndPriceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SoldPrice",
                table: "Auctions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FlipOpportunities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuctionUuid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ItemTag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    CurrentPrice = table.Column<long>(type: "bigint", nullable: false),
                    MedianPrice = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedProfit = table.Column<long>(type: "bigint", nullable: false),
                    ProfitMarginPercent = table.Column<double>(type: "double precision", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuctionEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlipOpportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemTag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MedianPrice = table.Column<long>(type: "bigint", nullable: false),
                    AveragePrice = table.Column<long>(type: "bigint", nullable: false),
                    LowestBIN = table.Column<long>(type: "bigint", nullable: false),
                    TotalSales = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Bin_Status_End",
                table: "Auctions",
                columns: new[] { "Bin", "Status", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Status_End",
                table: "Auctions",
                columns: new[] { "Status", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Tag_End",
                table: "Auctions",
                columns: new[] { "Tag", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_FlipOpportunities_AuctionUuid",
                table: "FlipOpportunities",
                column: "AuctionUuid");

            migrationBuilder.CreateIndex(
                name: "IX_FlipOpportunities_DetectedAt",
                table: "FlipOpportunities",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FlipOpportunities_ProfitMarginPercent",
                table: "FlipOpportunities",
                column: "ProfitMarginPercent");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistory_Date",
                table: "PriceHistory",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistory_ItemTag_Date",
                table: "PriceHistory",
                columns: new[] { "ItemTag", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlipOpportunities");

            migrationBuilder.DropTable(
                name: "PriceHistory");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_Bin_Status_End",
                table: "Auctions");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_Status_End",
                table: "Auctions");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_Tag_End",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "SoldPrice",
                table: "Auctions");
        }
    }
}
