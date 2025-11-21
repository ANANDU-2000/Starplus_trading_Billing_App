/*
Purpose: Profit calculation service - Gross & Net Profit, COGS
Author: AI Assistant
Date: 2025
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IProfitService
    {
        Task<ProfitReportDto> CalculateProfitAsync(DateTime fromDate, DateTime toDate);
        Task<List<ProductProfitDto>> CalculateProductProfitAsync(DateTime fromDate, DateTime toDate);
        Task<DailyProfitDto> GetDailyProfitAsync(DateTime date);
    }

    public class ProfitService : IProfitService
    {
        private readonly AppDbContext _context;

        public ProfitService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ProfitReportDto> CalculateProfitAsync(DateTime fromDate, DateTime toDate)
        {
            // CRITICAL: Ensure date range includes full days
            var from = fromDate.Date;
            var to = toDate.Date.AddDays(1).AddTicks(-1); // End of day
            
            Console.WriteLine($"📊 CalculateProfitAsync: fromDate={from:yyyy-MM-dd}, toDate={to:yyyy-MM-dd HH:mm:ss}");
            
            // CRITICAL: Total Sales - use GrandTotal (includes VAT) for accurate reporting
            var totalSales = await _context.Sales
                .Where(s => s.InvoiceDate >= from && s.InvoiceDate <= to && !s.IsDeleted)
                .SumAsync(s => (decimal?)s.GrandTotal) ?? 0;

            // Sales Subtotal (excluding VAT) for COGS calculation
            var totalSalesSubtotal = await _context.Sales
                .Where(s => s.InvoiceDate >= from && s.InvoiceDate <= to && !s.IsDeleted)
                .SumAsync(s => (decimal?)s.Subtotal) ?? 0;

            var totalSalesVat = await _context.Sales
                .Where(s => s.InvoiceDate >= from && s.InvoiceDate <= to && !s.IsDeleted)
                .SumAsync(s => (decimal?)s.VatTotal) ?? 0;

            // CRITICAL: Calculate COGS (Cost of Goods Sold) - use actual product cost prices
            // COGS = Sum of (SaleItem.Qty * Product.CostPrice) for all sales in period
            var saleItems = await _context.SaleItems
                .Include(si => si.Sale)
                .Include(si => si.Product)
                .Where(si => si.Sale.InvoiceDate >= from && 
                            si.Sale.InvoiceDate <= to && 
                            !si.Sale.IsDeleted)
                .ToListAsync();
            
            // Calculate COGS with proper unit conversion and VAT handling
            var cogs = saleItems.Sum(si => {
                // Convert sale quantity to base unit for accurate cost calculation
                // CostPrice is already per base unit, so we need to convert sale qty to base unit
                var conversionFactor = si.Product.ConversionToBase > 0 ? si.Product.ConversionToBase : 1;
                var baseQty = si.Qty * conversionFactor;
                
                // CRITICAL: Use CostPrice which should already be VAT-excluded
                // After purchase VAT fix, CostPrice will be the actual cost without VAT
                return baseQty * si.Product.CostPrice;
            });

            // CRITICAL: Total Expenses - filter by date range
            var totalExpenses = await _context.Expenses
                .Where(e => e.Date >= from && e.Date <= to)
                .SumAsync(e => (decimal?)e.Amount) ?? 0;

            // CRITICAL: Total Purchases (for reference) - filter by date range
            var totalPurchases = await _context.Purchases
                .Where(p => p.PurchaseDate >= from && p.PurchaseDate <= to)
                .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;

            // CRITICAL: Calculate Profits using correct formulas
            // Gross Profit = Sales (Subtotal) - COGS
            var grossProfit = totalSalesSubtotal - cogs;
            // Net Profit = Gross Profit - Expenses
            var netProfit = grossProfit - totalExpenses;
            var grossProfitMargin = totalSalesSubtotal > 0 ? (grossProfit / totalSalesSubtotal) * 100 : 0;
            var netProfitMargin = totalSalesSubtotal > 0 ? (netProfit / totalSalesSubtotal) * 100 : 0;

            // CRITICAL: Calculate daily profit array for chart
            var dailyProfit = new List<DailyProfitDto>();
            var currentDate = from;
            while (currentDate <= toDate)
            {
                var dayStart = currentDate.Date;
                var dayEnd = dayStart.AddDays(1).AddTicks(-1);
                
                var daySales = await _context.Sales
                    .Where(s => s.InvoiceDate >= dayStart && s.InvoiceDate <= dayEnd && !s.IsDeleted)
                    .SumAsync(s => (decimal?)s.Subtotal) ?? 0;
                
                var daySaleItems = await _context.SaleItems
                    .Include(si => si.Sale)
                    .Include(si => si.Product)
                    .Where(si => si.Sale.InvoiceDate >= dayStart && 
                                si.Sale.InvoiceDate <= dayEnd && 
                                !si.Sale.IsDeleted)
                    .ToListAsync();
                
                var dayCogs = daySaleItems.Sum(si => {
                    var baseQty = si.Qty * (si.Product.ConversionToBase > 0 ? si.Product.ConversionToBase : 1);
                    return baseQty * si.Product.CostPrice;
                });
                
                var dayExpenses = await _context.Expenses
                    .Where(e => e.Date >= dayStart && e.Date <= dayEnd)
                    .SumAsync(e => (decimal?)e.Amount) ?? 0;
                
                var dayProfit = daySales - dayCogs - dayExpenses;
                
                dailyProfit.Add(new DailyProfitDto
                {
                    Date = currentDate,
                    Sales = daySales,
                    Expenses = dayExpenses,
                    Profit = dayProfit
                });
                
                currentDate = currentDate.AddDays(1);
            }

            Console.WriteLine($"✅ Profit Calculation: Sales (GrandTotal)={totalSales:C}, Sales (Subtotal)={totalSalesSubtotal:C}, VAT={totalSalesVat:C}, COGS={cogs:C}, Expenses={totalExpenses:C}, Net Profit={netProfit:C}");
            Console.WriteLine($"✅ Daily Profit entries: {dailyProfit.Count} days");

            return new ProfitReportDto
            {
                FromDate = from,
                ToDate = toDate,
                TotalSales = totalSales, // CRITICAL FIX: Use GrandTotal (with VAT) to match dashboard
                TotalSalesVat = totalSalesVat,
                TotalSalesWithVat = totalSales, // Same as TotalSales (GrandTotal includes VAT)
                CostOfGoodsSold = cogs,
                GrossProfit = grossProfit, // Gross Profit = Subtotal - COGS (VAT-excluded calculation)
                GrossProfitMargin = grossProfitMargin,
                TotalExpenses = totalExpenses,
                NetProfit = netProfit,
                NetProfitMargin = netProfitMargin,
                TotalPurchases = totalPurchases,
                DailyProfit = dailyProfit // CRITICAL: Include daily profit array
            };
        }

        public async Task<List<ProductProfitDto>> CalculateProductProfitAsync(DateTime fromDate, DateTime toDate)
        {
            var productProfits = await _context.SaleItems
                .Include(si => si.Sale)
                .Include(si => si.Product)
                .Where(si => si.Sale.InvoiceDate >= fromDate && 
                            si.Sale.InvoiceDate <= toDate && 
                            !si.Sale.IsDeleted)
                .GroupBy(si => new { si.ProductId, si.Product.NameEn, si.Product.CostPrice, si.Product.SellPrice })
                .Select(g => new ProductProfitDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.NameEn,
                    QuantitySold = g.Sum(si => si.Qty),
                    TotalSales = g.Sum(si => si.LineTotal),
                    TotalCost = g.Sum(si => si.Qty * g.Key.CostPrice),
                    Profit = g.Sum(si => si.LineTotal) - g.Sum(si => si.Qty * g.Key.CostPrice),
                    ProfitMargin = g.Sum(si => si.LineTotal) > 0 
                        ? ((g.Sum(si => si.LineTotal) - g.Sum(si => si.Qty * g.Key.CostPrice)) / g.Sum(si => si.LineTotal)) * 100 
                        : 0
                })
                .OrderByDescending(p => p.Profit)
                .ToListAsync();

            return productProfits;
        }

        public async Task<DailyProfitDto> GetDailyProfitAsync(DateTime date)
        {
            var fromDate = date.Date;
            var toDate = fromDate.AddDays(1);

            var profitReport = await CalculateProfitAsync(fromDate, toDate);

            return new DailyProfitDto
            {
                Date = date,
                Sales = profitReport.TotalSales,
                Expenses = profitReport.TotalExpenses,
                Profit = profitReport.NetProfit
            };
        }
    }
}

