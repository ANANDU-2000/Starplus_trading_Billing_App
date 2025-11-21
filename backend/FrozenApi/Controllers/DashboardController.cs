using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;
using FrozenApi.Services;
using System.Security.Claims;

namespace FrozenApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITimeZoneService _timeZoneService;

    public DashboardController(AppDbContext context, ITimeZoneService timeZoneService)
    {
        _context = context;
        _timeZoneService = timeZoneService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardData()
    {
        // Get user role from token
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Staff";
        var isAdmin = role == "Admin";

        // Get today's date range in Gulf Standard Time (GST, UTC+4)
        var today = _timeZoneService.GetCurrentDate();
        var startOfDay = DateTime.SpecifyKind(new DateTime(today.Year, today.Month, today.Day, 0, 0, 0), DateTimeKind.Unspecified);
        var endOfDay = DateTime.SpecifyKind(startOfDay.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        
        // Convert to UTC for database query
        var startOfDayUtc = _timeZoneService.ConvertToUtc(startOfDay);
        var endOfDayUtc = _timeZoneService.ConvertToUtc(endOfDay);

        // Calculate totals (excluding deleted sales)
        var totalSales = await _context.Sales
            .Where(s => !s.IsDeleted && s.InvoiceDate >= startOfDayUtc && s.InvoiceDate <= endOfDayUtc)
            .SumAsync(s => (decimal?)s.GrandTotal) ?? 0;

        var totalPurchases = await _context.Purchases
            .Where(p => p.PurchaseDate >= startOfDayUtc && p.PurchaseDate <= endOfDayUtc)
            .SumAsync(p => p.TotalAmount);

        var totalExpenses = await _context.Expenses
            .Where(e => e.Date >= startOfDayUtc && e.Date <= endOfDayUtc)
            .SumAsync(e => e.Amount);

        // Calculate profit (only for Admin)
        decimal? profitToday = null;
        if (isAdmin)
        {
            // Profit = Sales (Subtotal) - COGS (Cost of Goods Sold) - Expenses
            // CRITICAL: Use Subtotal for profit calc (VAT should not be included in profit)
            var totalSalesSubtotal = await _context.Sales
                .Where(s => !s.IsDeleted && s.InvoiceDate >= startOfDayUtc && s.InvoiceDate <= endOfDayUtc)
                .SumAsync(s => (decimal?)s.Subtotal) ?? 0;
            
            // Calculate the actual cost of goods sold for today's sales
            var salesItems = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.InvoiceDate >= startOfDayUtc && 
                            si.Sale.InvoiceDate <= endOfDayUtc && 
                            !si.Sale.IsDeleted)
                .ToListAsync();

            // COGS = Sum of (Qty * ConversionToBase * CostPrice)
            var costOfGoodsSold = salesItems.Sum(si => {
                var baseQty = si.Qty * (si.Product.ConversionToBase > 0 ? si.Product.ConversionToBase : 1);
                return baseQty * si.Product.CostPrice;
            });
            
            var grossProfit = totalSalesSubtotal - costOfGoodsSold;
            profitToday = grossProfit - totalExpenses;
            
            Console.WriteLine($"📊 Dashboard Profit: Sales (GrandTotal)={totalSales:C}, Sales (Subtotal)={totalSalesSubtotal:C}, COGS={costOfGoodsSold:C}, Expenses={totalExpenses:C}, Net Profit={profitToday:C}");
        }

        // Pending Bills Count (sales where PaymentStatus is Pending or Partial, excluding deleted)
        var pendingBillsCount = await _context.Sales
            .Where(s => !s.IsDeleted && (s.PaymentStatus == SalePaymentStatus.Pending || s.PaymentStatus == SalePaymentStatus.Partial))
            .CountAsync();
        
        // Pending Payments Amount (sales where PaymentStatus is Pending or Partial, excluding deleted)
        // Use Sale.PaidAmount directly (maintained atomically by PaymentService)
        var pendingPayments = await _context.Sales
            .Where(s => !s.IsDeleted && (s.PaymentStatus == SalePaymentStatus.Pending || s.PaymentStatus == SalePaymentStatus.Partial))
            .SumAsync(s => (decimal?)(s.GrandTotal - s.PaidAmount)) ?? 0;

        // Low Stock Alerts (products with stock less than 100)
        var lowStockProducts = await _context.Products
            .Where(p => p.StockQty < 100)
            .Select(p => new LowStockProduct
            {
                Id = p.Id,
                Name = p.NameEn,
                StockQty = p.StockQty,
                UnitType = p.UnitType
            })
            .ToListAsync();

        var response = new DashboardResponse
        {
            TotalSales = totalSales,
            TotalPurchases = totalPurchases,
            TotalExpenses = totalExpenses,
            ProfitToday = profitToday, // null for Staff
            PendingPayments = pendingPayments,
            PendingBillsCount = pendingBillsCount,
            LowStockAlerts = lowStockProducts,
            IsAdmin = isAdmin
        };

        return Ok(response);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetDetailedStatistics()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Staff";
        var isAdmin = role == "Admin";

        var today = _timeZoneService.GetCurrentDate();
        var startOfDay = DateTime.SpecifyKind(new DateTime(today.Year, today.Month, today.Day, 0, 0, 0), DateTimeKind.Unspecified);
        var endOfDay = DateTime.SpecifyKind(startOfDay.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        
        // Convert to UTC for database query
        var startOfDayUtc = _timeZoneService.ConvertToUtc(startOfDay);
        var endOfDayUtc = _timeZoneService.ConvertToUtc(endOfDay);

        // Get sales data (excluding deleted)
        var salesCount = await _context.Sales
            .Where(s => !s.IsDeleted && s.InvoiceDate >= startOfDayUtc && s.InvoiceDate <= endOfDayUtc)
            .CountAsync();

        var purchasesCount = await _context.Purchases
            .Where(p => p.PurchaseDate >= startOfDayUtc && p.PurchaseDate <= endOfDayUtc)
            .CountAsync();

        var expensesCount = await _context.Expenses
            .Where(e => e.Date >= startOfDayUtc && e.Date <= endOfDayUtc)
            .CountAsync();

        var pendingInvoicesCount = await _context.Sales
            .Where(s => !s.IsDeleted && (s.PaymentStatus == SalePaymentStatus.Pending || s.PaymentStatus == SalePaymentStatus.Partial))
            .CountAsync();

        var lowStockCount = await _context.Products
            .Where(p => p.StockQty < 100)
            .CountAsync();

        var response = new DashboardStatisticsResponse
        {
            SalesCount = salesCount,
            PurchasesCount = purchasesCount,
            ExpensesCount = expensesCount,
            PendingInvoicesCount = pendingInvoicesCount,
            LowStockCount = lowStockCount
        };

        return Ok(response);
    }
}

// DTOs
public class DashboardResponse
{
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal? ProfitToday { get; set; } // null for Staff role
    public decimal PendingPayments { get; set; }
    public int PendingBillsCount { get; set; }
    public List<LowStockProduct> LowStockAlerts { get; set; } = new();
    public bool IsAdmin { get; set; }
}

public class LowStockProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal StockQty { get; set; }
    public string UnitType { get; set; } = string.Empty;
}

public class DashboardStatisticsResponse
{
    public int SalesCount { get; set; }
    public int PurchasesCount { get; set; }
    public int ExpensesCount { get; set; }
    public int PendingInvoicesCount { get; set; }
    public int LowStockCount { get; set; }
}
