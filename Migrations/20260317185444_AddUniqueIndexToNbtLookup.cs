using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToNbtLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_AuctionId_KeyId",
                table: "NBTLookups",
                columns: new[] { "AuctionId", "KeyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NBTLookups_AuctionId_KeyId",
                table: "NBTLookups");
        }
    }
}
