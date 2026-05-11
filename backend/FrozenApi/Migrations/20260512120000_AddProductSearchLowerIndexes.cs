using FrozenApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrozenApi.Migrations
{
    /// <summary>
    /// Expression indexes for case-insensitive product search (ILIKE %term% still scans;
    /// these help planner for prefix patterns and keep POS search fast on large catalogs).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260512120000_AddProductSearchLowerIndexes")]
    public partial class AddProductSearchLowerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_Products_NameEn_Lower"
                ON "Products" (LOWER("NameEn"));
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_Products_Sku_Lower"
                ON "Products" (LOWER("Sku"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Products_Sku_Lower"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Products_NameEn_Lower"";");
        }
    }
}
