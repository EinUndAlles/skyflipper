using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyNbtLookupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NBTLookups_Key_ValueNumeric",
                table: "NBTLookups");

            migrationBuilder.DropIndex(
                name: "IX_NBTLookups_Key_ValueString",
                table: "NBTLookups");


            migrationBuilder.DropColumn(
                name: "Key",
                table: "NBTLookups");

            migrationBuilder.DropColumn(
                name: "ValueString",
                table: "NBTLookups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "NBTLookups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValueString",
                table: "NBTLookups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_Key_ValueNumeric",
                table: "NBTLookups",
                columns: new[] { "Key", "ValueNumeric" });

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_Key_ValueString",
                table: "NBTLookups",
                columns: new[] { "Key", "ValueString" });

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_KeyId_ValueNumeric",
                table: "NBTLookups",
                columns: new[] { "KeyId", "ValueNumeric" });
        }
    }
}
