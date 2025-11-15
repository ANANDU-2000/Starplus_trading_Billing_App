/*
Purpose: Admin Reset Service - Safely reset transactional data with backup option
Author: AI Assistant
Date: 2025
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IResetService
    {
        Task<ResetResult> ResetSystemAsync(bool createBackup, bool clearAuditLogs, int userId);
        Task<SystemSummary> GetSystemSummaryAsync();
    }

    public class ResetService : IResetService
    {
        private readonly AppDbContext _context;
        private readonly IComprehensiveBackupService _backupService;

        public ResetService(AppDbContext context, IComprehensiveBackupService backupService)
        {
            _context = context;
            _backupService = backupService;
        }

        public async Task<ResetResult> ResetSystemAsync(bool createBackup, bool clearAuditLogs, int userId)
        {
            var result = new ResetResult
            {
                Success = false,
                Message = "",
                BackupCreated = false,
                BackupFilePath = null
            };

            try
            {
                // Step 1: Create backup if requested
                if (createBackup)
                {
                    try
                    {
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var backupFileName = $"BeforeReset_{timestamp}.zip";
                        
                        // Create backup to desktop and server
                        await _backupService.CreateFullBackupAsync(exportToDesktop: true, uploadToGoogleDrive: false, sendEmail: false);
                        
                        result.BackupCreated = true;
                        result.BackupFilePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                            "StarPlus_Backups",
                            backupFileName
                        );
                        
                        Console.WriteLine($"✅ Backup created before reset: {result.BackupFilePath}");
                    }
                    catch (Exception backupEx)
                    {
                        // Don't fail reset if backup fails, but warn admin
                        result.Message = $"Warning: Backup failed: {backupEx.Message}. Continue with reset?";
                        Console.WriteLine($"⚠️ Backup failed: {backupEx.Message}");
                    }
                }

                // Step 2: Get counts before deletion (for reporting)
                var summaryBefore = await GetSystemSummaryAsync();

                // Step 3: Delete all transactional data
                // Sales and Sale Items
                var salesCount = await _context.Sales.Where(s => !s.IsDeleted).ExecuteDeleteAsync();
                var saleItemsCount = await _context.SaleItems.ExecuteDeleteAsync();
                
                // Payments
                var paymentsCount = await _context.Payments.ExecuteDeleteAsync();
                
                // Expenses
                var expensesCount = await _context.Expenses.ExecuteDeleteAsync();
                
                // Inventory Transactions
                var inventoryTxCount = await _context.InventoryTransactions.ExecuteDeleteAsync();
                
                // Sales Returns
                var salesReturnsCount = await _context.SaleReturns.ExecuteDeleteAsync();
                
                // Purchase Returns
                var purchaseReturnsCount = await _context.PurchaseReturns.ExecuteDeleteAsync();
                
                // Purchases
                var purchasesCount = await _context.Purchases.ExecuteDeleteAsync();

                // Step 4: Reset stock quantities to 0 (keep products)
                var productsUpdated = await _context.Products
                    .Where(p => p.StockQty != 0)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.StockQty, 0));

                // Step 5: Reset customer balances to 0 (keep customers)
                var customersUpdated = await _context.Customers
                    .Where(c => c.Balance != 0)
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.Balance, 0));

                // Step 6: Optional - Clear audit logs
                int auditLogsCount = 0;
                if (clearAuditLogs)
                {
                    auditLogsCount = await _context.AuditLogs.ExecuteDeleteAsync();
                }

                // Step 7: Create audit log entry for the reset action
                var resetAuditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "SYSTEM_RESET",
                    Details = $"System reset executed. Deleted: {salesCount} sales, {paymentsCount} payments, {expensesCount} expenses, {inventoryTxCount} inventory transactions. Backup: {(result.BackupCreated ? "Yes" : "No")}. Audit logs cleared: {clearAuditLogs}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(resetAuditLog);

                await _context.SaveChangesAsync();

                result.Success = true;
                result.Message = $"System reset completed successfully. " +
                    $"Deleted: {salesCount} sales, {saleItemsCount} sale items, {paymentsCount} payments, {expensesCount} expenses, " +
                    $"{inventoryTxCount} inventory transactions, {salesReturnsCount} sales returns, {purchaseReturnsCount} purchase returns, {purchasesCount} purchases. " +
                    $"Reset: {productsUpdated} products (stock to 0), {customersUpdated} customers (balance to 0). " +
                    $"{(clearAuditLogs ? $"Cleared {auditLogsCount} audit logs." : "Audit logs preserved.")}";

                Console.WriteLine($"✅ System reset completed by user {userId}");
                Console.WriteLine($"   Summary: {result.Message}");

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Reset failed: {ex.Message}";
                Console.WriteLine($"❌ System reset failed: {ex.Message}");
                return result;
            }
        }

        public async Task<SystemSummary> GetSystemSummaryAsync()
        {
            return new SystemSummary
            {
                TotalSales = await _context.Sales.Where(s => !s.IsDeleted).CountAsync(),
                TotalSaleItems = await _context.SaleItems.CountAsync(),
                TotalPayments = await _context.Payments.CountAsync(),
                TotalExpenses = await _context.Expenses.CountAsync(),
                TotalInventoryTransactions = await _context.InventoryTransactions.CountAsync(),
                TotalSalesReturns = await _context.SaleReturns.CountAsync(),
                TotalPurchaseReturns = await _context.PurchaseReturns.CountAsync(),
                TotalPurchases = await _context.Purchases.CountAsync(),
                TotalProducts = await _context.Products.CountAsync(),
                TotalCustomers = await _context.Customers.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalAuditLogs = await _context.AuditLogs.CountAsync(),
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    public class ResetResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool BackupCreated { get; set; }
        public string? BackupFilePath { get; set; }
    }

    public class SystemSummary
    {
        public int TotalSales { get; set; }
        public int TotalSaleItems { get; set; }
        public int TotalPayments { get; set; }
        public int TotalExpenses { get; set; }
        public int TotalInventoryTransactions { get; set; }
        public int TotalSalesReturns { get; set; }
        public int TotalPurchaseReturns { get; set; }
        public int TotalPurchases { get; set; }
        public int TotalProducts { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalUsers { get; set; }
        public int TotalAuditLogs { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}

