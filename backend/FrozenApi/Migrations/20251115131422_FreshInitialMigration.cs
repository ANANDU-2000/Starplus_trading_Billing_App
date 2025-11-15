using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FrozenApi.Migrations
{
    /// <inheritdoc />
    public partial class FreshInitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Trn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TotalSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TotalPayments = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    PendingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    LastActivity = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastPaymentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BYTEA", nullable: true, defaultValue: new byte[] { 0 })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ColorCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConversionToBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SellPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StockQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReorderLevel = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DescriptionEn = table.Column<string>(type: "text", nullable: true),
                    DescriptionAr = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false, defaultValue: new byte[] { 0 }),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ChangeQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionType = table.Column<string>(type: "text", nullable: false),
                    RefId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedBy = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_Users_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expenses_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HtmlCode = table.Column<string>(type: "text", nullable: false),
                    CssCode = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceTemplates_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    OldPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NewPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PriceDifference = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChangedBy = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceChangeLogs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceChangeLogs_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InvoiceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExpenseCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InvoiceFilePath = table.Column<string>(type: "text", nullable: true),
                    InvoiceFileName = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Purchases_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    LastPaymentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastModifiedBy = table.Column<int>(type: "integer", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsFinalized = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EditReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false, defaultValue: new byte[] { 0 })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sales_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sales_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sales_Users_LastModifiedBy",
                        column: x => x.LastModifiedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    ReturnNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    EditReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DiffSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceVersions_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceVersions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleItems_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    ReturnNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RestoreStock = table.Column<bool>(type: "boolean", nullable: false),
                    IsBadItem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SaleReturns_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseReturnId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseItemId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseItems_PurchaseItemId",
                        column: x => x.PurchaseItemId,
                        principalTable: "PurchaseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId",
                        column: x => x.PurchaseReturnId,
                        principalTable: "PurchaseReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentIdempotencies",
                columns: table => new
                {
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PaymentId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResponseSnapshot = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIdempotencies", x => x.IdempotencyKey);
                    table.ForeignKey(
                        name: "FK_PaymentIdempotencies_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentIdempotencies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleReturnId = table.Column<int>(type: "integer", nullable: false),
                    SaleItemId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_SaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ExpenseCategories",
                columns: new[] { "Id", "ColorCode", "CreatedAt", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "#EF4444", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(3827), true, "Rent" },
                    { 2, "#F59E0B", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4152), true, "Utilities" },
                    { 3, "#3B82F6", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4154), true, "Staff Salary" },
                    { 4, "#8B5CF6", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4157), true, "Marketing" },
                    { 5, "#14B8A6", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4159), true, "Fuel" },
                    { 6, "#F97316", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4161), true, "Delivery" },
                    { 7, "#EC4899", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4409), true, "Food" },
                    { 8, "#6366F1", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4411), true, "Maintenance" },
                    { 9, "#10B981", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4413), true, "Insurance" },
                    { 10, "#6B7280", new DateTime(2025, 11, 15, 13, 14, 15, 187, DateTimeKind.Utc).AddTicks(4415), true, "Other" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "ConversionToBase", "CostPrice", "CreatedAt", "DescriptionAr", "DescriptionEn", "ExpiryDate", "NameAr", "NameEn", "ReorderLevel", "SellPrice", "Sku", "StockQty", "UnitType", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 1m, 75.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(5493), null, "Frozen chicken griller 1000gm per carton", null, "دجاج شواء مجمد 1000جم - كواليكو", "FROZEN CHICKEN GRILLER 1000GM - QUALIKO", 3, 86.00m, "CHK-GRL-001", 7m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(6029) },
                    { 2, 1m, 85.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7163), null, "Frozen chicken grillers 10x1200gms per carton", null, "دجاج شواء مجمد (10×1200جم) - فرانجوسول", "FROZEN CHICKEN GRILLERS (10X1200GMS)-FRANGOSUL", 3, 99.00m, "CHK-GRL-002", 5m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7165) },
                    { 3, 1m, 60.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7857), null, "Frozen chicken grillers 10x900gms per carton", null, "دجاج شواء مجمد (10×900جم) - سيدروب", "FROZEN CHICKEN GRILLERS (10X900GMS)-CEDROB", 3, 70.00m, "CHK-GRL-003", 1m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7863) },
                    { 4, 1m, 125.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7889), null, "Frozen chicken breast skinless boneless 12kgs", null, "صدر دجاج مجمد بدون جلد/عظم - بركات 12كجم", "FROZEN CHICKEN BREAST S/L B/L - BARKAT 12KGS", 2, 145.00m, "CHK-BREAST-001", 1m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7890) },
                    { 5, 1m, 28.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7896), null, "Frozen tapioca cuts 12x700gm per carton", null, "تأبيوكا مقطعة مجمدة - مالابار (12×700 جم)", "FROZEN-TAPIOCA CUTS-MALABAR (12X700 GM)", 3, 33.00m, "VEG-TAP-001", 4m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7897) },
                    { 6, 1m, 250.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7901), null, "Frozen Indian bobby veal 18kg per carton", null, "لحم عجل هندي بوبي مجمد 18كجم - برايم جولد", "FROZEN INDIAN BOBBY VEAL 18KG- PRIME GOLD", 1, 275.00m, "BEEF-BOB-001", 1m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7902) },
                    { 7, 1m, 250.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7906), null, "Veal leg", null, "لحم عجل ساق أمين", "VEAL LEG AMEEN", 5, 305.00m, "MEAT-VEAL-001", 15m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7907) },
                    { 8, 1m, 75.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7911), null, "Hamoor fillet", null, "هامور فيليه", "HAMOOR FILLET", 5, 88.00m, "SEA-HAMOOR-001", 20m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(7912) },
                    { 9, 1m, 85.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8203), null, "1200 gm chicken", null, "دجاج 1200 جم", "1200 GM CKN", 10, 98.00m, "CHK-1200GM-001", 30m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8205) },
                    { 10, 1m, 95.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8216), null, "1300 gm chicken", null, "دجاج 1300 جم", "1300 GM CKN", 10, 112.00m, "CHK-1300GM-001", 25m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8216) },
                    { 11, 1m, 150.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8252), null, "Chicken breast 12kg", null, "صدر 12 كجم", "BREAST 12KG", 5, 179.00m, "CHK-BREAST-12KG", 12m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8265) },
                    { 12, 1m, 45.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8270), null, "Mumtaz butter", null, "زبدة ممتاز", "MUMTAZ BUTTER", 10, 56.00m, "DAIRY-BUTTER-001", 40m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8273) },
                    { 13, 1m, 115.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8282), null, "Chicken breast 12kg aroura", null, "صدر 12 كجم أورورا", "BREAST 12KG AROURA", 5, 140.00m, "CHK-BREAST-AROURA", 18m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8284) },
                    { 14, 1m, 150.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8291), null, "Meat mince", null, "لحم مفروم", "MEAT MINCE", 5, 180.00m, "MEAT-MINCE-001", 15m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8292) },
                    { 15, 1m, 75.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8297), null, "1000 gm chicken", null, "دجاج 1000 جم", "1000GM CKN", 10, 88.00m, "CHK-1000GM-001", 35m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8298) },
                    { 16, 1m, 85.00m, new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8302), null, "Rose hotdog", null, "هوت دوغ روز", "ROSE HOTDOG", 10, 103.00m, "FOOD-HOTDOG-001", 45m, "CRTN", new DateTime(2025, 11, 15, 13, 14, 15, 186, DateTimeKind.Utc).AddTicks(8302) }
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Key", "CreatedAt", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { "COMPANY_ADDRESS", new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7292), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Mussafah 44, Industrail Area" },
                    { "COMPANY_NAME_AR", new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7290), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "ستار بلس لتجارة المواد الغذائية" },
                    { "COMPANY_NAME_EN", new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7283), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Starplus Foodstuff Trading" },
                    { "COMPANY_PHONE", new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7294), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "+971 555298878" },
                    { "COMPANY_TRN", new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7293), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "100366253100003" },
                    { "CURRENCY", new DateTime(2025, 11, 15, 13, 14, 15, 182, DateTimeKind.Utc).AddTicks(7297), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "AED" },
                    { "VAT_PERCENT", new DateTime(2025, 11, 15, 13, 14, 15, 181, DateTimeKind.Utc).AddTicks(9960), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "5" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "Name", "PasswordHash", "Phone", "Role" },
                values: new object[] { 1, new DateTime(2025, 11, 15, 13, 14, 15, 172, DateTimeKind.Utc).AddTicks(5485), "admin@starplus.com", "Admin", "$2a$11$2NrWEj1qE5TaBVDxya.pougj0VN2aLQDA8zyKKsh4i/MH1R.88AKa", "+971 555 298 878", "Admin" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_CreatedAt",
                table: "Alerts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IsRead",
                table: "Alerts",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IsResolved",
                table: "Alerts",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_ResolvedBy",
                table: "Alerts",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Type",
                table: "Alerts",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Name",
                table: "ExpenseCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryId",
                table: "Expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedBy",
                table: "Expenses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductId",
                table: "InventoryTransactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_CreatedBy",
                table: "InvoiceTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_IsActive",
                table: "InvoiceTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceVersions_CreatedById",
                table: "InvoiceVersions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceVersions_SaleId",
                table: "InvoiceVersions",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceVersions_SaleId_VersionNumber",
                table: "InvoiceVersions",
                columns: new[] { "SaleId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIdempotencies_IdempotencyKey",
                table: "PaymentIdempotencies",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIdempotencies_PaymentId",
                table: "PaymentIdempotencies",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIdempotencies_UserId",
                table: "PaymentIdempotencies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedBy",
                table: "Payments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerId",
                table: "Payments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SaleId",
                table: "Payments",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeLogs_ChangedBy",
                table: "PriceChangeLogs",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeLogs_ProductId",
                table: "PriceChangeLogs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_ProductId",
                table: "PurchaseItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_PurchaseId",
                table: "PurchaseItems",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductId",
                table: "PurchaseReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseItemId",
                table: "PurchaseReturnItems",
                column: "PurchaseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturnId",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_CreatedBy",
                table: "PurchaseReturns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_PurchaseId",
                table: "PurchaseReturns",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ReturnNo",
                table: "PurchaseReturns",
                column: "ReturnNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_CreatedBy",
                table: "Purchases",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_InvoiceNo",
                table: "Purchases",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_ProductId",
                table: "SaleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItems",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_ProductId",
                table: "SaleReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_SaleItemId",
                table: "SaleReturnItems",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_SaleReturnId",
                table: "SaleReturnItems",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_CreatedBy",
                table: "SaleReturns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_CustomerId",
                table: "SaleReturns",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_ReturnNo",
                table: "SaleReturns",
                column: "ReturnNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_SaleId",
                table: "SaleReturns",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CreatedAt",
                table: "Sales",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CreatedBy",
                table: "Sales",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_DeletedBy",
                table: "Sales",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ExternalReference",
                table: "Sales",
                column: "ExternalReference",
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceNo",
                table: "Sales",
                column: "InvoiceNo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_IsLocked",
                table: "Sales",
                column: "IsLocked");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_LastModifiedBy",
                table: "Sales",
                column: "LastModifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "InvoiceTemplates");

            migrationBuilder.DropTable(
                name: "InvoiceVersions");

            migrationBuilder.DropTable(
                name: "PaymentIdempotencies");

            migrationBuilder.DropTable(
                name: "PriceChangeLogs");

            migrationBuilder.DropTable(
                name: "PurchaseReturnItems");

            migrationBuilder.DropTable(
                name: "SaleReturnItems");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PurchaseItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturns");

            migrationBuilder.DropTable(
                name: "SaleItems");

            migrationBuilder.DropTable(
                name: "SaleReturns");

            migrationBuilder.DropTable(
                name: "Purchases");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
