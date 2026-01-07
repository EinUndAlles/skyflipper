using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class AddTextureToAuction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auctions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Uuid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UId = table.Column<long>(type: "bigint", nullable: false),
                    Tag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    StartingBid = table.Column<long>(type: "bigint", nullable: false),
                    HighestBidAmount = table.Column<long>(type: "bigint", nullable: false),
                    Bin = table.Column<bool>(type: "boolean", nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuctioneerId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Reforge = table.Column<int>(type: "integer", nullable: false),
                    AnvilUses = table.Column<short>(type: "smallint", nullable: false),
                    ItemCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FlatenedNBTJson = table.Column<string>(type: "text", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WasSold = table.Column<bool>(type: "boolean", nullable: false),
                    Texture = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auctions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enchantments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<byte>(type: "smallint", nullable: false),
                    AuctionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enchantments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enchantments_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuctionId = table.Column<int>(type: "integer", nullable: false),
                    CurrentPrice = table.Column<long>(type: "bigint", nullable: false),
                    MedianPrice = table.Column<long>(type: "bigint", nullable: false),
                    TargetPrice = table.Column<long>(type: "bigint", nullable: false),
                    Profit = table.Column<long>(type: "bigint", nullable: false),
                    ProfitPercent = table.Column<double>(type: "double precision", nullable: false),
                    ReferenceCount = table.Column<int>(type: "integer", nullable: false),
                    FoundAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotificationSent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flips_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_End",
                table: "Auctions",
                column: "End");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Tag",
                table: "Auctions",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Tag_Tier_Reforge",
                table: "Auctions",
                columns: new[] { "Tag", "Tier", "Reforge" });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_UId",
                table: "Auctions",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Uuid",
                table: "Auctions",
                column: "Uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enchantments_AuctionId_Type",
                table: "Enchantments",
                columns: new[] { "AuctionId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Flips_AuctionId",
                table: "Flips",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Flips_FoundAt",
                table: "Flips",
                column: "FoundAt");

            migrationBuilder.CreateIndex(
                name: "IX_Flips_NotificationSent",
                table: "Flips",
                column: "NotificationSent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Enchantments");

            migrationBuilder.DropTable(
                name: "Flips");

            migrationBuilder.DropTable(
                name: "Auctions");
        }
    }
}
