using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrozenApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVatDefaultsToExpenseCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultVatRate",
                table: "ExpenseCategories",
                type: "numeric(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTaxType",
                table: "ExpenseCategories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<bool>(
                name: "DefaultIsTaxClaimable",
                table: "ExpenseCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultIsEntertainment",
                table: "ExpenseCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VatDefaultLocked",
                table: "ExpenseCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Expenses",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Expenses",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Expenses",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxType",
                table: "Expenses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxClaimable",
                table: "Expenses",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEntertainment",
                table: "Expenses",
                type: "boolean",
                nullable: true);

            // Set VAT defaults for existing categories (by Id)
            migrationBuilder.Sql(@"
                UPDATE ""ExpenseCategories"" SET ""DefaultVatRate"" = 0.05, ""DefaultTaxType"" = 'Standard', ""DefaultIsTaxClaimable"" = true WHERE ""Id"" = 1;
                UPDATE ""ExpenseCategories"" SET ""DefaultVatRate"" = 0.05, ""DefaultTaxType"" = 'Standard', ""DefaultIsTaxClaimable"" = true WHERE ""Id"" = 2;
                UPDATE ""ExpenseCategories"" SET ""DefaultTaxType"" = 'OutOfScope', ""DefaultIsTaxClaimable"" = false WHERE ""Id"" = 3;
                UPDATE ""ExpenseCategories"" SET ""DefaultVatRate"" = 0.05, ""DefaultTaxType"" = 'Standard', ""DefaultIsTaxClaimable"" = true WHERE ""Id"" = 4;
                UPDATE ""ExpenseCategories"" SET ""DefaultTaxType"" = 'Petroleum', ""DefaultIsTaxClaimable"" = false WHERE ""Id"" = 5;
                UPDATE ""ExpenseCategories"" SET ""DefaultVatRate"" = 0.05, ""DefaultTaxType"" = 'Standard', ""DefaultIsTaxClaimable"" = true WHERE ""Id"" = 6;
                UPDATE ""ExpenseCategories"" SET ""DefaultVatRate"" = 0.05, ""DefaultIsEntertainment"" = true WHERE ""Id"" = 7;
                UPDATE ""ExpenseCategories"" SET ""DefaultVatRate"" = 0.05, ""DefaultTaxType"" = 'Standard', ""DefaultIsTaxClaimable"" = true WHERE ""Id"" = 8;
                UPDATE ""ExpenseCategories"" SET ""DefaultTaxType"" = 'Exempt', ""DefaultIsTaxClaimable"" = false WHERE ""Id"" = 9;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DefaultVatRate", table: "ExpenseCategories");
            migrationBuilder.DropColumn(name: "DefaultTaxType", table: "ExpenseCategories");
            migrationBuilder.DropColumn(name: "DefaultIsTaxClaimable", table: "ExpenseCategories");
            migrationBuilder.DropColumn(name: "DefaultIsEntertainment", table: "ExpenseCategories");
            migrationBuilder.DropColumn(name: "VatDefaultLocked", table: "ExpenseCategories");
            migrationBuilder.DropColumn(name: "VatRate", table: "Expenses");
            migrationBuilder.DropColumn(name: "VatAmount", table: "Expenses");
            migrationBuilder.DropColumn(name: "TotalAmount", table: "Expenses");
            migrationBuilder.DropColumn(name: "TaxType", table: "Expenses");
            migrationBuilder.DropColumn(name: "IsTaxClaimable", table: "Expenses");
            migrationBuilder.DropColumn(name: "IsEntertainment", table: "Expenses");
        }
    }
}
