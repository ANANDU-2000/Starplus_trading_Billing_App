/*
Purpose: Program.cs - Main entry point for ASP.NET Core application
Author: AI Assistant
Date: 2024
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Services;
using FrozenApi.Helpers;
using FrozenApi.Models;
using BCrypt.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure logging early for better visibility
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);
// Suppress noisy EF Core command logging (only show warnings and errors)
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

// Create logger for startup logging
var logger = LoggerFactory.Create(config => config.AddConsole().AddDebug()).CreateLogger("Startup");

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PostgreSQL Database Configuration
// Support both Render's DATABASE_URL and standard connection strings
// Priority: Environment variables > appsettings.json (for production deployment)
string? connectionString = null;

// Check environment variables FIRST (for Render deployment)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

logger.LogInformation("Checking connection string sources...");
logger.LogInformation("DATABASE_URL env var: {HasDatabaseUrl}", !string.IsNullOrWhiteSpace(databaseUrl));
logger.LogInformation("ConnectionStrings__DefaultConnection env var: {HasEnvConnection}", !string.IsNullOrWhiteSpace(envConnectionString));

// Priority 1: ConnectionStrings__DefaultConnection environment variable
if (!string.IsNullOrWhiteSpace(envConnectionString))
{
    connectionString = envConnectionString;
    logger.LogInformation("✅ Using ConnectionStrings__DefaultConnection from environment");
}
// Priority 2: DATABASE_URL from Render
else if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    try
    {
        logger.LogInformation("Parsing DATABASE_URL: {UrlPrefix}", databaseUrl.Substring(0, Math.Min(20, databaseUrl.Length)) + "...");
        
        // Remove trailing ? if present
        var cleanUrl = databaseUrl.TrimEnd('?');
        var uri = new Uri(cleanUrl);
        
        // Use default PostgreSQL port (5432) if not specified
        var dbPort = uri.Port > 0 ? uri.Port : 5432;
        
        connectionString = $"Host={uri.Host};Port={dbPort};Database={uri.AbsolutePath.TrimStart('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true";
        logger.LogInformation("✅ Successfully parsed DATABASE_URL from Render");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to parse DATABASE_URL: {Message}", ex.Message);
        throw new InvalidOperationException("Invalid DATABASE_URL format", ex);
    }
}
// Priority 3: appsettings.json (for local development)
else
{
    var appSettingsConnection = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? builder.Configuration.GetConnectionString("PostgreSQL");
    
    if (!string.IsNullOrWhiteSpace(appSettingsConnection))
    {
        connectionString = appSettingsConnection;
        logger.LogInformation("✅ Using connection string from appsettings.json");
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    logger.LogError("❌ CRITICAL: No PostgreSQL connection string available!");
    logger.LogError("Please set one of:");
    logger.LogError("  - ConnectionStrings__DefaultConnection environment variable (Render)");
    logger.LogError("  - DATABASE_URL environment variable (Render)");
    logger.LogError("  - DefaultConnection in appsettings.json (Local)");
    throw new InvalidOperationException("PostgreSQL connection string is required.");
}

logger.LogInformation("Using PostgreSQL database");

// CRITICAL: Configure Npgsql to handle DateTime properly
// This prevents "Cannot write DateTime with Kind=Unspecified" errors
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // REMOVED EnableRetryOnFailure - incompatible with manual transactions (BeginTransaction)
        // Manual transactions are used in SaleService, ReturnService, etc.
        npgsqlOptions.CommandTimeout(60);
    });
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
var passwordPart = connectionString.Split(';').FirstOrDefault(x => x.Contains("Password", StringComparison.OrdinalIgnoreCase));
var safeConnectionString = !string.IsNullOrEmpty(passwordPart) 
    ? connectionString.Replace(passwordPart, "Password=****") 
    : connectionString;
logger.LogInformation("PostgreSQL connection: {ConnectionString}", safeConnectionString);

// Security Services
builder.Services.AddSecurityServices(builder.Configuration);

// Services
builder.Services.AddSingleton<IFontService, FontService>(); // Singleton for font registration
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddScoped<IInvoiceTemplateService, InvoiceTemplateService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IComprehensiveBackupService, ComprehensiveBackupService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IProfitService, ProfitService>();
builder.Services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IProductSeedService, ProductSeedService>();
builder.Services.AddScoped<IResetService, ResetService>();
builder.Services.AddScoped<IInvoiceNumberService, InvoiceNumberService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<IBalanceService, BalanceService>();
builder.Services.AddSingleton<ITimeZoneService, TimeZoneService>(); // Gulf Standard Time (GST, UTC+4)

// Background services
builder.Services.AddHostedService<DailyBackupScheduler>();
builder.Services.AddHostedService<AlertCheckBackgroundService>();

var app = builder.Build();

// Get logger from app services
var appLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Application");

// Initialize fonts early at startup
appLogger.LogInformation("Initializing font registration...");
var fontService = app.Services.GetRequiredService<IFontService>();
fontService.RegisterFonts();
appLogger.LogInformation("Font registration completed. Arabic font: {Font}", fontService.GetArabicFontFamily());

// Configure URLs - Support Render deployment (PORT env var) and local development
app.Urls.Clear();
var serverPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(serverPort) && int.TryParse(serverPort, out int portNumber))
{
    // Render deployment - bind to 0.0.0.0:PORT
    app.Urls.Add($"http://0.0.0.0:{portNumber}");
    appLogger.LogInformation("Server configured to listen on port {Port} (0.0.0.0:{Port})", portNumber, portNumber);
}
else
{
    // Local development - use default port
    app.Urls.Add("http://localhost:5000");
    appLogger.LogInformation("Server configured to listen on http://localhost:5000");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve static files from wwwroot/uploads (for logo and other uploads)
var uploadsPath = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// Only enforce HTTPS in non-development environments to simplify local dev
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// CORS MUST be before authentication/authorization
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

// Security middleware (includes rate limiting and security headers)
app.UseSecurityMiddleware(app.Environment);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// CORS diagnostic endpoint (anonymous for debugging)
app.MapGet("/api/cors-check", (HttpContext context) =>
{
    var allowedOriginsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
    var allowedOriginsConfig = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
    
    return new
    {
        corsEnabled = true,
        environment = builder.Environment.EnvironmentName,
        corsPolicy = builder.Environment.IsDevelopment() ? "Development" : "Production",
        envVariable = allowedOriginsEnv ?? "Not Set",
        configOrigins = allowedOriginsConfig ?? Array.Empty<string>(),
        requestOrigin = context.Request.Headers["Origin"].ToString(),
        timestamp = DateTime.UtcNow
    };
}).AllowAnonymous();

// Database initialization - run in background, don't block server startup
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // Wait 2 seconds for server to start
    using (var scope = app.Services.CreateScope())
    {
        var initLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInit");
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            // PostgreSQL - no special initialization needed
            
            // Create performance indexes for large datasets (100K+ records)
            try
            {
                initLogger.LogInformation("Creating performance indexes...");
                
                // Try multiple paths to find the SQL file (works in both local and Docker environments)
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var possiblePaths = new[]
                {
                    Path.Combine(baseDirectory, "Migrations", "AddPerformanceIndexes.sql"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Migrations", "AddPerformanceIndexes.sql"),
                    Path.Combine(baseDirectory, "..", "Migrations", "AddPerformanceIndexes.sql"),
                    Path.Combine(baseDirectory, "..", "..", "Migrations", "AddPerformanceIndexes.sql")
                };
                
                string? indexSql = null;
                string? foundPath = null;
                
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        foundPath = path;
                        indexSql = await File.ReadAllTextAsync(path);
                        break;
                    }
                }
                
                if (!string.IsNullOrEmpty(indexSql))
                {
                    initLogger.LogInformation("Found index SQL file at: {Path}", foundPath);
                    // Execute each CREATE INDEX statement separately (SQLite doesn't support multi-statement in one call)
                    var statements = indexSql.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s) && s.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase));
                    
                    foreach (var statement in statements)
                    {
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync(statement);
                        }
                        catch (Exception idxEx)
                        {
                            // Index might already exist, ignore
                            if (!idxEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                            {
                                initLogger.LogWarning(idxEx, "Index creation warning");
                            }
                        }
                    }
                    initLogger.LogInformation("Performance indexes created/verified");
                }
                else
                {
                    initLogger.LogWarning("Index SQL file not found. Searched paths: {Paths}", string.Join(", ", possiblePaths));
                }
            }
            catch (Exception idxEx)
            {
                initLogger.LogWarning(idxEx, "Index creation skipped (file not found or error)");
            }
            
            // Apply pending migrations FIRST (before any operations)
            try
            {
                initLogger.LogInformation("Checking for pending migrations...");
                var pending = context.Database.GetPendingMigrations().ToList();
                if (pending.Any())
                {
                    initLogger.LogInformation("Found {Count} pending migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
                    initLogger.LogInformation("Applying migrations...");
                    
                    // Use async method properly
                    await context.Database.MigrateAsync();
                    initLogger.LogInformation("Database migrations applied successfully");
                }
                else
                {
                    initLogger.LogInformation("All migrations are up to date");
                }
            }
            catch (Exception ex)
            {
                initLogger.LogError(ex, "Migration error: {Message}", ex.Message);
                if (ex.InnerException != null)
                {
                    initLogger.LogError("Inner exception: {Message}", ex.InnerException.Message);
                }
                
                // DatabaseFixer is SQLite-specific - skip for PostgreSQL
                // PostgreSQL schema is managed entirely through EF Core migrations
                if (!context.Database.IsNpgsql())
                {
                    initLogger.LogInformation("Attempting to fix missing columns...");
                    try
                    {
                        await FrozenApi.Helpers.DatabaseFixer.FixMissingColumnsAsync(context);
                    }
                    catch (Exception fixEx)
                    {
                        initLogger.LogWarning(fixEx, "Column fix failed");
                    }
                }
            }
            
            // CRITICAL: PostgreSQL Production Schema Validation
            if (context.Database.IsNpgsql())
            {
                try
                {
                    initLogger.LogInformation("Validating PostgreSQL schema...");
                    
                    // Check if critical columns exist in Customers table
                    var checkCustomerColumns = @"
                        SELECT 
                            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Customers' AND column_name = 'TotalSales') AS has_total_sales,
                            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Customers' AND column_name = 'TotalPayments') AS has_total_payments,
                            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Customers' AND column_name = 'PendingBalance') AS has_pending_balance,
                            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Customers' AND column_name = 'Balance') AS has_balance,
                            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Customers' AND column_name = 'CreditLimit') AS has_credit_limit";
                    
                    using (var command = context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = checkCustomerColumns;
                        await context.Database.OpenConnectionAsync();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                bool hasTotalSales = reader.GetBoolean(0);
                                bool hasTotalPayments = reader.GetBoolean(1);
                                bool hasPendingBalance = reader.GetBoolean(2);
                                bool hasBalance = reader.GetBoolean(3);
                                bool hasCreditLimit = reader.GetBoolean(4);
                                
                                if (!hasTotalSales || !hasTotalPayments || !hasPendingBalance || !hasBalance || !hasCreditLimit)
                                {
                                    initLogger.LogError("❌ CRITICAL: Missing columns in Customers table!");
                                    initLogger.LogError("   TotalSales: {HasTotalSales}", hasTotalSales);
                                    initLogger.LogError("   TotalPayments: {HasTotalPayments}", hasTotalPayments);
                                    initLogger.LogError("   PendingBalance: {HasPendingBalance}", hasPendingBalance);
                                    initLogger.LogError("   Balance: {HasBalance}", hasBalance);
                                    initLogger.LogError("   CreditLimit: {HasCreditLimit}", hasCreditLimit);
                                    initLogger.LogError("");
                                    initLogger.LogError("⚠️  DATABASE SCHEMA IS INCOMPLETE!");
                                    initLogger.LogError("⚠️  Please run: backend/FrozenApi/Scripts/ApplyRenderDatabaseFix.ps1");
                                    initLogger.LogError("⚠️  Or manually execute: backend/FrozenApi/Scripts/FixProductionDatabase.sql");
                                    initLogger.LogError("");
                                }
                                else
                                {
                                    initLogger.LogInformation("✅ PostgreSQL schema validation passed");
                                }
                            }
                        }
                        await context.Database.CloseConnectionAsync();
                    }
                }
                catch (Exception schemaEx)
                {
                    initLogger.LogWarning(schemaEx, "PostgreSQL schema validation failed");
                }
            }
            
            // PostgreSQL Alerts table is created via migrations
            
            // DatabaseFixer is SQLite-specific - SKIP for PostgreSQL
            // PostgreSQL schema is managed entirely through EF Core migrations
            if (!context.Database.IsNpgsql())
            {
                // ALWAYS run column fixer as safety net (handles existing columns gracefully)
                // This ensures all required columns exist even if migrations fail
                // Note: This may log "fail" messages for columns that already exist - this is normal and expected
                try
                {
                    initLogger.LogInformation("Running database column fixer (this may show 'fail' logs for existing columns - this is normal)...");
                    await FrozenApi.Helpers.DatabaseFixer.FixMissingColumnsAsync(context);
                    initLogger.LogInformation("Database column fixer completed");
                }
                catch (Exception ex)
                {
                    initLogger.LogError(ex, "Column fixer error: {Message}", ex.Message);
                    if (ex.InnerException != null)
                    {
                        initLogger.LogError("Inner exception: {Message}", ex.InnerException.Message);
                    }
                }
            }
            else
            {
                initLogger.LogInformation("Skipping DatabaseFixer for PostgreSQL (migrations handle schema)");
            }
            
            // Check if database can connect (after migrations)
            if (!context.Database.CanConnect())
            {
                initLogger.LogWarning("Cannot connect to database after migrations. This may indicate a problem.");
            }
            else
            {
                initLogger.LogInformation("Database connection verified");
            }

            // ALWAYS seed/update default users - critical for deployment
            // This ensures admin user exists with correct password even if migrations seeded it differently
            try
            {
                initLogger.LogInformation("Ensuring default users exist with correct passwords...");
                var allUsers = await context.Users.ToListAsync();
                var adminEmail = "admin@starplus.com".ToLowerInvariant();
                var staffEmail = "staff@starplus.com".ToLowerInvariant();
                
                // Admin user - create or update
                var adminUser = allUsers.FirstOrDefault(u => (u.Email ?? string.Empty).Trim().ToLowerInvariant() == adminEmail);
                var correctAdminPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
                
                if (adminUser == null)
                {
                    // Create new admin user
                    adminUser = new User
                    {
                        Name = "Admin",
                        Email = "admin@starplus.com",
                        PasswordHash = correctAdminPasswordHash,
                        Role = UserRole.Admin,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(adminUser);
                    initLogger.LogInformation("Created default admin user");
                }
                else
                {
                    // Update existing admin user password to ensure it's correct
                    var testPassword = BCrypt.Net.BCrypt.Verify("Admin123!", adminUser.PasswordHash);
                    if (!testPassword)
                    {
                        adminUser.PasswordHash = correctAdminPasswordHash;
                        initLogger.LogInformation("Updated admin user password to ensure correct hash");
                    }
                    else
                    {
                        initLogger.LogInformation("Admin user exists with correct password");
                    }
                }

                // Staff user - create or update
                var staffUser = allUsers.FirstOrDefault(u => (u.Email ?? string.Empty).Trim().ToLowerInvariant() == staffEmail);
                var correctStaffPasswordHash = BCrypt.Net.BCrypt.HashPassword("Staff123!");
                
                if (staffUser == null)
                {
                    // Create new staff user
                    staffUser = new User
                    {
                        Name = "Staff",
                        Email = "staff@starplus.com",
                        PasswordHash = correctStaffPasswordHash,
                        Role = UserRole.Staff,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(staffUser);
                    initLogger.LogInformation("Created default staff user");
                }
                else
                {
                    // Update existing staff user password to ensure it's correct
                    var testPassword = BCrypt.Net.BCrypt.Verify("Staff123!", staffUser.PasswordHash);
                    if (!testPassword)
                    {
                        staffUser.PasswordHash = correctStaffPasswordHash;
                        initLogger.LogInformation("Updated staff user password to ensure correct hash");
                    }
                    else
                    {
                        initLogger.LogInformation("Staff user exists with correct password");
                    }
                }

                // Save all changes
                await context.SaveChangesAsync();
                
                // Verify admin user can login - reload users after save to get updated data
                var updatedUsers = await context.Users.ToListAsync();
                var verifyAdmin = updatedUsers.FirstOrDefault(u => 
                    (u.Email ?? string.Empty).Trim().ToLowerInvariant() == adminEmail);
                if (verifyAdmin != null)
                {
                    var canLogin = BCrypt.Net.BCrypt.Verify("Admin123!", verifyAdmin.PasswordHash);
                    if (canLogin)
                    {
                        initLogger.LogInformation("✅ Admin user verified - login should work with: admin@starplus.com / Admin123!");
                    }
                    else
                    {
                        initLogger.LogError("❌ Admin user password verification failed - this is a critical error!");
                    }
                }
                else
                {
                    initLogger.LogError("❌ Admin user not found after seeding - this is a critical error!");
                }
            }
            catch (Exception ex)
            {
                initLogger.LogError(ex, "❌ CRITICAL: User seeding failed - admin login will not work!");
            }

            // CRITICAL: Sync invoice sequence with existing data (PostgreSQL only)
            if (context.Database.IsNpgsql())
            {
                try
                {
                    initLogger.LogInformation("Syncing invoice number sequence with existing data...");
                    
                    // Get the highest invoice number from Sales table
                    var maxInvoiceQuery = @"
                        SELECT COALESCE(MAX(CAST(""InvoiceNo"" AS INTEGER)), 2000) 
                        FROM ""Sales"" 
                        WHERE ""IsDeleted"" = false 
                        AND ""InvoiceNo"" ~ '^[0-9]+$'";
                    
                    using (var command = context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = maxInvoiceQuery;
                        if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                        {
                            await context.Database.OpenConnectionAsync();
                        }
                        
                        var maxInvoice = await command.ExecuteScalarAsync();
                        if (maxInvoice != null && int.TryParse(maxInvoice.ToString(), out int maxNum))
                        {
                            // Set sequence to max + 1
                            var nextValue = maxNum + 1;
                            var syncSequenceQuery = $"SELECT setval('invoice_number_seq', {nextValue});";
                            
                            using (var syncCommand = context.Database.GetDbConnection().CreateCommand())
                            {
                                syncCommand.CommandText = syncSequenceQuery;
                                await syncCommand.ExecuteScalarAsync();
                                initLogger.LogInformation("✅ Invoice sequence synced: Current max = {MaxInvoice}, Next will be = {NextValue}", maxNum, nextValue);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    initLogger.LogWarning(ex, "Invoice sequence sync failed (non-critical)");
                }
            }

            // Seed products from Excel files (if database is empty or has few products)
            try
            {
                initLogger.LogInformation("Checking if product seeding is needed...");
                var productSeedService = scope.ServiceProvider.GetRequiredService<IProductSeedService>();
                await productSeedService.SeedProductsFromExcelAsync();
                initLogger.LogInformation("Product seeding check completed");
            }
            catch (Exception ex)
            {
                initLogger.LogWarning(ex, "Product seeding failed (non-critical - products can be imported manually)");
            }
        }
        catch (Exception ex)
        {
            initLogger.LogError(ex, "Database initialization error");
        }
    }
});

// Start the server
appLogger.LogInformation("Starting server...");
appLogger.LogInformation("Swagger UI available at: {SwaggerUrl}", app.Urls.FirstOrDefault() + "/swagger");
app.Run();


