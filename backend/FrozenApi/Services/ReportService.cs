/*
Purpose: Report service for analytics and AI suggestions
Author: AI Assistant
Date: 2024
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IReportService
    {
        Task<SummaryReportDto> GetSummaryReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<PagedResponse<SaleDto>> GetSalesReportAsync(DateTime fromDate, DateTime toDate, int? customerId = null, string? status = null, int page = 1, int pageSize = 10);
        Task<EnhancedSalesReportDto> GetEnhancedSalesReportAsync(DateTime fromDate, DateTime toDate, string granularity = "day", int? productId = null, int? customerId = null, string? status = null, int page = 1, int pageSize = 50);
        Task<List<ProductSalesDto>> GetProductSalesReportAsync(DateTime fromDate, DateTime toDate, int top = 20);
        Task<List<ProductSalesDto>> GetEnhancedProductSalesReportAsync(DateTime fromDate, DateTime toDate, int? productId = null, string? unitType = null, bool lowStockOnly = false);
        Task<List<CustomerDto>> GetOutstandingCustomersAsync(int days = 30);
        Task<CustomerReportDto> GetCustomerReportAsync(DateTime fromDate, DateTime toDate, decimal? minOutstanding = null);
        Task<List<PaymentDto>> GetChequeReportAsync();
        Task<AISuggestionsDto> GetAISuggestionsAsync(int periodDays = 30);
        Task<List<PendingBillDto>> GetPendingBillsAsync(DateTime? fromDate = null, DateTime? toDate = null, int? customerId = null, string? search = null, string? status = null);
        Task<AgingReportDto> GetAgingReportAsync(DateTime asOfDate, int? customerId = null);
        Task<StockReportDto> GetStockReportAsync(bool lowOnly = false);
        Task<List<ExpenseByCategoryDto>> GetExpensesByCategoryAsync(DateTime fromDate, DateTime toDate);
        Task<List<SalesVsExpensesDto>> GetSalesVsExpensesAsync(DateTime fromDate, DateTime toDate, string groupBy = "day");
        Task<SalesLedgerReportDto> GetComprehensiveSalesLedgerAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }

    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;

        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SummaryReportDto> GetSummaryReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var today = DateTime.Today;
                DateTime from;
                DateTime to;
                
                // Handle date parsing - fromDate and toDate might be DateTime objects or might need parsing
                if (fromDate.HasValue)
                {
                    from = fromDate.Value.Date; // Use only the date part
                }
                else
                {
                    from = today;
                }
                
                if (toDate.HasValue)
                {
                    to = toDate.Value.Date.AddDays(1); // Include the entire day
                }
                else
                {
                    to = today.AddDays(1);
                }
                
                Console.WriteLine($"📊 GetSummaryReportAsync called with fromDate: {from:yyyy-MM-dd}, toDate: {to:yyyy-MM-dd}");
                Console.WriteLine($"📊 Date range: {from:yyyy-MM-dd HH:mm:ss} to {to:yyyy-MM-dd HH:mm:ss}");

                decimal salesToday = 0;
                decimal purchasesToday = 0;
                decimal expensesToday = 0;

                try
                {
                    var salesQuery = _context.Sales
                        .Where(s => !s.IsDeleted && s.InvoiceDate >= from && s.InvoiceDate < to);
                    var salesCount = await salesQuery.CountAsync();
                    Console.WriteLine($"📈 Found {salesCount} sales records in date range");
                    salesToday = await salesQuery.SumAsync(s => (decimal?)s.GrandTotal) ?? 0;
                    Console.WriteLine($"💰 Total sales today: {salesToday}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error calculating salesToday: {ex.Message}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                    salesToday = 0;
                }

                try
                {
                    var purchasesQuery = _context.Purchases
                        .Where(p => p.PurchaseDate >= from && p.PurchaseDate < to);
                    var purchasesCount = await purchasesQuery.CountAsync();
                    Console.WriteLine($"📦 Found {purchasesCount} purchase records in date range");
                    purchasesToday = await purchasesQuery.SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
                    Console.WriteLine($"💰 Total purchases today: {purchasesToday}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error calculating purchasesToday: {ex.Message}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                    purchasesToday = 0;
                }

                try
                {
                    var expensesQuery = _context.Expenses
                        .Where(e => e.Date >= from && e.Date < to);
                    var expensesCount = await expensesQuery.CountAsync();
                    Console.WriteLine($"💸 Found {expensesCount} expense records in date range");
                    expensesToday = await expensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0;
                    Console.WriteLine($"💰 Total expenses today: {expensesToday}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error calculating expensesToday: {ex.Message}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                    expensesToday = 0;
                }

                // Calculate profit correctly: Sales - COGS - Expenses
                // Get COGS for today's sales
                var salesItems = await _context.SaleItems
                    .Include(si => si.Product)
                    .Include(si => si.Sale)
                    .Where(si => !si.Sale.IsDeleted && si.Sale.InvoiceDate >= from && si.Sale.InvoiceDate < to)
                    .ToListAsync();

                var costOfGoodsSold = salesItems.Sum(si => si.Qty * si.Product.CostPrice);
                var grossProfit = salesToday - costOfGoodsSold;
                var profitToday = grossProfit - expensesToday;

                List<ProductDto> lowStockProducts = new List<ProductDto>();
                try
                {
                    // Load data first, then order in memory to avoid SQLite decimal ORDER BY issues
                    var products = await _context.Products
                        .Where(p => p.StockQty <= p.ReorderLevel)
                        .Select(p => new ProductDto
                        {
                            Id = p.Id,
                            Sku = p.Sku,
                            NameEn = p.NameEn,
                            NameAr = p.NameAr,
                            UnitType = p.UnitType,
                            ConversionToBase = p.ConversionToBase,
                            CostPrice = p.CostPrice,
                            SellPrice = p.SellPrice,
                            StockQty = p.StockQty,
                            ReorderLevel = p.ReorderLevel,
                            DescriptionEn = p.DescriptionEn,
                            DescriptionAr = p.DescriptionAr
                        })
                        .ToListAsync();
                    
                    lowStockProducts = products
                        .OrderBy(p => p.StockQty)
                        .Take(10)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading low stock products: {ex.Message}");
                    lowStockProducts = new List<ProductDto>();
                }

                List<SaleDto> pendingInvoices = new List<SaleDto>();
                int pendingBillsCount = 0;
                decimal pendingBillsAmount = 0;
                int paidBillsCount = 0;
                decimal paidBillsAmount = 0;
                try
                {
                    // CRITICAL: Get ALL pending bills - sales with outstanding balance > 0
                    // Use actual balance calculation: GrandTotal - PaidAmount
                    var allSales = await _context.Sales
                        .Where(s => !s.IsDeleted)
                        .ToListAsync();
                    
                    // Calculate pending bills from ALL customers
                    var pendingSales = allSales
                        .Where(s => {
                            var balance = s.GrandTotal - s.PaidAmount;
                            return balance > 0.01m; // Allow 0.01 rounding tolerance
                        })
                        .ToList();
                    
                    pendingBillsCount = pendingSales.Count;
                    pendingBillsAmount = pendingSales.Sum(s => s.GrandTotal - s.PaidAmount);
                    
                    // Calculate paid bills from ALL customers
                    var paidSales = allSales
                        .Where(s => {
                            var balance = s.GrandTotal - s.PaidAmount;
                            return balance <= 0.01m; // Fully paid (within rounding tolerance)
                        })
                        .ToList();
                    
                    paidBillsCount = paidSales.Count;
                    paidBillsAmount = paidSales.Sum(s => s.GrandTotal);
                    
                    Console.WriteLine($"📊 Pending Bills: {pendingBillsCount} invoices, Amount: {pendingBillsAmount:C}");
                    Console.WriteLine($"✅ Paid Bills: {paidBillsCount} invoices, Amount: {paidBillsAmount:C}");
                    
                    // Get pending invoices for display (with customer info)
                    // CRITICAL: Get pending invoices with actual balance calculation
                    pendingInvoices = await (from s in _context.Sales
                                            join c in _context.Customers on s.CustomerId equals c.Id into customerGroup
                                            from c in customerGroup.DefaultIfEmpty()
                                            where !s.IsDeleted && 
                                                  (s.GrandTotal - s.PaidAmount) > 0.01m // Actual balance > 0 (all customers)
                                            orderby s.InvoiceDate descending
                                            select new SaleDto
                                            {
                                                Id = s.Id,
                                                InvoiceNo = s.InvoiceNo,
                                                InvoiceDate = s.InvoiceDate,
                                                CustomerId = s.CustomerId,
                                                CustomerName = c != null ? c.Name : null,
                                                Subtotal = s.Subtotal,
                                                VatTotal = s.VatTotal,
                                                Discount = s.Discount,
                                                GrandTotal = s.GrandTotal,
                                                PaidAmount = s.PaidAmount, // CRITICAL: Include for balance calculation
                                                PaymentStatus = s.PaymentStatus.ToString(),
                                                Notes = s.Notes,
                                                Items = new List<SaleItemDto>() // Empty list for summary view
                                            })
                                            .Take(10)
                                            .ToListAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading pending invoices: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    pendingInvoices = new List<SaleDto>();
                    pendingBillsCount = 0;
                }

                // Calculate invoice counts
                var invoicesToday = await _context.Sales
                    .Where(s => !s.IsDeleted && s.InvoiceDate >= today && s.InvoiceDate < today.AddDays(1))
                    .CountAsync();
                
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var invoicesWeekly = await _context.Sales
                    .Where(s => !s.IsDeleted && s.InvoiceDate >= startOfWeek && s.InvoiceDate < today.AddDays(1))
                    .CountAsync();
                
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var invoicesMonthly = await _context.Sales
                    .Where(s => !s.IsDeleted && s.InvoiceDate >= startOfMonth && s.InvoiceDate < today.AddDays(1))
                    .CountAsync();

                var result = new SummaryReportDto
                {
                    SalesToday = salesToday,
                    PurchasesToday = purchasesToday,
                    ExpensesToday = expensesToday,
                    ProfitToday = profitToday,
                    LowStockProducts = lowStockProducts,
                    PendingInvoices = pendingInvoices,
                    PendingBills = pendingBillsCount,
                    PendingBillsAmount = pendingBillsAmount,
                    PaidBills = paidBillsCount,
                    PaidBillsAmount = paidBillsAmount,
                    InvoicesToday = invoicesToday,
                    InvoicesWeekly = invoicesWeekly,
                    InvoicesMonthly = invoicesMonthly
                };
                
                Console.WriteLine($"✅ SummaryReportDto created: Sales={salesToday}, Purchases={purchasesToday}, Expenses={expensesToday}, Profit={profitToday}");
                Console.WriteLine($"✅ Bills Summary: Pending={pendingBillsCount} (${pendingBillsAmount:C}), Paid={paidBillsCount} (${paidBillsAmount:C})");
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error in GetSummaryReportAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                
                // Return a safe default response
                return new SummaryReportDto
                {
                    SalesToday = 0,
                    PurchasesToday = 0,
                    ExpensesToday = 0,
                    ProfitToday = 0,
                    LowStockProducts = new List<ProductDto>(),
                    PendingInvoices = new List<SaleDto>(),
                    PendingBills = 0,
                    PendingBillsAmount = 0,
                    PaidBills = 0,
                    PaidBillsAmount = 0,
                    InvoicesToday = 0,
                    InvoicesWeekly = 0,
                    InvoicesMonthly = 0
                };
            }
        }

        public async Task<PagedResponse<SaleDto>> GetSalesReportAsync(
            DateTime fromDate, 
            DateTime toDate, 
            int? customerId = null,
            string? status = null,
            int page = 1, 
            int pageSize = 10)
        {
            // Build query with filters
            var query = _context.Sales
                .Where(s => !s.IsDeleted && s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate);
            
            // Apply customer filter
            if (customerId.HasValue)
            {
                query = query.Where(s => s.CustomerId == customerId.Value);
            }
            
            // Apply status filter (Pending, Paid, Partial)
            if (!string.IsNullOrWhiteSpace(status))
            {
                var statusUpper = status.ToUpper();
                if (statusUpper == "PENDING" || statusUpper == "UNPAID")
                {
                    // Pending: balance > 0.01
                    query = query.Where(s => (s.GrandTotal - s.PaidAmount) > 0.01m);
                }
                else if (statusUpper == "PAID")
                {
                    // Paid: balance <= 0.01
                    query = query.Where(s => (s.GrandTotal - s.PaidAmount) <= 0.01m);
                }
                else if (statusUpper == "PARTIAL")
                {
                    // Partial: paid > 0 but balance > 0.01
                    query = query.Where(s => s.PaidAmount > 0 && (s.GrandTotal - s.PaidAmount) > 0.01m);
                }
            }
            
            var totalCount = await query.CountAsync();
            
            // CRITICAL: Include PaidAmount to calculate actual balance for accurate reporting
            // Calculate balance = GrandTotal - PaidAmount for each sale
            var sales = await (from s in query
                              join c in _context.Customers on s.CustomerId equals c.Id into customerGroup
                              from c in customerGroup.DefaultIfEmpty()
                              orderby s.InvoiceDate descending
                              select new SaleDto
                              {
                                  Id = s.Id,
                                  InvoiceNo = s.InvoiceNo,
                                  InvoiceDate = s.InvoiceDate,
                                  CustomerId = s.CustomerId,
                                  CustomerName = c != null ? c.Name : null,
                                  Subtotal = s.Subtotal,
                                  VatTotal = s.VatTotal,
                                  Discount = s.Discount,
                                  GrandTotal = s.GrandTotal,
                                  PaidAmount = s.PaidAmount, // CRITICAL: Include for balance calculation
                                  PaymentStatus = s.PaymentStatus.ToString(),
                                  Notes = s.Notes,
                                  Items = new List<SaleItemDto>() // Items not needed for report view
                              })
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();
            
            Console.WriteLine($"📊 Sales Report: {totalCount} total sales, returning {sales.Count} for page {page}");

            return new PagedResponse<SaleDto>
            {
                Items = sales,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        public async Task<List<ProductSalesDto>> GetProductSalesReportAsync(DateTime fromDate, DateTime toDate, int top = 20)
        {
            try
            {
                // First, get the grouped sales data
                var groupedData = await (from si in _context.SaleItems
                                        join s in _context.Sales on si.SaleId equals s.Id
                                        where !s.IsDeleted && s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate
                                        group si by si.ProductId into g
                                        select new
                                        {
                                            ProductId = g.Key,
                                            TotalQty = g.Sum(si => si.Qty),
                                            TotalAmount = g.Sum(si => si.LineTotal),
                                            TotalSales = g.Count()
                                        })
                                        .OrderByDescending(x => x.TotalAmount)
                                        .Take(top)
                                        .ToListAsync();

                // If no data, return empty list
                if (!groupedData.Any())
                {
                    return new List<ProductSalesDto>();
                }

                // Get product IDs to fetch product details
                var productIds = groupedData.Select(x => x.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.NameEn, p.Sku })
                    .ToListAsync();

                // Combine the data
                var productSales = groupedData.Select(g =>
                {
                    var product = products.FirstOrDefault(p => p.Id == g.ProductId);
                    return new ProductSalesDto
                    {
                        ProductId = g.ProductId,
                        ProductName = product != null ? (product.NameEn ?? "Unknown") : "Deleted Product",
                        Sku = product != null ? (product.Sku ?? "N/A") : "N/A",
                        TotalQty = g.TotalQty,
                        TotalAmount = g.TotalAmount,
                        TotalSales = g.TotalSales
                    };
                }).ToList();

                return productSales;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetProductSalesReportAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                // Return empty list instead of throwing
                return new List<ProductSalesDto>();
            }
        }

        public async Task<List<CustomerDto>> GetOutstandingCustomersAsync(int days = 30)
        {
            try
            {
                // CRITICAL: Get customers with outstanding balance > 0.01 (accounting for rounding)
                var customers = await _context.Customers
                    .Where(c => c.Balance > 0.01m)
                    .OrderByDescending(c => c.Balance)
                    .Select(c => new CustomerDto
                    {
                        Id = c.Id,
                        Name = c.Name ?? "Unknown",
                        Phone = c.Phone,
                        Trn = c.Trn,
                        Address = c.Address,
                        CreditLimit = c.CreditLimit,
                        Balance = c.Balance
                    })
                    .ToListAsync();

                Console.WriteLine($"✅ GetOutstandingCustomersAsync: Found {customers.Count} customers with outstanding balance");
                if (customers.Any())
                {
                    Console.WriteLine($"✅ Total outstanding: {customers.Sum(c => c.Balance):C}");
                    Console.WriteLine($"✅ Highest balance: {customers.First().Balance:C} ({customers.First().Name})");
                }
                return customers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetOutstandingCustomersAsync: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner exception: {ex.InnerException.Message}");
                }
                // Return empty list instead of throwing
                return new List<CustomerDto>();
            }
        }

        public async Task<List<PaymentDto>> GetChequeReportAsync()
        {
            var cheques = await _context.Payments
                .Where(p => p.Mode == PaymentMode.CHEQUE)
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new PaymentDto
                {
                    Id = p.Id,
                    SaleId = p.SaleId,
                    InvoiceNo = p.Sale != null ? p.Sale.InvoiceNo : null,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer != null ? p.Customer.Name : null,
                    Amount = p.Amount,
                    Mode = p.Mode.ToString(),
                    Reference = p.Reference,
                    Status = p.Status.ToString(),
                    PaymentDate = p.PaymentDate,
                    CreatedBy = p.CreatedBy,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return cheques;
        }

        public async Task<AISuggestionsDto> GetAISuggestionsAsync(int periodDays = 30)
        {
            try
            {
                var fromDate = DateTime.Today.AddDays(-periodDays);

                // Top sellers - Safe query with null checks
                List<ProductDto> topSellers = new List<ProductDto>();
                try
                {
                    topSellers = await _context.SaleItems
                        .Include(si => si.Sale)
                        .Include(si => si.Product)
                        .Where(si => si.Sale != null && si.Sale.InvoiceDate >= fromDate && si.Product != null)
                        .GroupBy(si => new { 
                            si.ProductId, 
                            ProductName = si.Product != null ? si.Product.NameEn : "Unknown", 
                            ProductSku = si.Product != null ? si.Product.Sku : "N/A",
                            UnitType = si.Product != null ? si.Product.UnitType : "KG",
                            CostPrice = si.Product != null ? si.Product.CostPrice : 0,
                            SellPrice = si.Product != null ? si.Product.SellPrice : 0,
                            StockQty = si.Product != null ? si.Product.StockQty : 0,
                            ReorderLevel = si.Product != null ? si.Product.ReorderLevel : 0
                        })
                        .Select(g => new ProductDto
                        {
                            Id = g.Key.ProductId,
                            Sku = g.Key.ProductSku,
                            NameEn = g.Key.ProductName,
                            UnitType = g.Key.UnitType,
                            ConversionToBase = 1,
                            CostPrice = g.Key.CostPrice,
                            SellPrice = g.Key.SellPrice,
                            StockQty = g.Key.StockQty,
                            ReorderLevel = g.Key.ReorderLevel
                        })
                        .Take(5)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching top sellers: {ex.Message}");
                }

                // Restock candidates (low stock)
                List<ProductDto> restockCandidates = new List<ProductDto>();
                try
                {
                    restockCandidates = await _context.Products
                        .Where(p => p.StockQty <= p.ReorderLevel)
                        .OrderBy(p => p.StockQty)
                        .Take(5)
                        .Select(p => new ProductDto
                        {
                            Id = p.Id,
                            Sku = p.Sku,
                            NameEn = p.NameEn,
                            NameAr = p.NameAr,
                            UnitType = p.UnitType,
                            ConversionToBase = p.ConversionToBase,
                            CostPrice = p.CostPrice,
                            SellPrice = p.SellPrice,
                            StockQty = p.StockQty,
                            ReorderLevel = p.ReorderLevel,
                            DescriptionEn = p.DescriptionEn,
                            DescriptionAr = p.DescriptionAr
                        })
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching restock candidates: {ex.Message}");
                }

                // Low margin products
                List<ProductDto> lowMarginProducts = new List<ProductDto>();
                try
                {
                    // Load data first, then order in memory to avoid SQLite decimal ORDER BY issues
                    var products = await _context.Products
                        .Where(p => p.SellPrice > 0 && (p.SellPrice - p.CostPrice) / p.SellPrice < 0.2m)
                        .Select(p => new ProductDto
                        {
                            Id = p.Id,
                            Sku = p.Sku,
                            NameEn = p.NameEn,
                            NameAr = p.NameAr,
                            UnitType = p.UnitType,
                            ConversionToBase = p.ConversionToBase,
                            CostPrice = p.CostPrice,
                            SellPrice = p.SellPrice,
                            StockQty = p.StockQty,
                            ReorderLevel = p.ReorderLevel,
                            DescriptionEn = p.DescriptionEn,
                            DescriptionAr = p.DescriptionAr
                        })
                        .ToListAsync();
                    
                    lowMarginProducts = products
                        .OrderBy(p => p.SellPrice > 0 ? (p.SellPrice - p.CostPrice) / p.SellPrice : 0)
                        .Take(5)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching low margin products: {ex.Message}");
                }

                // Pending customers
                List<CustomerDto> pendingCustomers = new List<CustomerDto>();
                try
                {
                    pendingCustomers = await _context.Customers
                        .Where(c => c.Balance > 0)
                        .OrderByDescending(c => c.Balance)
                        .Take(5)
                        .Select(c => new CustomerDto
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Phone = c.Phone,
                            Trn = c.Trn,
                            Address = c.Address,
                            CreditLimit = c.CreditLimit,
                            Balance = c.Balance
                        })
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching pending customers: {ex.Message}");
                }

                // Promotion candidates
                List<ProductDto> promotionCandidates = new List<ProductDto>();
                try
                {
                    promotionCandidates = await _context.Products
                        .Where(p => p.SellPrice > 0 && (p.SellPrice - p.CostPrice) / p.SellPrice > 0.3m && p.StockQty <= p.ReorderLevel * 2)
                        .OrderByDescending(p => p.SellPrice > 0 ? (p.SellPrice - p.CostPrice) / p.SellPrice : 0)
                        .Take(5)
                        .Select(p => new ProductDto
                        {
                            Id = p.Id,
                            Sku = p.Sku,
                            NameEn = p.NameEn,
                            NameAr = p.NameAr,
                            UnitType = p.UnitType,
                            ConversionToBase = p.ConversionToBase,
                            CostPrice = p.CostPrice,
                            SellPrice = p.SellPrice,
                            StockQty = p.StockQty,
                            ReorderLevel = p.ReorderLevel,
                            DescriptionEn = p.DescriptionEn,
                            DescriptionAr = p.DescriptionAr
                        })
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching promotion candidates: {ex.Message}");
                }

                return new AISuggestionsDto
                {
                    TopSellers = topSellers,
                    RestockCandidates = restockCandidates,
                    LowMarginProducts = lowMarginProducts,
                    PendingCustomers = pendingCustomers,
                    PromotionCandidates = promotionCandidates
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error in GetAISuggestionsAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Return empty suggestions instead of crashing
                return new AISuggestionsDto
                {
                    TopSellers = new List<ProductDto>(),
                    RestockCandidates = new List<ProductDto>(),
                    LowMarginProducts = new List<ProductDto>(),
                    PendingCustomers = new List<CustomerDto>(),
                    PromotionCandidates = new List<ProductDto>()
                };
            }
        }

        public async Task<List<PendingBillDto>> GetPendingBillsAsync(
            DateTime? fromDate = null, 
            DateTime? toDate = null, 
            int? customerId = null,
            string? search = null, 
            string? status = null)
        {
            // CRITICAL: Get ALL pending bills regardless of invoice date
            // This ensures backdated invoices from past years show as overdue
            // Only apply date filter if EXPLICITLY provided (for report filtering)
            
            var salesQuery = _context.Sales
                .Include(s => s.Customer)
                .Where(s => !s.IsDeleted && (s.PaymentStatus == SalePaymentStatus.Pending || s.PaymentStatus == SalePaymentStatus.Partial))
                .AsQueryable();

            // ONLY filter by date range if BOTH dates are explicitly provided
            // This allows filtering in reports while showing ALL pending bills on dashboard
            if (fromDate.HasValue && toDate.HasValue)
            {
                var from = fromDate.Value.Date;
                var toDateEnd = toDate.Value.Date.AddDays(1).AddTicks(-1);
                salesQuery = salesQuery.Where(s => s.InvoiceDate >= from && s.InvoiceDate <= toDateEnd);
            }

            // Filter by customer
            if (customerId.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.CustomerId == customerId.Value);
            }

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                if (status.ToLower() == "pending")
                {
                    salesQuery = salesQuery.Where(s => s.PaymentStatus == SalePaymentStatus.Pending);
                }
                else if (status.ToLower() == "partial")
                {
                    salesQuery = salesQuery.Where(s => s.PaymentStatus == SalePaymentStatus.Partial);
                }
                else if (status.ToLower() == "overdue")
                {
                    var cutoffDate = DateTime.Today.AddDays(-30);
                    salesQuery = salesQuery.Where(s => s.InvoiceDate < cutoffDate && 
                                           s.PaymentStatus != SalePaymentStatus.Paid);
                }
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                salesQuery = salesQuery.Where(s => 
                    s.InvoiceNo.Contains(search) || 
                    (s.Customer != null && s.Customer.Name.Contains(search)));
            }

            // CRITICAL: Get ALL sales first, then filter in memory to ensure accuracy
            var allSales = await salesQuery
                .OrderByDescending(s => s.InvoiceDate)
                .ToListAsync();

            var pendingBills = new List<PendingBillDto>();
            var today = DateTime.Today;
            
            // CRITICAL: Filter by actual balance calculation (not just PaymentStatus)
            foreach (var sale in allSales)
            {
                // Use Sale.PaidAmount directly (maintained by PaymentService atomically)
                var paidAmount = sale.PaidAmount;
                var balanceAmount = sale.GrandTotal - paidAmount;
                
                // CRITICAL: Only include bills with actual outstanding balance > 0.01 (rounding tolerance)
                if (balanceAmount <= 0.01m) continue;
                
                var dueDate = sale.InvoiceDate.AddDays(30); // Default 30 days credit
                var daysOutstanding = (today - sale.InvoiceDate).Days;
                var daysOverdue = dueDate < today ? (today - dueDate).Days : 0;

                pendingBills.Add(new PendingBillDto
                {
                    Id = sale.Id,
                    InvoiceNo = sale.InvoiceNo,
                    InvoiceDate = sale.InvoiceDate,
                    DueDate = dueDate,
                    CustomerId = sale.CustomerId,
                    CustomerName = sale.Customer != null ? sale.Customer.Name : null,
                    GrandTotal = sale.GrandTotal,
                    PaidAmount = paidAmount,
                    BalanceAmount = balanceAmount,
                    PaymentStatus = sale.PaymentStatus.ToString(),
                    DaysOverdue = daysOverdue
                });
            }

            return pendingBills
                .OrderByDescending(pb => pb.DaysOverdue)
                .ThenByDescending(pb => pb.InvoiceDate)
                .ToList();
        }

        public async Task<List<ExpenseByCategoryDto>> GetExpensesByCategoryAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var expenses = await (from e in _context.Expenses
                                     join c in _context.ExpenseCategories on e.CategoryId equals c.Id into categoryGroup
                                     from category in categoryGroup.DefaultIfEmpty()
                                     where e.Date >= fromDate && e.Date <= toDate
                                     group e by new { 
                                         e.CategoryId,
                                         CategoryName = category != null ? category.Name : "Uncategorized",
                                         CategoryColor = category != null ? category.ColorCode : "#6B7280"
                                     } into g
                                     select new ExpenseByCategoryDto
                                     {
                                         CategoryId = g.Key.CategoryId,
                                         CategoryName = g.Key.CategoryName,
                                         CategoryColor = g.Key.CategoryColor,
                                         TotalAmount = g.Sum(e => e.Amount),
                                         ExpenseCount = g.Count()
                                     })
                                     .OrderByDescending(x => x.TotalAmount)
                                     .ToListAsync();

                return expenses;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetExpensesByCategoryAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                // Return empty list instead of throwing
                return new List<ExpenseByCategoryDto>();
            }
        }

        public async Task<List<SalesVsExpensesDto>> GetSalesVsExpensesAsync(DateTime fromDate, DateTime toDate, string groupBy = "day")
        {
            List<SalesVsExpensesDto> result = new List<SalesVsExpensesDto>();

            if (groupBy == "month")
            {
                // Group by month
                var salesData = await _context.Sales
                    .Where(s => s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate)
                    .GroupBy(s => new { Year = s.InvoiceDate.Year, Month = s.InvoiceDate.Month })
                    .Select(g => new
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                        Sales = g.Sum(s => s.GrandTotal)
                    })
                    .ToListAsync();

                var purchasesData = await _context.Purchases
                    .Where(p => p.PurchaseDate >= fromDate && p.PurchaseDate <= toDate)
                    .GroupBy(p => new { Year = p.PurchaseDate.Year, Month = p.PurchaseDate.Month })
                    .Select(g => new
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                        Purchases = g.Sum(p => p.TotalAmount)
                    })
                    .ToListAsync();

                var expensesData = await _context.Expenses
                    .Where(e => e.Date >= fromDate && e.Date <= toDate)
                    .GroupBy(e => new { Year = e.Date.Year, Month = e.Date.Month })
                    .Select(g => new
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                        Expenses = g.Sum(e => e.Amount)
                    })
                    .ToListAsync();

                var allPeriods = salesData.Select(s => s.Period)
                    .Union(purchasesData.Select(p => p.Period))
                    .Union(expensesData.Select(e => e.Period))
                    .Distinct()
                    .OrderBy(p => p)
                    .ToList();

                foreach (var period in allPeriods)
                {
                    var sale = salesData.FirstOrDefault(s => s.Period == period);
                    var purchase = purchasesData.FirstOrDefault(p => p.Period == period);
                    var expense = expensesData.FirstOrDefault(e => e.Period == period);

                    result.Add(new SalesVsExpensesDto
                    {
                        Period = period,
                        Date = sale?.Date ?? purchase?.Date ?? expense?.Date ?? DateTime.Today,
                        Sales = sale?.Sales ?? 0,
                        Purchases = purchase?.Purchases ?? 0,
                        Expenses = expense?.Expenses ?? 0,
                        Profit = (sale?.Sales ?? 0) - (purchase?.Purchases ?? 0) - (expense?.Expenses ?? 0)
                    });
                }
            }
            else
            {
                // Group by day
                var salesData = await _context.Sales
                    .Where(s => s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate)
                    .GroupBy(s => s.InvoiceDate.Date)
                    .Select(g => new
                    {
                        Period = g.Key.ToString("yyyy-MM-dd"),
                        Date = g.Key,
                        Sales = g.Sum(s => s.GrandTotal)
                    })
                    .ToListAsync();

                var purchasesData = await _context.Purchases
                    .Where(p => p.PurchaseDate >= fromDate && p.PurchaseDate <= toDate)
                    .GroupBy(p => p.PurchaseDate.Date)
                    .Select(g => new
                    {
                        Period = g.Key.ToString("yyyy-MM-dd"),
                        Date = g.Key,
                        Purchases = g.Sum(p => p.TotalAmount)
                    })
                    .ToListAsync();

                var expensesData = await _context.Expenses
                    .Where(e => e.Date >= fromDate && e.Date <= toDate)
                    .GroupBy(e => e.Date.Date)
                    .Select(g => new
                    {
                        Period = g.Key.ToString("yyyy-MM-dd"),
                        Date = g.Key,
                        Expenses = g.Sum(e => e.Amount)
                    })
                    .ToListAsync();

                var allPeriods = salesData.Select(s => s.Period)
                    .Union(purchasesData.Select(p => p.Period))
                    .Union(expensesData.Select(e => e.Period))
                    .Distinct()
                    .OrderBy(p => p)
                    .ToList();

                foreach (var period in allPeriods)
                {
                    var sale = salesData.FirstOrDefault(s => s.Period == period);
                    var purchase = purchasesData.FirstOrDefault(p => p.Period == period);
                    var expense = expensesData.FirstOrDefault(e => e.Period == period);

                    result.Add(new SalesVsExpensesDto
                    {
                        Period = period,
                        Date = sale?.Date ?? purchase?.Date ?? expense?.Date ?? DateTime.Today,
                        Sales = sale?.Sales ?? 0,
                        Purchases = purchase?.Purchases ?? 0,
                        Expenses = expense?.Expenses ?? 0,
                        Profit = (sale?.Sales ?? 0) - (purchase?.Purchases ?? 0) - (expense?.Expenses ?? 0)
                    });
                }
            }

            return result;
        }

        // Enhanced Sales Report with Granularity
        public async Task<EnhancedSalesReportDto> GetEnhancedSalesReportAsync(DateTime fromDate, DateTime toDate, string granularity = "day", int? productId = null, int? customerId = null, string? status = null, int page = 1, int pageSize = 50)
        {
            var query = _context.Sales
                .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                .Include(s => s.Customer)
                .Where(s => !s.IsDeleted && s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate)
                .AsQueryable();

            if (productId.HasValue)
            {
                query = query.Where(s => s.Items.Any(i => i.ProductId == productId.Value));
            }

            if (customerId.HasValue)
            {
                query = query.Where(s => s.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                var statusEnum = Enum.Parse<SalePaymentStatus>(status, true);
                query = query.Where(s => s.PaymentStatus == statusEnum);
            }

            var totalCount = await query.CountAsync();
            var sales = await query
                .OrderByDescending(s => s.InvoiceDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Calculate summary
            var allSales = await query.ToListAsync();
            var summary = new SummaryInfo
            {
                TotalSales = allSales.Sum(s => s.GrandTotal),
                NetSales = allSales.Sum(s => s.Subtotal),
                VatCollected = allSales.Sum(s => s.VatTotal),
                TotalInvoices = allSales.Count,
                AvgOrderValue = allSales.Any() ? allSales.Average(s => s.GrandTotal) : 0
            };

            // Generate series based on granularity
            var series = new List<SalesSeriesDto>();
            if (granularity == "day")
            {
                var grouped = allSales.GroupBy(s => s.InvoiceDate.Date);
                series = grouped.Select(g => new SalesSeriesDto
                {
                    Period = g.Key.ToString("yyyy-MM-dd"),
                    Date = g.Key,
                    Amount = g.Sum(s => s.GrandTotal),
                    Count = g.Count()
                }).OrderBy(s => s.Date).ToList();
            }
            else if (granularity == "week")
            {
                var grouped = allSales.GroupBy(s => System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(s.InvoiceDate, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Sunday));
                series = grouped.Select(g => new SalesSeriesDto
                {
                    Period = $"Week {g.Key}",
                    Date = g.Min(s => s.InvoiceDate),
                    Amount = g.Sum(s => s.GrandTotal),
                    Count = g.Count()
                }).OrderBy(s => s.Date).ToList();
            }
            else if (granularity == "month")
            {
                var grouped = allSales.GroupBy(s => new { s.InvoiceDate.Year, s.InvoiceDate.Month });
                series = grouped.Select(g => new SalesSeriesDto
                {
                    Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Amount = g.Sum(s => s.GrandTotal),
                    Count = g.Count()
                }).OrderBy(s => s.Date).ToList();
            }

            // Map sales to report items
            var reportItems = sales.Select(s => new SalesReportItemDto
            {
                InvoiceId = s.Id,
                Date = s.InvoiceDate,
                InvoiceNo = s.InvoiceNo,
                CustomerId = s.CustomerId,
                CustomerName = s.Customer?.Name,
                Items = s.Items.Take(2).Select(i => new ProductSummaryDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.NameEn ?? "Unknown",
                    Qty = i.Qty,
                    Price = i.UnitPrice
                }).ToList(),
                Qty = s.Items.Sum(i => i.Qty),
                Gross = s.Subtotal,
                Vat = s.VatTotal,
                Discount = s.Discount,
                Net = s.GrandTotal,
                PaymentStatus = s.PaymentStatus.ToString()
            }).ToList();

            return new EnhancedSalesReportDto
            {
                Summary = summary,
                Series = series,
                Data = new PagedResponse<SalesReportItemDto>
                {
                    Items = reportItems,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            };
        }

        // Enhanced Product Sales Report with Margin Analysis
        public async Task<List<ProductSalesDto>> GetEnhancedProductSalesReportAsync(DateTime fromDate, DateTime toDate, int? productId = null, string? unitType = null, bool lowStockOnly = false)
        {
            var query = from si in _context.SaleItems
                       join s in _context.Sales on si.SaleId equals s.Id
                       join p in _context.Products on si.ProductId equals p.Id
                       where !s.IsDeleted && s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate
                       select new { si, s, p };

            if (productId.HasValue)
            {
                query = query.Where(x => x.p.Id == productId.Value);
            }

            if (!string.IsNullOrEmpty(unitType))
            {
                query = query.Where(x => x.p.UnitType == unitType);
            }

            var grouped = await query
                .GroupBy(x => x.p.Id)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Product = g.First().p,
                    TotalQty = g.Sum(x => x.si.Qty),
                    TotalAmount = g.Sum(x => x.si.LineTotal),
                    TotalSales = g.Count(),
                    CostValue = g.Sum(x => x.p.CostPrice * x.si.Qty)
                })
                .ToListAsync();

            var result = grouped.Select(g => new ProductSalesDto
            {
                ProductId = g.ProductId,
                ProductName = g.Product.NameEn ?? "Unknown",
                Sku = g.Product.Sku ?? "N/A",
                UnitType = g.Product.UnitType.ToString(),
                TotalQty = g.TotalQty,
                TotalAmount = g.TotalAmount,
                CostValue = g.CostValue,
                GrossProfit = g.TotalAmount - g.CostValue,
                MarginPercent = g.TotalAmount > 0 ? ((g.TotalAmount - g.CostValue) / g.TotalAmount) * 100 : 0,
                TotalSales = g.TotalSales,
                StockOnHand = g.Product.StockQty,
                ReorderLevel = g.Product.ReorderLevel,
                IsLowStock = g.Product.StockQty <= g.Product.ReorderLevel
            })
            .OrderByDescending(p => p.TotalAmount)
            .ToList();

            if (lowStockOnly)
            {
                result = result.Where(p => p.IsLowStock).ToList();
            }

            return result;
        }

        // Customer Report with Outstanding Analysis
        public async Task<CustomerReportDto> GetCustomerReportAsync(DateTime fromDate, DateTime toDate, decimal? minOutstanding = null)
        {
            var customers = await _context.Customers.ToListAsync();
            var customerReports = new List<CustomerReportItemDto>();

            foreach (var customer in customers)
            {
                var sales = await _context.Sales
                    .Where(s => !s.IsDeleted && s.CustomerId == customer.Id && s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate)
                    .ToListAsync();

                var payments = await _context.Payments
                    .Where(p => p.CustomerId == customer.Id && p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
                    .ToListAsync();

                var totalSales = sales.Sum(s => s.GrandTotal);
                var totalPayments = payments.Sum(p => p.Amount);
                var outstanding = customer.Balance; // Use calculated balance

                // Calculate avg days to pay
                var paidInvoices = sales.Where(s => s.PaymentStatus == SalePaymentStatus.Paid).ToList();
                var avgDaysToPay = 0m;
                if (paidInvoices.Any())
                {
                    var daysList = new List<int>();
                    foreach (var inv in paidInvoices)
                    {
                        var firstPayment = payments.Where(p => p.SaleId == inv.Id).OrderBy(p => p.PaymentDate).FirstOrDefault();
                        if (firstPayment != null)
                        {
                            daysList.Add((firstPayment.PaymentDate.Date - inv.InvoiceDate.Date).Days);
                        }
                    }
                    if (daysList.Any())
                    {
                        avgDaysToPay = (decimal)daysList.Average();
                    }
                }

                var lastPayment = payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();

                if (minOutstanding == null || outstanding >= minOutstanding.Value)
                {
                    customerReports.Add(new CustomerReportItemDto
                    {
                        CustomerId = customer.Id,
                        CustomerName = customer.Name ?? "Unknown",
                        Trn = customer.Trn,
                        TotalSales = totalSales,
                        TotalPayments = totalPayments,
                        Outstanding = outstanding,
                        AvgDaysToPay = avgDaysToPay,
                        LastPaymentDate = lastPayment?.PaymentDate,
                        LastPaymentMode = lastPayment?.Mode.ToString()
                    });
                }
            }

            var summary = new CustomerReportSummary
            {
                TotalCustomers = customerReports.Count,
                TotalSales = customerReports.Sum(c => c.TotalSales),
                TotalPayments = customerReports.Sum(c => c.TotalPayments),
                TotalOutstanding = customerReports.Sum(c => c.Outstanding),
                AvgDaysToPay = customerReports.Any() ? customerReports.Average(c => c.AvgDaysToPay) : 0
            };

            return new CustomerReportDto
            {
                Customers = customerReports.OrderByDescending(c => c.Outstanding).ToList(),
                Summary = summary
            };
        }

        // Aging Report with Buckets
        public async Task<AgingReportDto> GetAgingReportAsync(DateTime asOfDate, int? customerId = null)
        {
            var salesQuery = _context.Sales
                .Include(s => s.Customer)
                .Where(s => !s.IsDeleted && s.PaymentStatus != SalePaymentStatus.Paid)
                .AsQueryable();

            if (customerId.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.CustomerId == customerId.Value);
            }

            var sales = await salesQuery.ToListAsync();
            var invoices = new List<AgingInvoiceDto>();

            foreach (var sale in sales)
            {
                var paidAmount = await _context.Payments
                    .Where(p => p.SaleId == sale.Id)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                var balance = sale.GrandTotal - paidAmount;
                if (balance <= 0) continue;

                var daysOverdue = (asOfDate.Date - sale.InvoiceDate.Date).Days;
                string bucket;
                if (daysOverdue <= 30) bucket = "0-30";
                else if (daysOverdue <= 60) bucket = "31-60";
                else if (daysOverdue <= 90) bucket = "61-90";
                else bucket = "90+";

                invoices.Add(new AgingInvoiceDto
                {
                    Id = sale.Id,
                    InvoiceNo = sale.InvoiceNo,
                    InvoiceDate = sale.InvoiceDate,
                    CustomerId = sale.CustomerId,
                    CustomerName = sale.Customer?.Name,
                    GrandTotal = sale.GrandTotal,
                    PaidAmount = paidAmount,
                    BalanceAmount = balance,
                    DaysOverdue = daysOverdue,
                    AgingBucket = bucket
                });
            }

            var bucket0_30 = new AgingBucket
            {
                Invoices = invoices.Where(i => i.AgingBucket == "0-30").ToList(),
                Total = invoices.Where(i => i.AgingBucket == "0-30").Sum(i => i.BalanceAmount),
                Count = invoices.Count(i => i.AgingBucket == "0-30")
            };

            var bucket31_60 = new AgingBucket
            {
                Invoices = invoices.Where(i => i.AgingBucket == "31-60").ToList(),
                Total = invoices.Where(i => i.AgingBucket == "31-60").Sum(i => i.BalanceAmount),
                Count = invoices.Count(i => i.AgingBucket == "31-60")
            };

            var bucket61_90 = new AgingBucket
            {
                Invoices = invoices.Where(i => i.AgingBucket == "61-90").ToList(),
                Total = invoices.Where(i => i.AgingBucket == "61-90").Sum(i => i.BalanceAmount),
                Count = invoices.Count(i => i.AgingBucket == "61-90")
            };

            var bucket90Plus = new AgingBucket
            {
                Invoices = invoices.Where(i => i.AgingBucket == "90+").ToList(),
                Total = invoices.Where(i => i.AgingBucket == "90+").Sum(i => i.BalanceAmount),
                Count = invoices.Count(i => i.AgingBucket == "90+")
            };

            return new AgingReportDto
            {
                Bucket0_30 = bucket0_30,
                Bucket31_60 = bucket31_60,
                Bucket61_90 = bucket61_90,
                Bucket90Plus = bucket90Plus,
                TotalOutstanding = invoices.Sum(i => i.BalanceAmount),
                Invoices = invoices.OrderByDescending(i => i.DaysOverdue).ToList()
            };
        }

        // Stock Report with Restock Alerts
        public async Task<StockReportDto> GetStockReportAsync(bool lowOnly = false)
        {
            var productsQuery = _context.Products.AsQueryable();

            if (lowOnly)
            {
                productsQuery = productsQuery.Where(p => p.StockQty <= p.ReorderLevel);
            }

            var products = await productsQuery.ToListAsync();

            // Get reserved quantities (pending sales)
            var reservedByProduct = await _context.SaleItems
                .Include(si => si.Sale)
                .Where(si => !si.Sale.IsDeleted && si.Sale.PaymentStatus == SalePaymentStatus.Pending)
                .GroupBy(si => si.ProductId)
                .Select(g => new { ProductId = g.Key, Reserved = g.Sum(si => si.Qty) })
                .ToListAsync();

            var stockItems = products.Select(p =>
            {
                var reserved = reservedByProduct.FirstOrDefault(r => r.ProductId == p.Id)?.Reserved ?? 0;
                var available = p.StockQty - reserved;

                // Calculate predicted days to stockout (based on 30-day avg sales)
                var last30Days = DateTime.Today.AddDays(-30);
                var avgDailySales = _context.SaleItems
                    .Include(si => si.Sale)
                    .Where(si => si.ProductId == p.Id && !si.Sale.IsDeleted && si.Sale.InvoiceDate >= last30Days)
                    .Sum(si => (decimal?)si.Qty) ?? 0;
                avgDailySales = avgDailySales / 30;
                var predictedDays = avgDailySales > 0 && available > 0 ? (int)(available / avgDailySales) : (int?)null;

                return new StockItemDto
                {
                    ProductId = p.Id,
                    ProductName = p.NameEn ?? "Unknown",
                    Sku = p.Sku ?? "N/A",
                    UnitType = p.UnitType.ToString(),
                    OnHand = p.StockQty,
                    Reserved = reserved,
                    Available = available,
                    ReorderLevel = p.ReorderLevel,
                    SafetyStock = p.ReorderLevel,
                    LastPurchaseDate = _context.Purchases
                        .Include(pi => pi.Items)
                        .Where(pi => pi.Items.Any(pi => pi.ProductId == p.Id))
                        .OrderByDescending(pi => pi.PurchaseDate)
                        .Select(pi => (DateTime?)pi.PurchaseDate)
                        .FirstOrDefault(),
                    IsLowStock = available <= p.ReorderLevel,
                    PredictedDaysToStockOut = predictedDays
                };
            }).ToList();

            var summary = new StockSummary
            {
                TotalSKUs = products.Count,
                LowStockCount = stockItems.Count(i => i.IsLowStock),
                OutOfStockCount = stockItems.Count(i => i.Available <= 0),
                StockValue = products.Sum(p => p.StockQty * p.CostPrice)
            };

            return new StockReportDto
            {
                Summary = summary,
                Items = stockItems.OrderByDescending(i => i.IsLowStock).ThenBy(i => i.Available).ToList()
            };
        }

        public async Task<SalesLedgerReportDto> GetComprehensiveSalesLedgerAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var from = fromDate ?? DateTime.Today.AddDays(-365);
            var to = (toDate ?? DateTime.Today).AddDays(1).AddTicks(-1); // End of day

            // Get all sales (non-deleted) within date range
            var sales = await _context.Sales
                .Where(s => !s.IsDeleted && s.InvoiceDate >= from && s.InvoiceDate <= to)
                .OrderBy(s => s.InvoiceDate)
                .ThenBy(s => s.Id)
                .ToListAsync();

            // Get all payments within date range
            var payments = await _context.Payments
                .Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
                .OrderBy(p => p.PaymentDate)
                .ThenBy(p => p.Id)
                .ToListAsync();

            // Get payment totals per sale for status calculation
            var salePayments = await _context.Payments
                .Where(p => p.SaleId.HasValue)
                .GroupBy(p => p.SaleId!.Value)
                .Select(g => new { SaleId = g.Key, TotalPaid = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.SaleId, x => x.TotalPaid);

            // Load all customers in one query for efficiency
            var customerIds = sales.Select(s => s.CustomerId).Concat(payments.Select(p => p.CustomerId))
                .Where(id => id.HasValue)
                .Distinct()
                .ToList();
            var customers = await _context.Customers
                .Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            // Build ledger entries
            var ledgerEntries = new List<SalesLedgerEntryDto>();
            
            // Track per-customer balances (not global)
            // Use 0 as key for null customer IDs
            var customerBalances = new Dictionary<int, decimal>();

            // Add sales entries (Debit)
            foreach (var sale in sales)
            {
                var paidAmount = salePayments.GetValueOrDefault(sale.Id, 0m);
                var balance = sale.GrandTotal - paidAmount;
                
                // Determine status
                string status = "Unpaid";
                if (balance <= 0.01m)
                {
                    status = "Paid";
                }
                else if (paidAmount > 0)
                {
                    status = "Partial";
                }

                // Calculate Plan Date (Due Date = Invoice Date + 30 days)
                var planDate = sale.InvoiceDate.AddDays(30);

                // Update customer balance
                var customerKey = sale.CustomerId ?? 0;
                if (!customerBalances.ContainsKey(customerKey))
                {
                    customerBalances[customerKey] = 0m;
                }
                customerBalances[customerKey] += sale.GrandTotal; // Add debit

                // Payment mode: Show "NOT PAID" if unpaid, otherwise show payment mode from related payments
                string paymentModeDisplay = "NOT PAID";
                if (status == "Paid" || status == "Partial")
                {
                    // Get payment mode from first payment for this sale
                    var firstPayment = payments.FirstOrDefault(p => p.SaleId == sale.Id);
                    if (firstPayment != null)
                    {
                        paymentModeDisplay = firstPayment.Mode.ToString().ToUpper();
                    }
                    else if (status == "Paid")
                    {
                        paymentModeDisplay = "PAID";
                    }
                }

                // Calculate real pending (GrandTotal - PaidAmount)
                var realPending = sale.GrandTotal - paidAmount;
                
                ledgerEntries.Add(new SalesLedgerEntryDto
                {
                    Date = sale.InvoiceDate,
                    Type = "Sale",
                    InvoiceNo = sale.InvoiceNo,
                    CustomerId = sale.CustomerId,
                    CustomerName = sale.CustomerId.HasValue && customers.ContainsKey(sale.CustomerId.Value)
                        ? customers[sale.CustomerId.Value]
                        : "Cash Customer",
                    PaymentMode = paymentModeDisplay,
                    GrandTotal = sale.GrandTotal, // CRITICAL: Full invoice amount
                    PaidAmount = paidAmount, // CRITICAL: Amount already paid for this invoice
                    RealPending = realPending > 0 ? realPending : 0,
                    RealGotPayment = paidAmount, // CRITICAL: Show actual paid amount for sales (not 0)
                    Status = status,
                    CustomerBalance = customerBalances[customerKey],
                    PlanDate = planDate,
                    SaleId = sale.Id
                });
            }

            // Add payment entries (Credit)
            foreach (var payment in payments)
            {
                // Get related sale for invoice number and status
                var relatedSale = payment.SaleId.HasValue 
                    ? sales.FirstOrDefault(s => s.Id == payment.SaleId.Value)
                    : null;

                var invoiceNo = relatedSale?.InvoiceNo ?? payment.Reference ?? "-";
                var paidAmount = salePayments.GetValueOrDefault(relatedSale?.Id ?? 0, 0m);
                var saleBalance = relatedSale != null ? relatedSale.GrandTotal - paidAmount : 0m;
                
                // Determine status from related sale
                string status = "Partial";
                if (relatedSale != null)
                {
                    if (saleBalance <= 0.01m)
                    {
                        status = "Paid";
                    }
                    else if (paidAmount > 0)
                    {
                        status = "Partial";
                    }
                    else
                    {
                        status = "Unpaid";
                    }
                }

                // Update customer balance
                var paymentCustomerKey = payment.CustomerId ?? 0;
                if (!customerBalances.ContainsKey(paymentCustomerKey))
                {
                    customerBalances[paymentCustomerKey] = 0m;
                }
                customerBalances[paymentCustomerKey] -= payment.Amount; // Subtract credit

                ledgerEntries.Add(new SalesLedgerEntryDto
                {
                    Date = payment.PaymentDate,
                    Type = "Payment",
                    InvoiceNo = invoiceNo,
                    CustomerId = payment.CustomerId,
                    CustomerName = payment.CustomerId.HasValue && customers.ContainsKey(payment.CustomerId.Value)
                        ? customers[payment.CustomerId.Value]
                        : "Cash Customer",
                    PaymentMode = payment.Mode.ToString().ToUpper(),
                    GrandTotal = payment.Amount, // CRITICAL: Payment amount
                    PaidAmount = 0, // Payments don't have paidAmount (they ARE the payment)
                    RealPending = 0, // Payments don't have pending
                    RealGotPayment = payment.Amount, // Real payment received
                    Status = status,
                    CustomerBalance = customerBalances[paymentCustomerKey],
                    PlanDate = null, // Payments don't have plan dates
                    PaymentId = payment.Id,
                    SaleId = payment.SaleId
                });
            }

            // Sort by date, then by type (Sales before Payments on same date)
            ledgerEntries = ledgerEntries
                .OrderBy(e => e.Date)
                .ThenBy(e => e.Type == "Payment" ? 1 : 0)
                .ToList();

            // Calculate summary totals - CORRECTED CALCULATIONS
            // 1. Total Sales = Sum of GrandTotal from all sales in date range
            var totalSales = sales.Sum(s => s.GrandTotal);
            
            // 2. Total Payments = Sum of payments linked to sales in this period ONLY
            // CRITICAL: Only count payments that are linked to sales in the date range
            // This ensures: Total Payments <= Total Sales (logically correct)
            // We use salePayments dictionary which already has the correct totals per sale
            var saleIdsInPeriod = sales.Select(s => s.Id).ToHashSet();
            var totalPayments = salePayments
                .Where(kvp => saleIdsInPeriod.Contains(kvp.Key))
                .Sum(kvp => kvp.Value);
            
            // Alternative: Sum from payments directly (for verification)
            var totalPaymentsFromPayments = payments
                .Where(p => p.SaleId.HasValue && saleIdsInPeriod.Contains(p.SaleId.Value))
                .Sum(p => p.Amount);
            
            // Use the higher value to ensure accuracy (should be same, but handle edge cases)
            totalPayments = Math.Max(totalPayments, totalPaymentsFromPayments);
            
            // CRITICAL: Ensure payments never exceed sales (logically impossible)
            totalPayments = Math.Min(totalPayments, totalSales);
            
            // 3. Real Pending = Sum of unpaid amounts (GrandTotal - PaidAmount) from sales only
            var totalRealPending = ledgerEntries
                .Where(e => e.Type == "Sale")
                .Sum(e => e.RealPending);
            
            // 4. Total Real Got Payment = Total Payments (same value, different name)
            var totalRealGotPayment = totalPayments;
            
            // 5. Pending Balance = Total Sales - Total Payments (net outstanding)
            // This is the actual amount still owed after all payments
            var pendingBalance = totalSales - totalPayments;

            return new SalesLedgerReportDto
            {
                Entries = ledgerEntries,
                Summary = new SalesLedgerSummary
                {
                    TotalDebit = totalRealPending, // Keep for backward compatibility
                    TotalCredit = totalRealGotPayment, // Keep for backward compatibility
                    OutstandingBalance = pendingBalance, // CORRECTED: Use calculated pending balance
                    TotalSales = totalSales, // CORRECTED: Sum of all invoice amounts
                    TotalPayments = totalPayments // CORRECTED: Sum of all payments
                }
            };
        }
    }

    public class ProductSalesDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string UnitType { get; set; } = string.Empty;
        public decimal TotalQty { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CostValue { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal MarginPercent { get; set; }
        public int TotalSales { get; set; }
        public decimal StockOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsLowStock { get; set; }
    }
}

