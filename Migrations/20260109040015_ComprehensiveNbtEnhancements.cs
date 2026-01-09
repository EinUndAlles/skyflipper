using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class ComprehensiveNbtEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "NBTLookups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<short>(
                name: "KeyId",
                table: "NBTLookups",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NBTKeys",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KeyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NBTKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_KeyId_ValueNumeric",
                table: "NBTLookups",
                columns: new[] { "KeyId", "ValueNumeric" });

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_KeyId_ValueString",
                table: "NBTLookups",
                columns: new[] { "KeyId", "ValueString" });

            migrationBuilder.CreateIndex(
                name: "IX_NBTKeys_KeyName",
                table: "NBTKeys",
                column: "KeyName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NBTLookups_NBTKeys_KeyId",
                table: "NBTLookups",
                column: "KeyId",
                principalTable: "NBTKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NBTLookups_NBTKeys_KeyId",
                table: "NBTLookups");

            migrationBuilder.DropTable(
                name: "NBTKeys");

            migrationBuilder.DropIndex(
                name: "IX_NBTLookups_KeyId_ValueNumeric",
                table: "NBTLookups");

            migrationBuilder.DropIndex(
                name: "IX_NBTLookups_KeyId_ValueString",
                table: "NBTLookups");

            migrationBuilder.DropColumn(
                name: "KeyId",
                table: "NBTLookups");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "NBTLookups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
