using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class NbtEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceHistory");

            migrationBuilder.AddColumn<int>(
                name: "NbtDataId",
                table: "Auctions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AveragePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemTag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Granularity = table.Column<int>(type: "integer", nullable: false),
                    Min = table.Column<double>(type: "double precision", nullable: false),
                    Max = table.Column<double>(type: "double precision", nullable: false),
                    Avg = table.Column<double>(type: "double precision", nullable: false),
                    Median = table.Column<double>(type: "double precision", nullable: false),
                    Volume = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AveragePrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NbtData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Data = table.Column<byte[]>(type: "bytea", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NbtData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NBTLookups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuctionId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValueNumeric = table.Column<long>(type: "bigint", nullable: true),
                    ValueString = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NBTLookups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NBTLookups_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_NbtDataId",
                table: "Auctions",
                column: "NbtDataId");

            migrationBuilder.CreateIndex(
                name: "IX_AveragePrices_ItemTag_Granularity",
                table: "AveragePrices",
                columns: new[] { "ItemTag", "Granularity" });

            migrationBuilder.CreateIndex(
                name: "IX_AveragePrices_ItemTag_Timestamp_Granularity",
                table: "AveragePrices",
                columns: new[] { "ItemTag", "Timestamp", "Granularity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AveragePrices_Timestamp",
                table: "AveragePrices",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_AuctionId",
                table: "NBTLookups",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_Key_ValueNumeric",
                table: "NBTLookups",
                columns: new[] { "Key", "ValueNumeric" });

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_Key_ValueString",
                table: "NBTLookups",
                columns: new[] { "Key", "ValueString" });

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_NbtData_NbtDataId",
                table: "Auctions",
                column: "NbtDataId",
                principalTable: "NbtData",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auctions_NbtData_NbtDataId",
                table: "Auctions");

            migrationBuilder.DropTable(
                name: "AveragePrices");

            migrationBuilder.DropTable(
                name: "NbtData");

            migrationBuilder.DropTable(
                name: "NBTLookups");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_NbtDataId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "NbtDataId",
                table: "Auctions");

            migrationBuilder.CreateTable(
                name: "PriceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AveragePrice = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemTag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    LowestBIN = table.Column<long>(type: "bigint", nullable: false),
                    MedianPrice = table.Column<long>(type: "bigint", nullable: false),
                    TotalSales = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistory", x => x.Id);
                });

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
    }
}
