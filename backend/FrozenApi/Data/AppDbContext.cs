/*
Purpose: Database context for Entity Framework Core
Author: AI Assistant
Date: 2024
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Models;

namespace FrozenApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                return;
            }
            
            // Suppress pending model changes warning during development
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<PriceChangeLog> PriceChangeLogs { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<SaleReturn> SaleReturns { get; set; }
        public DbSet<SaleReturnItem> SaleReturnItems { get; set; }
        public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
        public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; }
        public DbSet<InvoiceVersion> InvoiceVersions { get; set; }
        public DbSet<PaymentIdempotency> PaymentIdempotencies { get; set; }
        public DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }
        public DbSet<Alert> Alerts { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Role).HasConversion<string>();
            });

            // Product configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Sku).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Sku).IsUnique();
                entity.Property(e => e.UnitType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CostPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SellPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.StockQty).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ConversionToBase).HasColumnType("decimal(18,2)");
            // PostgreSQL rowversion (bytea) for optimistic concurrency
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasDefaultValue(new byte[] { 0 });
            });

            // Purchase configuration
            modelBuilder.Entity<Purchase>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InvoiceNo).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.InvoiceNo).IsUnique();
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
            });

            // PurchaseItem configuration
            modelBuilder.Entity<PurchaseItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Qty).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Purchase).WithMany(p => p.Items).HasForeignKey(e => e.PurchaseId);
                entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
            });

            // Sale configuration
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InvoiceNo).IsRequired().HasMaxLength(100);
                // CRITICAL: Unique constraint only for non-deleted invoices
                entity.HasIndex(e => e.InvoiceNo)
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false"); // Allow reuse of invoice numbers after deletion
                entity.Property(e => e.ExternalReference).HasMaxLength(200);
                // PostgreSQL requires quoted column names in filter expressions
                entity.HasIndex(e => e.ExternalReference)
                    .IsUnique()
                    .HasFilter("\"ExternalReference\" IS NOT NULL"); // PostgreSQL/SQLite compatible
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Discount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaymentStatus).HasConversion<string>(); // SalePaymentStatus enum
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsLocked).HasDefaultValue(false);
                entity.Property(e => e.Version).HasDefaultValue(1);
                entity.Property(e => e.EditReason).HasMaxLength(500);
                // Optimistic concurrency control - prevent duplicate saves
                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken()
                    .HasDefaultValue(new byte[] { 0 });
                entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
                entity.HasOne(e => e.LastModifiedByUser).WithMany().HasForeignKey(e => e.LastModifiedBy);
                entity.HasOne(e => e.DeletedByUser).WithMany().HasForeignKey(e => e.DeletedBy);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.IsLocked);
            });
            
            // InvoiceTemplate configuration
            modelBuilder.Entity<InvoiceTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Version).HasMaxLength(50);
                entity.Property(e => e.HtmlCode).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
                entity.HasIndex(e => e.IsActive);
            });

            // Alert configuration
            modelBuilder.Entity<Alert>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Message).HasMaxLength(2000);
                entity.Property(e => e.Severity).HasMaxLength(50);
                entity.Property(e => e.Metadata).HasMaxLength(500);
                entity.HasOne(e => e.ResolvedByUser).WithMany().HasForeignKey(e => e.ResolvedBy).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.IsResolved);
                entity.HasIndex(e => e.CreatedAt);
            });

            // InvoiceVersion configuration
            modelBuilder.Entity<InvoiceVersion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DataJson).IsRequired();
                entity.Property(e => e.EditReason).HasMaxLength(500);
                entity.Property(e => e.DiffSummary).HasMaxLength(1000);
                entity.HasOne(e => e.Sale).WithMany().HasForeignKey(e => e.SaleId);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedById);
                entity.HasIndex(e => e.SaleId);
                entity.HasIndex(e => new { e.SaleId, e.VersionNumber });
            });

            // SaleItem configuration
            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Qty).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Discount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Sale).WithMany(s => s.Items).HasForeignKey(e => e.SaleId);
                entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
            });

            // Customer configuration - Base configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Trn).HasMaxLength(50);
                entity.Property(e => e.Address).HasMaxLength(500);
                
                // CRITICAL: All decimal fields must have defaults and cannot be NULL
                entity.Property(e => e.CreditLimit)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m)
                    .IsRequired();
                entity.Property(e => e.Balance)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m)
                    .IsRequired();
                
                // REAL-TIME BALANCE TRACKING FIELDS (added in migration 20251111120000)
                entity.Property(e => e.TotalSales)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m)
                    .IsRequired();
                entity.Property(e => e.TotalPayments)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m)
                    .IsRequired();
                entity.Property(e => e.PendingBalance)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m)
                    .IsRequired();
                entity.Property(e => e.LastPaymentDate).IsRequired(false);
                entity.Property(e => e.LastActivity).IsRequired(false);
                
                // CRITICAL: RowVersion configuration
                entity.Property(e => e.RowVersion)
                    .HasColumnType("BYTEA") // PostgreSQL bytea type
                    .IsConcurrencyToken()
                    .HasDefaultValue(new byte[] { 0 })
                    .IsRequired(false);
            });

            // Payment configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Mode).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.Reference).HasMaxLength(200);
                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken()
                    .IsRequired(false); // Make nullable for PostgreSQL
                entity.HasOne(e => e.Sale).WithMany().HasForeignKey(e => e.SaleId);
                entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
            });
            
            // PaymentIdempotency configuration (idempotency)
            modelBuilder.Entity<PaymentIdempotency>(entity =>
            {
                entity.HasKey(e => e.IdempotencyKey);
                entity.Property(e => e.IdempotencyKey).HasMaxLength(100);
                entity.Property(e => e.ResponseSnapshot).HasColumnType("text"); // PostgreSQL text type
                entity.HasIndex(e => e.IdempotencyKey).IsUnique();
                entity.HasOne(e => e.Payment).WithMany().HasForeignKey(e => e.PaymentId);
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            });
            
            // Sale - Add paid amount tracking
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                // Ensure TotalAmount = GrandTotal on creation
            });
            
            // Customer - Last activity already configured above

            // ExpenseCategory configuration
            modelBuilder.Entity<ExpenseCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // Expense configuration
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Category).WithMany(c => c.Expenses).HasForeignKey(e => e.CategoryId);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
            });

            // InventoryTransaction configuration
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChangeQty).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TransactionType).HasConversion<string>();
                entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
            });

            // AuditLog configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            });

            // Setting configuration
            modelBuilder.Entity<Setting>(entity =>
            {
                entity.HasKey(e => e.Key);
            });

            // PriceChangeLog configuration
            modelBuilder.Entity<PriceChangeLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OldPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NewPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PriceDifference).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
                entity.HasOne(e => e.ChangedByUser).WithMany().HasForeignKey(e => e.ChangedBy);
            });

            // SaleReturn configuration
            modelBuilder.Entity<SaleReturn>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReturnNo).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.ReturnNo).IsUnique();
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Discount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasOne(e => e.Sale).WithMany().HasForeignKey(e => e.SaleId);
                entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
            });

            // SaleReturnItem configuration
            modelBuilder.Entity<SaleReturnItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Qty).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.SaleReturn).WithMany(s => s.Items).HasForeignKey(e => e.SaleReturnId);
                entity.HasOne(e => e.SaleItem).WithMany().HasForeignKey(e => e.SaleItemId);
                entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
            });

            // PurchaseReturn configuration
            modelBuilder.Entity<PurchaseReturn>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReturnNo).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.ReturnNo).IsUnique();
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasOne(e => e.Purchase).WithMany().HasForeignKey(e => e.PurchaseId);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
            });

            // PurchaseReturnItem configuration
            modelBuilder.Entity<PurchaseReturnItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Qty).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.PurchaseReturn).WithMany(p => p.Items).HasForeignKey(e => e.PurchaseReturnId);
                entity.HasOne(e => e.PurchaseItem).WithMany().HasForeignKey(e => e.PurchaseItemId);
                entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
            });

            // Sale - Add soft delete and edit tracking
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasOne(e => e.LastModifiedByUser).WithMany().HasForeignKey(e => e.LastModifiedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(e => e.DeletedByUser).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
            });

            // Seed data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed admin user
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Name = "Admin",
                    Email = "admin@starplus.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    Role = UserRole.Admin,
                    Phone = "+971 555 298 878",
                    CreatedAt = DateTime.UtcNow
                }
            );

            // Seed default settings
            modelBuilder.Entity<Setting>().HasData(
                new Setting { Key = "VAT_PERCENT", Value = "5" },
                new Setting { Key = "COMPANY_NAME_EN", Value = "Starplus Foodstuff Trading" },
                new Setting { Key = "COMPANY_NAME_AR", Value = "ستار بلس لتجارة المواد الغذائية" },
                new Setting { Key = "COMPANY_ADDRESS", Value = "Mussafah 44, Industrail Area" },
                new Setting { Key = "COMPANY_TRN", Value = "100366253100003" },
                new Setting { Key = "COMPANY_PHONE", Value = "+971 555298878" },
                new Setting { Key = "CURRENCY", Value = "AED" }
            );

            // Seed sample products (STARPLUS FOODSTUFF TRADING - Frozen Chicken Products)
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Sku = "CHK-GRL-001",
                    NameEn = "FROZEN CHICKEN GRILLER 1000GM - QUALIKO",
                    NameAr = "دجاج شواء مجمد 1000جم - كواليكو",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 75.00m,
                    SellPrice = 86.00m,
                    StockQty = 7,
                    ReorderLevel = 3,
                    DescriptionEn = "Frozen chicken griller 1000gm per carton",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 2,
                    Sku = "CHK-GRL-002",
                    NameEn = "FROZEN CHICKEN GRILLERS (10X1200GMS)-FRANGOSUL",
                    NameAr = "دجاج شواء مجمد (10×1200جم) - فرانجوسول",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 85.00m,
                    SellPrice = 99.00m,
                    StockQty = 5,
                    ReorderLevel = 3,
                    DescriptionEn = "Frozen chicken grillers 10x1200gms per carton",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 3,
                    Sku = "CHK-GRL-003",
                    NameEn = "FROZEN CHICKEN GRILLERS (10X900GMS)-CEDROB",
                    NameAr = "دجاج شواء مجمد (10×900جم) - سيدروب",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 60.00m,
                    SellPrice = 70.00m,
                    StockQty = 1,
                    ReorderLevel = 3,
                    DescriptionEn = "Frozen chicken grillers 10x900gms per carton",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 4,
                    Sku = "CHK-BREAST-001",
                    NameEn = "FROZEN CHICKEN BREAST S/L B/L - BARKAT 12KGS",
                    NameAr = "صدر دجاج مجمد بدون جلد/عظم - بركات 12كجم",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 125.00m,
                    SellPrice = 145.00m,
                    StockQty = 1,
                    ReorderLevel = 2,
                    DescriptionEn = "Frozen chicken breast skinless boneless 12kgs",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 5,
                    Sku = "VEG-TAP-001",
                    NameEn = "FROZEN-TAPIOCA CUTS-MALABAR (12X700 GM)",
                    NameAr = "تأبيوكا مقطعة مجمدة - مالابار (12×700 جم)",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 28.00m,
                    SellPrice = 33.00m,
                    StockQty = 4,
                    ReorderLevel = 3,
                    DescriptionEn = "Frozen tapioca cuts 12x700gm per carton",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 6,
                    Sku = "BEEF-BOB-001",
                    NameEn = "FROZEN INDIAN BOBBY VEAL 18KG- PRIME GOLD",
                    NameAr = "لحم عجل هندي بوبي مجمد 18كجم - برايم جولد",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 250.00m,
                    SellPrice = 275.00m,
                    StockQty = 1,
                    ReorderLevel = 1,
                    DescriptionEn = "Frozen Indian bobby veal 18kg per carton",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                // Sample products from the invoices
                new Product
                {
                    Id = 7,
                    Sku = "MEAT-VEAL-001",
                    NameEn = "VEAL LEG AMEEN",
                    NameAr = "لحم عجل ساق أمين",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 250.00m,
                    SellPrice = 305.00m,
                    StockQty = 15,
                    ReorderLevel = 5,
                    DescriptionEn = "Veal leg",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 8,
                    Sku = "SEA-HAMOOR-001",
                    NameEn = "HAMOOR FILLET",
                    NameAr = "هامور فيليه",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 75.00m,
                    SellPrice = 88.00m,
                    StockQty = 20,
                    ReorderLevel = 5,
                    DescriptionEn = "Hamoor fillet",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 9,
                    Sku = "CHK-1200GM-001",
                    NameEn = "1200 GM CKN",
                    NameAr = "دجاج 1200 جم",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 85.00m,
                    SellPrice = 98.00m,
                    StockQty = 30,
                    ReorderLevel = 10,
                    DescriptionEn = "1200 gm chicken",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 10,
                    Sku = "CHK-1300GM-001",
                    NameEn = "1300 GM CKN",
                    NameAr = "دجاج 1300 جم",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 95.00m,
                    SellPrice = 112.00m,
                    StockQty = 25,
                    ReorderLevel = 10,
                    DescriptionEn = "1300 gm chicken",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 11,
                    Sku = "CHK-BREAST-12KG",
                    NameEn = "BREAST 12KG",
                    NameAr = "صدر 12 كجم",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 150.00m,
                    SellPrice = 179.00m,
                    StockQty = 12,
                    ReorderLevel = 5,
                    DescriptionEn = "Chicken breast 12kg",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 12,
                    Sku = "DAIRY-BUTTER-001",
                    NameEn = "MUMTAZ BUTTER",
                    NameAr = "زبدة ممتاز",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 45.00m,
                    SellPrice = 56.00m,
                    StockQty = 40,
                    ReorderLevel = 10,
                    DescriptionEn = "Mumtaz butter",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 13,
                    Sku = "CHK-BREAST-AROURA",
                    NameEn = "BREAST 12KG AROURA",
                    NameAr = "صدر 12 كجم أورورا",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 115.00m,
                    SellPrice = 140.00m,
                    StockQty = 18,
                    ReorderLevel = 5,
                    DescriptionEn = "Chicken breast 12kg aroura",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 14,
                    Sku = "MEAT-MINCE-001",
                    NameEn = "MEAT MINCE",
                    NameAr = "لحم مفروم",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 150.00m,
                    SellPrice = 180.00m,
                    StockQty = 15,
                    ReorderLevel = 5,
                    DescriptionEn = "Meat mince",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 15,
                    Sku = "CHK-1000GM-001",
                    NameEn = "1000GM CKN",
                    NameAr = "دجاج 1000 جم",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 75.00m,
                    SellPrice = 88.00m,
                    StockQty = 35,
                    ReorderLevel = 10,
                    DescriptionEn = "1000 gm chicken",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new Product
                {
                    Id = 16,
                    Sku = "FOOD-HOTDOG-001",
                    NameEn = "ROSE HOTDOG",
                    NameAr = "هوت دوغ روز",
                    UnitType = "CRTN",
                    ConversionToBase = 1,
                    CostPrice = 85.00m,
                    SellPrice = 103.00m,
                    StockQty = 45,
                    ReorderLevel = 10,
                    DescriptionEn = "Rose hotdog",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                }
            );

            // Seed default expense categories
            modelBuilder.Entity<ExpenseCategory>().HasData(
                new ExpenseCategory { Id = 1, Name = "Rent", ColorCode = "#EF4444", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 2, Name = "Utilities", ColorCode = "#F59E0B", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 3, Name = "Staff Salary", ColorCode = "#3B82F6", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 4, Name = "Marketing", ColorCode = "#8B5CF6", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 5, Name = "Fuel", ColorCode = "#14B8A6", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 6, Name = "Delivery", ColorCode = "#F97316", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 7, Name = "Food", ColorCode = "#EC4899", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 8, Name = "Maintenance", ColorCode = "#6366F1", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 9, Name = "Insurance", ColorCode = "#10B981", CreatedAt = DateTime.UtcNow },
                new ExpenseCategory { Id = 10, Name = "Other", ColorCode = "#6B7280", CreatedAt = DateTime.UtcNow }
            );
        }
    }
}

