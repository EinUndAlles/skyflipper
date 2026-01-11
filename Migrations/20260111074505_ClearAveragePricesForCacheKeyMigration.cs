using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyFlipperSolo.Migrations
{
    /// <inheritdoc />
    public partial class ClearAveragePricesForCacheKeyMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing index if it exists
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_AveragePrices_ItemTag_Timestamp_Granularity\"");

            // Clear all existing AveragePrices data to avoid unique constraint conflicts
            // when switching from ItemTag-based to CacheKey-based indexing
            migrationBuilder.Sql("DELETE FROM \"AveragePrices\"");

            // Reset sequences if needed
            migrationBuilder.Sql("ALTER SEQUENCE IF EXISTS \"AveragePrices_Id_seq\" RESTART WITH 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
