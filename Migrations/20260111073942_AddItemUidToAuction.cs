using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class AddItemUidToAuction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemUid",
                table: "Auctions",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_ItemUid",
                table: "Auctions",
                column: "ItemUid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auctions_ItemUid",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ItemUid",
                table: "Auctions");
        }
    }
}
