using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class FullParityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ValueId",
                table: "NBTLookups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuctionId = table.Column<int>(type: "integer", nullable: false),
                    BidderId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bids_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MinecraftType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FallbackTier = table.Column<int>(type: "integer", nullable: true),
                    FallbackCategory = table.Column<int>(type: "integer", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NBTValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KeyId = table.Column<short>(type: "smallint", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NBTValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NBTValues_NBTKeys_KeyId",
                        column: x => x.KeyId,
                        principalTable: "NBTKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlternativeNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemDetailsId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlternativeNames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlternativeNames_ItemDetails_ItemDetailsId",
                        column: x => x.ItemDetailsId,
                        principalTable: "ItemDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_KeyId_ValueId",
                table: "NBTLookups",
                columns: new[] { "KeyId", "ValueId" });

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_ValueId",
                table: "NBTLookups",
                column: "ValueId");

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeNames_ItemDetailsId",
                table: "AlternativeNames",
                column: "ItemDetailsId");

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeNames_Name",
                table: "AlternativeNames",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionId",
                table: "Bids",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_BidderId",
                table: "Bids",
                column: "BidderId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_Timestamp",
                table: "Bids",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ItemDetails_LastSeen",
                table: "ItemDetails",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_ItemDetails_Tag",
                table: "ItemDetails",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NBTValues_KeyId_Value",
                table: "NBTValues",
                columns: new[] { "KeyId", "Value" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NBTLookups_NBTValues_ValueId",
                table: "NBTLookups",
                column: "ValueId",
                principalTable: "NBTValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NBTLookups_NBTValues_ValueId",
                table: "NBTLookups");

            migrationBuilder.DropTable(
                name: "AlternativeNames");

            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "NBTValues");

            migrationBuilder.DropTable(
                name: "ItemDetails");

            migrationBuilder.DropIndex(
                name: "IX_NBTLookups_KeyId_ValueId",
                table: "NBTLookups");

            migrationBuilder.DropIndex(
                name: "IX_NBTLookups_ValueId",
                table: "NBTLookups");

            migrationBuilder.DropColumn(
                name: "ValueId",
                table: "NBTLookups");
        }
    }
}
