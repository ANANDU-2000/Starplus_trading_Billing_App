using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrozenApi.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundOffToSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RoundOff",
                table: "Sales",
                type: "numeric(10,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoundOff",
                table: "Sales");
        }
    }
}
