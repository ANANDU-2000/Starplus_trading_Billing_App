using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrozenApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBarcodeIsActiveSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_NameEn",
                table: "Products",
                columns: new[] { "IsActive", "NameEn" });

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_Barcode_Unique_NonEmpty"
                ON "Products" ("Barcode")
                WHERE "Barcode" IS NOT NULL AND BTRIM("Barcode") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Products_Barcode_Unique_NonEmpty"";");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive_NameEn",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Products");
        }
    }
}
