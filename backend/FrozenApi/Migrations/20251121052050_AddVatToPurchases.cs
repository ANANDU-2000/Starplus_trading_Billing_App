using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrozenApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVatToPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Purchases",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatTotal",
                table: "Purchases",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostExclVat",
                table: "PurchaseItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "PurchaseItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3031));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3239));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3240));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3242));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3243));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3245));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3248));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3250));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3251));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 142, DateTimeKind.Utc).AddTicks(3253));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(7661), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(7879) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8260), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8261) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8265), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8265) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8317), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8318) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8321), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8322) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8325), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8325) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8328), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8329) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8332), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8332) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8335), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8336) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8355), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8355) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8370), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8382) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8387), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8387) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8391), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8392) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8395), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8396) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8399), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8399) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8404), new DateTime(2025, 11, 21, 5, 20, 48, 141, DateTimeKind.Utc).AddTicks(8404) });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_ADDRESS",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 140, DateTimeKind.Utc).AddTicks(6632));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_NAME_AR",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 140, DateTimeKind.Utc).AddTicks(6631));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_NAME_EN",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 140, DateTimeKind.Utc).AddTicks(6629));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_PHONE",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 140, DateTimeKind.Utc).AddTicks(6633));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_TRN",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 140, DateTimeKind.Utc).AddTicks(6633));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "CURRENCY",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 140, DateTimeKind.Utc).AddTicks(6634));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "VAT_PERCENT",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 5, 20, 48, 140, DateTimeKind.Utc).AddTicks(5920));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2025, 11, 21, 5, 20, 48, 138, DateTimeKind.Utc).AddTicks(9838), "$2a$11$.6xJ4H/2BlmPhX9kuUdisOM3sfkxXjZablywJ7QS18QWdUZL7B7XK" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "VatTotal",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "UnitCostExclVat",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "PurchaseItems");

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(3827));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4152));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4154));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4157));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4159));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4161));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4409));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4411));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4413));

            migrationBuilder.UpdateData(
                table: "ExpenseCategories",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4415));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(5493), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(6029) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7163), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7165) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7857), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7863) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7889), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7890) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7896), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7897) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7901), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7902) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7906), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7907) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7911), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7912) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8203), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8205) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8216), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8216) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8252), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8265) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8270), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8273) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8282), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8284) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8291), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8292) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8297), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8298) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8302), new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8302) });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_ADDRESS",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7292));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_NAME_AR",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7290));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_NAME_EN",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7283));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_PHONE",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7294));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "COMPANY_TRN",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7293));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "CURRENCY",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7297));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "VAT_PERCENT",
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 14, 15, 181, DateTimeKind.Utc).AddTicks(9960));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2025, 11, 15, 13, 14, 15, 172, DateTimeKind.Utc).AddTicks(5485), "$2a$11$2NrWEj1qE5TaBVDxya.pougj0VN2aLQDA8zyKKsh4i/MH1R.88AKa" });
        }
    }
}
